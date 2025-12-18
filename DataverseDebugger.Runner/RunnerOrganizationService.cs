using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Runner
{
    /// <summary>
    /// Write mode for the runner organization service.
    /// </summary>
    internal enum RunnerWriteMode
    {
        /// <summary>Simulates write operations without making actual changes.</summary>
        FakeWrites = 0,

        /// <summary>Performs actual write operations against Dataverse.</summary>
        LiveWrites = 1
    }

    /// <summary>
    /// Organization service implementation that can operate in fake or live mode.
    /// Maintains an in-memory overlay of changes and optionally syncs with Dataverse via Web API.
    /// </summary>
    internal sealed class RunnerOrganizationService : IOrganizationService
    {
        private readonly Action<string> _log;
        private readonly HttpClient _http;
        private readonly string? _accessToken;
        private readonly RunnerWriteMode _writeMode;
        private readonly Func<string, string?> _entitySetResolver;
        private readonly AttributeMetadataResolver? _attributeResolver;
        private readonly string? _apiBaseUrl;
        private readonly Dictionary<string, Dictionary<Guid, Entity>> _overlay =
            new Dictionary<string, Dictionary<Guid, Entity>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _deleted =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the runner organization service.
        /// </summary>
        /// <param name="log">Logging delegate for operation tracing.</param>
        /// <param name="http">HTTP client for Web API calls.</param>
        /// <param name="orgUrl">Dataverse organization URL.</param>
        /// <param name="accessToken">OAuth access token for authentication.</param>
        /// <param name="writeMode">Whether to perform live or fake writes.</param>
        /// <param name="entitySetResolver">Function to resolve entity logical names to Web API entity set names.</param>
        public RunnerOrganizationService(Action<string> log, HttpClient http, string? orgUrl, string? accessToken, RunnerWriteMode writeMode, Func<string, string?> entitySetResolver, AttributeMetadataResolver? attributeResolver)
        {
            _log = log;
            _http = http;
            _accessToken = accessToken;
            _writeMode = writeMode;
            _entitySetResolver = entitySetResolver;
            _attributeResolver = attributeResolver;
            _apiBaseUrl = BuildApiBaseUrl(orgUrl);
        }

        /// <inheritdoc />
        public Guid Create(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var id = entity.Id != Guid.Empty ? entity.Id : Guid.NewGuid();
            entity.Id = id;
            UpsertOverlay(entity);

            if (_writeMode == RunnerWriteMode.LiveWrites)
            {
                var response = SendWebApi(HttpMethod.Post, BuildEntitySetUrl(entity.LogicalName), BuildEntityPayload(entity, includeId: false));
                EnsureSuccess(response, "Create");
                var createdId = TryGetIdFromResponse(response) ?? id;
                if (createdId != id)
                {
                    entity.Id = createdId;
                    UpsertOverlay(entity);
                }
            }
            else
            {
                _log($"[OrgService] Create {entity.LogicalName} (fake)");
            }

            return entity.Id;
        }

        /// <inheritdoc />
        public void Update(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id == Guid.Empty) throw new InvalidOperationException("Update requires an entity Id.");

            UpsertOverlay(entity);
            if (_writeMode == RunnerWriteMode.LiveWrites)
            {
                var url = BuildEntityIdUrl(entity.LogicalName, entity.Id);
                var response = SendWebApi(new HttpMethod("PATCH"), url, BuildEntityPayload(entity, includeId: false));
                EnsureSuccess(response, "Update");
            }
            else
            {
                _log($"[OrgService] Update {entity.LogicalName} {entity.Id} (fake)");
            }
        }

        /// <inheritdoc />
        public void Delete(string entityName, Guid id)
        {
            if (string.IsNullOrWhiteSpace(entityName)) throw new ArgumentNullException(nameof(entityName));
            if (id == Guid.Empty) throw new ArgumentException("Delete requires a valid id.");

            MarkDeleted(entityName, id);
            if (_writeMode == RunnerWriteMode.LiveWrites)
            {
                var response = SendWebApi(HttpMethod.Delete, BuildEntityIdUrl(entityName, id), null);
                EnsureSuccess(response, "Delete");
            }
            else
            {
                _log($"[OrgService] Delete {entityName} {id} (fake)");
            }
        }

        /// <inheritdoc />
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            if (TryGetOverlayEntity(entityName, id, columnSet, out var overlay))
            {
                _log($"[OrgService] Retrieve {entityName} {id} (overlay)");
                return overlay;
            }

            EnsureLiveRead();
            var url = BuildEntityIdUrl(entityName, id);
            var attributeMap = GetAttributeMap(entityName);
            var select = BuildSelect(columnSet, attributeMap);
            if (!string.IsNullOrWhiteSpace(select))
            {
                url += "?$select=" + Uri.EscapeDataString(select);
            }
            var response = SendWebApi(HttpMethod.Get, url, null);
            EnsureSuccess(response, "Retrieve");
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var entity = ParseEntityFromWebApi(json, entityName, id, attributeMap);
            return entity ?? new Entity(entityName) { Id = id };
        }

        /// <inheritdoc />
        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var logicalName = ResolveEntityName(query);
            var results = new EntityCollection();
            if (!string.IsNullOrWhiteSpace(logicalName))
            {
                results.EntityName = logicalName;
            }

            var overlayApplied = false;
            if (TryGetOverlayEntities(logicalName, out var overlayEntities))
            {
                results.Entities.AddRange(overlayEntities);
                overlayApplied = overlayEntities.Count > 0;
            }

            if (!HasLiveAccess())
            {
                _log("[OrgService] RetrieveMultiple live read unavailable; returning overlay only.");
                return results;
            }

            var fetchXml = GetFetchXml(query);
            if (string.IsNullOrWhiteSpace(fetchXml))
            {
                _log("[OrgService] RetrieveMultiple fetchXml unavailable; returning overlay only.");
                return results;
            }

            var entitySet = ResolveEntitySetFromFetch(fetchXml, logicalName);
            if (string.IsNullOrWhiteSpace(entitySet))
            {
                _log("[OrgService] RetrieveMultiple entity set not resolved; returning overlay only.");
                return results;
            }

            var attributeMap = string.IsNullOrWhiteSpace(logicalName) ? null : GetAttributeMap(logicalName!);
            var url = $"{_apiBaseUrl}{entitySet}?fetchXml={Uri.EscapeDataString(fetchXml)}";
            var response = SendWebApi(HttpMethod.Get, url, null);
            EnsureSuccess(response, "RetrieveMultiple");
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var live = ParseEntitiesFromWebApi(json, logicalName, attributeMap);
            results.Entities.Clear();
            results.Entities.AddRange(live);
            if (overlayApplied)
            {
                MergeOverlay(results, logicalName, overlayEntities);
            }
            return results;
        }

        /// <inheritdoc />
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            switch (request)
            {
                case RetrieveMultipleRequest retrieveMultiple:
                    var collection = RetrieveMultiple(retrieveMultiple.Query);
                    var response = new RetrieveMultipleResponse();
                    response.Results["EntityCollection"] = collection;
                    return response;
                case RetrieveRequest retrieve:
                    var entity = Retrieve(retrieve.Target.LogicalName, retrieve.Target.Id, retrieve.ColumnSet);
                    var retrieveResponse = new RetrieveResponse();
                    retrieveResponse.Results["Entity"] = entity;
                    return retrieveResponse;
                case CreateRequest create:
                    var createId = Create(create.Target);
                    var createResponse = new CreateResponse();
                    createResponse.Results["id"] = createId;
                    return createResponse;
                case UpdateRequest update:
                    Update(update.Target);
                    return new UpdateResponse();
                case DeleteRequest delete:
                    Delete(delete.Target.LogicalName, delete.Target.Id);
                    return new DeleteResponse();
            }

            if (string.Equals(request.RequestName, "WhoAmI", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteWhoAmI();
            }

            _log($"[OrgService] Execute {request.RequestName} not implemented.");
            return new OrganizationResponse();
        }

        /// <inheritdoc />
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            _log($"[OrgService] Associate {entityName} {entityId} (not implemented)");
        }

        /// <inheritdoc />
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            _log($"[OrgService] Disassociate {entityName} {entityId} (not implemented)");
        }

        private OrganizationResponse ExecuteWhoAmI()
        {
            if (!HasLiveAccess())
            {
                _log("[OrgService] WhoAmI live read unavailable; returning empty response.");
                return new OrganizationResponse();
            }

            var url = $"{_apiBaseUrl}WhoAmI";
            var response = SendWebApi(HttpMethod.Get, url, null);
            EnsureSuccess(response, "WhoAmI");
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var who = new OrganizationResponse();
                if (root.TryGetProperty("UserId", out var userId) && Guid.TryParse(userId.GetString(), out var userGuid))
                {
                    who.Results["UserId"] = userGuid;
                }
                if (root.TryGetProperty("BusinessUnitId", out var buId) && Guid.TryParse(buId.GetString(), out var buGuid))
                {
                    who.Results["BusinessUnitId"] = buGuid;
                }
                if (root.TryGetProperty("OrganizationId", out var orgId) && Guid.TryParse(orgId.GetString(), out var orgGuid))
                {
                    who.Results["OrganizationId"] = orgGuid;
                }
                return who;
            }
            catch
            {
                return new OrganizationResponse();
            }
        }

        private static string? BuildApiBaseUrl(string? orgUrl)
        {
            if (string.IsNullOrWhiteSpace(orgUrl)) return null;
            if (!Uri.TryCreate(orgUrl, UriKind.Absolute, out var uri)) return null;
            var baseUri = $"{uri.Scheme}://{uri.Host}";
            return baseUri.TrimEnd('/') + "/api/data/v9.2/";
        }

        private string BuildEntitySetUrl(string logicalName)
        {
            var setName = ResolveEntitySet(logicalName);
            if (string.IsNullOrWhiteSpace(setName) || string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                throw new InvalidOperationException("Entity set name not resolved.");
            }
            return _apiBaseUrl + setName;
        }

        private string BuildEntityIdUrl(string logicalName, Guid id)
        {
            return BuildEntitySetUrl(logicalName) + $"({id})";
        }

        private Dictionary<string, AttributeShape>? GetAttributeMap(string logicalName)
        {
            if (string.IsNullOrWhiteSpace(logicalName) || _attributeResolver == null)
            {
                return null;
            }

            return _attributeResolver.GetAttributeMap(logicalName);
        }

        private string ResolveEntitySet(string logicalName)
        {
            if (string.IsNullOrWhiteSpace(logicalName)) return logicalName;
            var resolved = _entitySetResolver?.Invoke(logicalName);
            if (resolved != null && !string.IsNullOrWhiteSpace(resolved)) return resolved;
            return logicalName + "s";
        }

        private void EnsureLiveRead()
        {
            if (!HasLiveAccess())
            {
                throw new InvalidOperationException("Live read requires Org URL and access token.");
            }
        }

        private bool HasLiveAccess()
        {
            return !string.IsNullOrWhiteSpace(_apiBaseUrl) && !string.IsNullOrWhiteSpace(_accessToken);
        }

        private HttpResponseMessage SendWebApi(HttpMethod method, string url, Dictionary<string, object?>? payload)
        {
            var request = new HttpRequestMessage(method, url);
            if (payload != null)
            {
                var json = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            if (!string.IsNullOrWhiteSpace(_accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            }

            request.Headers.TryAddWithoutValidation("Prefer", "odata.include-annotations=\"*\"");

            return _http.SendAsync(request).GetAwaiter().GetResult();
        }

        private void EnsureSuccess(HttpResponseMessage response, string action)
        {
            if (response.IsSuccessStatusCode) return;
            var detail = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException($"{action} failed: {(int)response.StatusCode} {response.ReasonPhrase} {detail}");
        }

        private Guid? TryGetIdFromResponse(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("OData-EntityId", out var values))
            {
                var val = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    var start = val.LastIndexOf('(');
                    var end = val.LastIndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        var idText = val.Substring(start + 1, end - start - 1);
                        if (Guid.TryParse(idText, out var guid)) return guid;
                    }
                }
            }
            return null;
        }

        private Dictionary<string, object?> BuildEntityPayload(Entity entity, bool includeId)
        {
            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in entity.Attributes)
            {
                var attr = kvp.Key;
                var value = kvp.Value;
                if (value == null)
                {
                    payload[attr] = null;
                    continue;
                }

                if (value is AliasedValue aliased)
                {
                    value = aliased.Value;
                    if (value == null)
                    {
                        payload[attr] = null;
                        continue;
                    }
                }

                if (value is EntityReference er)
                {
                    var setName = ResolveEntitySet(er.LogicalName);
                    payload[$"{attr}@odata.bind"] = $"/{setName}({er.Id})";
                    continue;
                }

                if (value is OptionSetValue osv)
                {
                    payload[attr] = osv.Value;
                    continue;
                }

                if (value is Money money)
                {
                    payload[attr] = money.Value;
                    continue;
                }

                if (value is DateTime dt)
                {
                    payload[attr] = dt.ToUniversalTime().ToString("o");
                    continue;
                }

                if (value is Guid guid)
                {
                    payload[attr] = guid;
                    continue;
                }

                if (value is bool || value is string || value is int || value is long || value is double || value is decimal)
                {
                    payload[attr] = value;
                    continue;
                }

                _log($"[OrgService] Skipping unsupported attribute {attr} ({value.GetType().Name}).");
            }

            if (includeId && entity.Id != Guid.Empty)
            {
                payload["id"] = entity.Id;
            }

            return payload;
        }

        private void UpsertOverlay(Entity entity)
        {
            if (entity == null || string.IsNullOrWhiteSpace(entity.LogicalName)) return;
            var logicalName = entity.LogicalName;
            if (!_overlay.TryGetValue(logicalName, out var bucket))
            {
                bucket = new Dictionary<Guid, Entity>();
                _overlay[logicalName] = bucket;
            }

            if (!bucket.TryGetValue(entity.Id, out var stored))
            {
                stored = new Entity(logicalName) { Id = entity.Id };
                bucket[entity.Id] = stored;
            }

            foreach (var attr in entity.Attributes)
            {
                stored[attr.Key] = attr.Value;
            }

            var key = BuildDeletedKey(logicalName, entity.Id);
            _deleted.Remove(key);
        }

        private void MarkDeleted(string logicalName, Guid id)
        {
            var key = BuildDeletedKey(logicalName, id);
            _deleted.Add(key);
            if (_overlay.TryGetValue(logicalName, out var bucket))
            {
                bucket.Remove(id);
            }
        }

        private static string BuildDeletedKey(string logicalName, Guid id)
        {
            return $"{logicalName}:{id}";
        }

        private bool TryGetOverlayEntity(string logicalName, Guid id, ColumnSet columnSet, out Entity entity)
        {
            entity = null!;
            if (string.IsNullOrWhiteSpace(logicalName)) return false;
            if (_deleted.Contains(BuildDeletedKey(logicalName, id))) return false;
            if (_overlay.TryGetValue(logicalName, out var bucket) && bucket.TryGetValue(id, out var stored))
            {
                entity = CloneEntity(stored, columnSet);
                return true;
            }
            return false;
        }

        private bool TryGetOverlayEntities(string? logicalName, out List<Entity> entities)
        {
            entities = new List<Entity>();
            if (logicalName == null) return false;
            if (string.IsNullOrWhiteSpace(logicalName)) return false;
            string key = logicalName;
            if (_overlay.TryGetValue(key, out var bucket))
            {
                foreach (var e in bucket.Values)
                {
                    if (_deleted.Contains(BuildDeletedKey(key, e.Id))) continue;
                    entities.Add(CloneEntity(e, new ColumnSet(true)));
                }
                return entities.Count > 0;
            }
            return false;
        }

        private static void MergeOverlay(EntityCollection target, string? logicalName, List<Entity> overlay)
        {
            if (target == null || overlay == null || overlay.Count == 0) return;
            var ids = new HashSet<Guid>(target.Entities.Select(e => e.Id));
            foreach (var entity in overlay)
            {
                if (entity.Id == Guid.Empty || ids.Contains(entity.Id)) continue;
                target.Entities.Add(entity);
            }
        }

        private static Entity CloneEntity(Entity source, ColumnSet columnSet)
        {
            var clone = new Entity(source.LogicalName) { Id = source.Id };
            if (columnSet == null || columnSet.AllColumns)
            {
                foreach (var attr in source.Attributes)
                {
                    clone[attr.Key] = attr.Value;
                }
                return clone;
            }

            foreach (var col in columnSet.Columns)
            {
                if (source.Attributes.TryGetValue(col, out var value))
                {
                    clone[col] = value;
                }
            }
            return clone;
        }

        private string? BuildSelect(ColumnSet columnSet, Dictionary<string, AttributeShape>? attributeMap)
        {
            if (columnSet == null || columnSet.AllColumns || columnSet.Columns.Count == 0)
            {
                return null;
            }

            var selects = new List<string>();
            foreach (var column in columnSet.Columns)
            {
                if (string.IsNullOrWhiteSpace(column))
                {
                    continue;
                }

                selects.Add(MapAttributeToSelect(column, attributeMap));
            }

            return selects.Count == 0 ? null : string.Join(",", selects);
        }

        private static string MapAttributeToSelect(string logicalName, Dictionary<string, AttributeShape>? map)
        {
            if (map != null && map.TryGetValue(logicalName, out var shape) && shape != null)
            {
                var type = shape.AttributeType;
                if (type == AttributeTypeCode.Lookup || type == AttributeTypeCode.Customer || type == AttributeTypeCode.Owner)
                {
                    return "_" + logicalName + "_value";
                }
            }

            return logicalName;
        }

        private static string? ResolveEntityName(QueryBase query)
        {
            switch (query)
            {
                case QueryExpression qe:
                    return qe.EntityName;
                case FetchExpression fe:
                    return ExtractEntityFromFetch(fe.Query);
                default:
                    return null;
            }
        }

        private static string? GetFetchXml(QueryBase query)
        {
            switch (query)
            {
                case FetchExpression fetch:
                    return fetch.Query;
                case QueryExpression qe:
                    var method = qe.GetType().GetMethod("ToFetchXml", Type.EmptyTypes);
                    if (method != null)
                    {
                        return method.Invoke(qe, null) as string;
                    }
                    return QueryExpressionFetchXmlConverter.TryConvert(qe, out var generated)
                        ? generated
                        : null;
                default:
                    return null;
            }
        }

        private string? ResolveEntitySetFromFetch(string? fetchXml, string? logicalName)
        {
            var logical = logicalName ?? ExtractEntityFromFetch(fetchXml);
            if (logical == null) return null;
            if (string.IsNullOrWhiteSpace(logical)) return null;
            return ResolveEntitySet(logical);
        }

        private static string? ExtractEntityFromFetch(string? fetchXml)
        {
            if (fetchXml == null) return null;
            if (string.IsNullOrWhiteSpace(fetchXml)) return null;
            var marker = "name=\"";
            var index = fetchXml.IndexOf("<entity", StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;
            var nameIndex = fetchXml.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0) return null;
            nameIndex += marker.Length;
            var end = fetchXml.IndexOf("\"", nameIndex, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return null;
            return fetchXml.Substring(nameIndex, end - nameIndex);
        }

        private static Entity? ParseEntityFromWebApi(string json, string logicalName, Guid id, Dictionary<string, AttributeShape>? attributeMap)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                return ParseEntityElement(doc.RootElement, logicalName, id, attributeMap);
            }
            catch
            {
                return null;
            }
        }

        private static List<Entity> ParseEntitiesFromWebApi(string json, string? logicalName, Dictionary<string, AttributeShape>? attributeMap)
        {
            var list = new List<Entity>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
                {
                    return list;
                }

                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var entity = ParseEntityElement(item, logicalName ?? string.Empty, Guid.Empty, attributeMap);
                    if (entity != null)
                    {
                        list.Add(entity);
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }
            return list;
        }

        private static Entity? ParseEntityElement(JsonElement element, string logicalName, Guid id, Dictionary<string, AttributeShape>? attributeMap)
        {
            var entity = new Entity(logicalName);
            if (id != Guid.Empty)
            {
                entity.Id = id;
            }

            var lookupLogicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name.IndexOf("@Microsoft.Dynamics.CRM.lookuplogicalname", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var attrName = NormalizeLookupAttributeName(prop.Name.Split('@')[0]);
                    var value = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(attrName) && value != null && !string.IsNullOrWhiteSpace(value))
                    {
                        lookupLogicalNames[attrName] = value;
                    }
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                var name = prop.Name;
                if (name.StartsWith("@", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Contains("@")) continue;

                if (string.Equals(name, "logicalName", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
                {
                    if (Guid.TryParse(prop.Value.GetString(), out var gid))
                    {
                        entity.Id = gid;
                    }
                    continue;
                }

                if (name.StartsWith("_", StringComparison.OrdinalIgnoreCase) && name.EndsWith("_value", StringComparison.OrdinalIgnoreCase))
                {
                    var attr = NormalizeLookupAttributeName(name);
                    if (Guid.TryParse(prop.Value.GetString(), out var lookupId))
                    {
                        if (lookupLogicalNames.TryGetValue(attr, out var lookupLogical))
                        {
                            entity[attr] = new EntityReference(lookupLogical, lookupId);
                        }
                        else
                        {
                            entity[attr] = lookupId;
                        }
                    }
                    continue;
                }

                AttributeShape? attributeShape = null;
                attributeMap?.TryGetValue(name, out attributeShape);
                var typedValue = ConvertJsonValue(prop.Value, attributeShape);

                if (typedValue != null)
                {
                    entity[name] = typedValue;
                }
            }

            return entity;
        }

        private static object? ConvertJsonValue(JsonElement element, AttributeShape? shape)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (shape != null)
            {
                var attributeType = shape.AttributeType;
                var typeName = shape.AttributeTypeName?.ToLowerInvariant();

                try
                {
                    if (attributeType.HasValue)
                    {
                        switch (attributeType.Value)
                        {
                            case AttributeTypeCode.Boolean:
                                var boolValue = ReadBoolean(element);
                                if (boolValue.HasValue)
                                {
                                    return boolValue.Value;
                                }
                                break;
                            case AttributeTypeCode.DateTime:
                                var dateValue = ReadDateTime(element);
                                if (dateValue.HasValue)
                                {
                                    return dateValue.Value;
                                }
                                break;
                            case AttributeTypeCode.Decimal:
                                var decValue = ReadDecimal(element);
                                if (decValue.HasValue)
                                {
                                    return decValue.Value;
                                }
                                break;
                            case AttributeTypeCode.Double:
                                var doubleValue = ReadDouble(element);
                                if (doubleValue.HasValue)
                                {
                                    return doubleValue.Value;
                                }
                                break;
                            case AttributeTypeCode.Integer:
                                var intValue = ReadInt(element);
                                if (intValue.HasValue)
                                {
                                    return intValue.Value;
                                }
                                break;
                            case AttributeTypeCode.BigInt:
                                var longValue = ReadLong(element);
                                if (longValue.HasValue)
                                {
                                    return longValue.Value;
                                }
                                break;
                            case AttributeTypeCode.Money:
                                var moneyValue = ReadDecimal(element);
                                if (moneyValue.HasValue)
                                {
                                    return new Money(moneyValue.Value);
                                }
                                break;
                            case AttributeTypeCode.Picklist:
                            case AttributeTypeCode.State:
                            case AttributeTypeCode.Status:
                                var optionValue = ReadInt(element);
                                if (optionValue.HasValue)
                                {
                                    return new OptionSetValue(optionValue.Value);
                                }
                                break;
                            case AttributeTypeCode.Uniqueidentifier:
                                var guidValue = ReadGuid(element);
                                if (guidValue.HasValue)
                                {
                                    return guidValue.Value;
                                }
                                break;
                            case AttributeTypeCode.String:
                            case AttributeTypeCode.Memo:
                            case AttributeTypeCode.EntityName:
                                var stringValue = ReadString(element);
                                if (stringValue != null)
                                {
                                    return stringValue;
                                }
                                break;
                        }
                    }

                    if (attributeType == null && typeName == "moneytype")
                    {
                        var moneyValue = ReadDecimal(element);
                        if (moneyValue.HasValue)
                        {
                            return new Money(moneyValue.Value);
                        }
                    }
                }
                catch
                {
                    // Ignore conversion issues and fall back to default parsing
                }
            }

            return ConvertJsonValueDefault(element);
        }

        private static object? ConvertJsonValueDefault(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    var s = element.GetString();
                    if (Guid.TryParse(s, out var guid))
                    {
                        return guid;
                    }
                    return s;
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l))
                    {
                        return l;
                    }
                    if (element.TryGetDouble(out var d))
                    {
                        return d;
                    }
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
            }

            return null;
        }

        private static bool? ReadBoolean(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var number))
                    {
                        return number != 0;
                    }
                    break;
                case JsonValueKind.String:
                    var str = element.GetString();
                    if (bool.TryParse(str, out var boolean))
                    {
                        return boolean;
                    }
                    if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        return intValue != 0;
                    }
                    break;
            }
            return null;
        }

        private static DateTime? ReadDateTime(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = element.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal | DateTimeStyles.RoundtripKind, out var value))
            {
                if (value.Kind == DateTimeKind.Unspecified)
                {
                    return DateTime.SpecifyKind(value, DateTimeKind.Utc);
                }
                return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            }

            return null;
        }

        private static decimal? ReadDecimal(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static double? ReadDouble(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static int? ReadInt(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static long? ReadLong(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static Guid? ReadGuid(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (Guid.TryParse(text, out var guid))
                {
                    return guid;
                }
            }

            return null;
        }

        private static string? ReadString(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }

            return element.ToString();
        }

        private static string NormalizeLookupAttributeName(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                return attributeName;
            }

            var name = attributeName;
            if (name.StartsWith("_", StringComparison.OrdinalIgnoreCase) && name.EndsWith("_value", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(1, name.Length - "_value".Length - 1);
            }

            return name;
        }
    }
}
