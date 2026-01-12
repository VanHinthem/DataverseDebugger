using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DataverseDebugger.Runner.Abstractions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Runner.Services.Hybrid
{
    internal sealed class HybridOrganizationService : IOrganizationService
    {
        private readonly IOrganizationService? _inner;
        private readonly Action<string>? _log;
        private readonly Dictionary<string, Dictionary<Guid, Entity>> _creates =
            new Dictionary<string, Dictionary<Guid, Entity>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<Guid, Entity>> _updates =
            new Dictionary<string, Dictionary<Guid, Entity>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _deletes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HybridOrganizationService(IOrganizationService? inner, Action<string>? log = null)
        {
            _inner = inner;
            _log = log;
        }

        public Guid Create(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(entity.LogicalName))
            {
                throw new InvalidOperationException("Create requires an entity logical name.");
            }

            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            CacheCreate(entity);
            _log?.Invoke($"[Hybrid] Create cached {entity.LogicalName} {entity.Id}");
            return entity.Id;
        }

        public void Update(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(entity.LogicalName))
            {
                throw new InvalidOperationException("Update requires an entity logical name.");
            }
            if (entity.Id == Guid.Empty) throw new InvalidOperationException("Update requires an entity Id.");

            CacheUpdate(entity);
            _log?.Invoke($"[Hybrid] Update cached {entity.LogicalName} {entity.Id}");
        }

        public void Delete(string entityName, Guid id)
        {
            if (string.IsNullOrWhiteSpace(entityName)) throw new ArgumentNullException(nameof(entityName));
            if (id == Guid.Empty) throw new ArgumentException("Delete requires a valid id.");

            RemoveCacheEntry(entityName, id);
            _deletes.Add(BuildKey(entityName, id));
            _log?.Invoke($"[Hybrid] Delete cached {entityName} {id}");
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request is WhoAmIRequest)
            {
                if (_inner != null)
                {
                    return _inner.Execute(request) ?? new OrganizationResponse();
                }

                _log?.Invoke("[Hybrid] WhoAmI requested with no live service.");
                return new OrganizationResponse();
            }

            throw new RunnerNotSupportedException(
                "Hybrid",
                request.GetType().Name,
                "Only WhoAmIRequest is supported in Hybrid mode.");
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            if (string.IsNullOrWhiteSpace(entityName)) throw new ArgumentNullException(nameof(entityName));
            if (id == Guid.Empty) throw new ArgumentException("Retrieve requires a valid id.");

            if (IsDeleted(entityName, id))
            {
                _log?.Invoke($"[Hybrid] Retrieve suppressed for deleted {entityName} {id}.");
                return new Entity(entityName) { Id = id };
            }

            if (TryGetCachedEntity(entityName, id, out var cached))
            {
                var resolved = ResolveMergedEntity(entityName, id, cached, columnSet);
                _log?.Invoke($"[Hybrid] Retrieve merged for cached {entityName} {id}.");
                return resolved;
            }

            if (_inner != null)
            {
                return _inner.Retrieve(entityName, id, columnSet);
            }

            return new Entity(entityName) { Id = id };
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            var logicalName = ResolveEntityName(query);
            var requested = GetRequestedAttributes(query, logicalName);
            var liveResults = _inner?.RetrieveMultiple(query) ?? new EntityCollection();
            var mergedResults = new EntityCollection
            {
                EntityName = string.IsNullOrWhiteSpace(liveResults.EntityName) ? logicalName : liveResults.EntityName,
                MoreRecords = liveResults.MoreRecords,
                PagingCookie = liveResults.PagingCookie,
                TotalRecordCount = liveResults.TotalRecordCount
            };

            var liveIds = new HashSet<Guid>();
            foreach (var entity in liveResults.Entities)
            {
                if (entity == null)
                {
                    continue;
                }

                var id = entity.Id;
                if (id == Guid.Empty)
                {
                    mergedResults.Entities.Add(entity);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(logicalName))
                {
                    var resolvedName = logicalName!;
                    if (IsDeleted(resolvedName, id))
                    {
                        _log?.Invoke($"[Hybrid] RetrieveMultiple removed deleted {resolvedName} {id}.");
                        continue;
                    }

                    if (TryGetCachedEntity(resolvedName, id, out var cached))
                    {
                        var merged = MergeCachedWithLive(resolvedName, id, cached, entity, requested);
                        mergedResults.Entities.Add(merged);
                        liveIds.Add(id);
                        _log?.Invoke($"[Hybrid] RetrieveMultiple merged cached {resolvedName} {id}.");
                        continue;
                    }
                }

                mergedResults.Entities.Add(entity);
                liveIds.Add(id);
            }

            if (!string.IsNullOrWhiteSpace(logicalName))
            {
                var resolvedName = logicalName!;
                if (TryGetIdTargetedIds(query, resolvedName, out var targetedIds))
                {
                    foreach (var id in targetedIds)
                    {
                        if (liveIds.Contains(id))
                        {
                            continue;
                        }

                        if (TryGetCachedCreate(resolvedName, id, out var cachedCreate))
                        {
                            var merged = MergeCachedWithLive(resolvedName, id, cachedCreate, null, requested);
                            mergedResults.Entities.Add(merged);
                            _log?.Invoke($"[Hybrid] RetrieveMultiple injected cached create {resolvedName} {id}.");
                        }
                    }
                }
            }

            return mergedResults;
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            throw new RunnerNotSupportedException(
                "Hybrid",
                "Associate",
                "Associate is not supported in Hybrid mode.");
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            throw new RunnerNotSupportedException(
                "Hybrid",
                "Disassociate",
                "Disassociate is not supported in Hybrid mode.");
        }

        private void CacheCreate(Entity entity)
        {
            var logicalName = entity.LogicalName;
            if (!_creates.TryGetValue(logicalName, out var bucket))
            {
                bucket = new Dictionary<Guid, Entity>();
                _creates[logicalName] = bucket;
            }

            var clone = CloneEntity(entity, new ColumnSet(true));
            bucket[clone.Id] = clone;
            if (_updates.TryGetValue(logicalName, out var updateBucket))
            {
                updateBucket.Remove(clone.Id);
            }
            _deletes.Remove(BuildKey(logicalName, clone.Id));
        }

        private void CacheUpdate(Entity entity)
        {
            var logicalName = entity.LogicalName;
            if (TryGetCachedCreate(logicalName, entity.Id, out var created))
            {
                ApplyAttributes(created, entity);
                _deletes.Remove(BuildKey(logicalName, entity.Id));
                return;
            }

            if (!_updates.TryGetValue(logicalName, out var bucket))
            {
                bucket = new Dictionary<Guid, Entity>();
                _updates[logicalName] = bucket;
            }

            if (!bucket.TryGetValue(entity.Id, out var stored))
            {
                stored = new Entity(logicalName) { Id = entity.Id };
                bucket[entity.Id] = stored;
            }

            ApplyAttributes(stored, entity);
            _deletes.Remove(BuildKey(logicalName, entity.Id));
        }

        private void RemoveCacheEntry(string logicalName, Guid id)
        {
            if (_creates.TryGetValue(logicalName, out var createBucket))
            {
                createBucket.Remove(id);
            }

            if (_updates.TryGetValue(logicalName, out var updateBucket))
            {
                updateBucket.Remove(id);
            }
        }

        private static void ApplyAttributes(Entity target, Entity source)
        {
            foreach (var attr in source.Attributes)
            {
                target[attr.Key] = CloneAttributeValue(attr.Value);
            }

            foreach (var formatted in source.FormattedValues)
            {
                target.FormattedValues[formatted.Key] = formatted.Value;
            }

            if (source.KeyAttributes != null && target.KeyAttributes != null)
            {
                foreach (var keyAttr in source.KeyAttributes)
                {
                    target.KeyAttributes[keyAttr.Key] = CloneAttributeValue(keyAttr.Value);
                }
            }
        }

        private bool IsDeleted(string logicalName, Guid id)
        {
            return _deletes.Contains(BuildKey(logicalName, id));
        }

        private bool TryGetCachedCreate(string logicalName, Guid id, out Entity entity)
        {
            entity = null!;
            if (_creates.TryGetValue(logicalName, out var bucket) && bucket.TryGetValue(id, out var stored))
            {
                entity = stored;
                return true;
            }
            return false;
        }

        private bool TryGetCachedUpdate(string logicalName, Guid id, out Entity entity)
        {
            entity = null!;
            if (_updates.TryGetValue(logicalName, out var bucket) && bucket.TryGetValue(id, out var stored))
            {
                entity = stored;
                return true;
            }
            return false;
        }

        private bool TryGetCachedEntity(string logicalName, Guid id, out Entity entity)
        {
            if (TryGetCachedCreate(logicalName, id, out entity))
            {
                return true;
            }

            if (TryGetCachedUpdate(logicalName, id, out entity))
            {
                return true;
            }

            entity = null!;
            return false;
        }

        private Entity ResolveMergedEntity(string logicalName, Guid id, Entity cached, ColumnSet columnSet)
        {
            var requested = GetRequestedAttributes(columnSet);
            if (_inner == null)
            {
                return MergeCachedWithLive(logicalName, id, cached, null, requested);
            }

            if (!requested.AllAttributes && requested.Attributes.Count == 0)
            {
                return MergeCachedWithLive(logicalName, id, cached, null, requested);
            }

            Entity? live = null;
            if (requested.AllAttributes)
            {
                live = _inner.Retrieve(logicalName, id, new ColumnSet(true));
            }
            else
            {
                var missing = GetMissingAttributes(cached, requested.Attributes);
                if (missing.Count > 0)
                {
                    live = _inner.Retrieve(logicalName, id, new ColumnSet(missing.ToArray()));
                }
            }

            return MergeCachedWithLive(logicalName, id, cached, live, requested);
        }

        private static HashSet<string> GetMissingAttributes(Entity cached, HashSet<string> requested)
        {
            var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attr in requested)
            {
                if (string.IsNullOrWhiteSpace(attr))
                {
                    continue;
                }

                if (!cached.Attributes.Contains(attr))
                {
                    missing.Add(attr);
                }
            }
            return missing;
        }

        private static RequestedAttributes GetRequestedAttributes(ColumnSet? columnSet)
        {
            if (columnSet == null || columnSet.AllColumns)
            {
                return RequestedAttributes.All();
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columnSet.Columns)
            {
                if (!string.IsNullOrWhiteSpace(column))
                {
                    set.Add(column);
                }
            }

            return new RequestedAttributes(false, set);
        }

        private static RequestedAttributes GetRequestedAttributes(QueryBase query, string? logicalName)
        {
            switch (query)
            {
                case QueryExpression qe:
                    return GetRequestedAttributes(qe.ColumnSet);
                case FetchExpression fetch:
                    return GetRequestedAttributesFromFetch(fetch.Query, logicalName);
                default:
                    return RequestedAttributes.All();
            }
        }

        private static RequestedAttributes GetRequestedAttributesFromFetch(string? fetchXml, string? logicalName)
        {
            if (string.IsNullOrWhiteSpace(fetchXml))
            {
                return RequestedAttributes.All();
            }

            try
            {
                var doc = XDocument.Parse(fetchXml);
                var entity = GetFetchEntity(doc, logicalName);
                if (entity == null)
                {
                    return RequestedAttributes.All();
                }

                if (entity.Elements().Any(e => e.Name.LocalName == "all-attributes"))
                {
                    return RequestedAttributes.All();
                }

                var attributes = entity.Elements().Where(e => e.Name.LocalName == "attribute").ToList();
                if (attributes.Count == 0)
                {
                    return RequestedAttributes.All();
                }

                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var attr in attributes)
                {
                    var name = attr.Attribute("name")?.Value;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name!);
                    }
                }

                return new RequestedAttributes(names.Count == 0, names);
            }
            catch
            {
                return RequestedAttributes.All();
            }
        }

        private static Entity MergeCachedWithLive(
            string logicalName,
            Guid id,
            Entity cached,
            Entity? live,
            RequestedAttributes requested)
        {
            if (requested.AllAttributes)
            {
                var baseEntity = live != null ? CloneEntity(live, new ColumnSet(true)) : new Entity(logicalName) { Id = id };
                var overlay = CloneEntity(cached, new ColumnSet(true));
                return EntityMergeUtility.Merge(baseEntity, overlay);
            }

            var merged = new Entity(logicalName) { Id = id };
            foreach (var attribute in requested.Attributes)
            {
                if (cached.Attributes.TryGetValue(attribute, out var cachedValue))
                {
                    merged[attribute] = CloneAttributeValue(cachedValue);
                }
                else if (live != null && live.Attributes.TryGetValue(attribute, out var liveValue))
                {
                    merged[attribute] = CloneAttributeValue(liveValue);
                }
            }

            return merged;
        }

        private static bool TryGetIdTargetedIds(QueryBase query, string logicalName, out HashSet<Guid> ids)
        {
            ids = new HashSet<Guid>();
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return false;
            }

            switch (query)
            {
                case QueryExpression qe:
                    return TryGetIdTargetedIds(qe, logicalName, ids);
                case FetchExpression fetch:
                    return TryGetIdTargetedIdsFromFetch(fetch.Query, logicalName, ids);
                default:
                    return false;
            }
        }

        private static bool TryGetIdTargetedIds(QueryExpression query, string logicalName, HashSet<Guid> ids)
        {
            if (query == null)
            {
                return false;
            }

            if (!string.Equals(query.EntityName, logicalName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (query.LinkEntities != null && query.LinkEntities.Count > 0)
            {
                return false;
            }

            var criteria = query.Criteria;
            if (criteria == null)
            {
                return false;
            }

            if (criteria.Filters != null && criteria.Filters.Count > 0)
            {
                return false;
            }

            if (criteria.Conditions == null || criteria.Conditions.Count != 1)
            {
                return false;
            }

            var condition = criteria.Conditions[0];
            var primaryId = logicalName + "id";
            if (!string.Equals(condition.AttributeName, primaryId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            switch (condition.Operator)
            {
                case ConditionOperator.Equal:
                    if (condition.Values == null || condition.Values.Count != 1)
                    {
                        return false;
                    }
                    if (!TryGetGuid(condition.Values[0], out var id))
                    {
                        return false;
                    }
                    ids.Add(id);
                    return true;
                case ConditionOperator.In:
                    if (condition.Values == null || condition.Values.Count == 0)
                    {
                        return false;
                    }
                    foreach (var value in condition.Values)
                    {
                        if (!TryGetGuid(value, out var inId))
                        {
                            return false;
                        }
                        ids.Add(inId);
                    }
                    return ids.Count > 0;
                default:
                    return false;
            }
        }

        private static bool TryGetIdTargetedIdsFromFetch(string? fetchXml, string logicalName, HashSet<Guid> ids)
        {
            if (string.IsNullOrWhiteSpace(fetchXml))
            {
                return false;
            }

            try
            {
                var doc = XDocument.Parse(fetchXml);
                var fetch = doc.Root;
                if (fetch == null || fetch.Name.LocalName != "fetch")
                {
                    return false;
                }

                var aggregate = fetch.Attribute("aggregate")?.Value;
                if (string.Equals(aggregate, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var entity = GetFetchEntity(doc, logicalName);
                if (entity == null)
                {
                    return false;
                }

                if (entity.Elements().Any(e => e.Name.LocalName == "link-entity"))
                {
                    return false;
                }

                var filters = entity.Elements().Where(e => e.Name.LocalName == "filter").ToList();
                if (filters.Count != 1)
                {
                    return false;
                }

                var filter = filters[0];
                if (filter.Elements().Any(e => e.Name.LocalName == "filter"))
                {
                    return false;
                }

                var conditions = filter.Elements().Where(e => e.Name.LocalName == "condition").ToList();
                if (conditions.Count != 1)
                {
                    return false;
                }

                var condition = conditions[0];
                var attributeName = condition.Attribute("attribute")?.Value;
                var primaryId = logicalName + "id";
                if (!string.Equals(attributeName, primaryId, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var op = condition.Attribute("operator")?.Value;
                if (string.Equals(op, "eq", StringComparison.OrdinalIgnoreCase))
                {
                    var value = condition.Attribute("value")?.Value;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        value = condition.Elements().FirstOrDefault(e => e.Name.LocalName == "value")?.Value;
                    }
                    if (!TryParseGuid(value, out var id))
                    {
                        return false;
                    }
                    ids.Add(id);
                    return true;
                }

                if (string.Equals(op, "in", StringComparison.OrdinalIgnoreCase))
                {
                    var values = condition.Elements().Where(e => e.Name.LocalName == "value").Select(e => e.Value).ToList();
                    if (values.Count == 0)
                    {
                        return false;
                    }

                    foreach (var raw in values)
                    {
                        if (!TryParseGuid(raw, out var id))
                        {
                            return false;
                        }
                        ids.Add(id);
                    }
                    return ids.Count > 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static XElement? GetFetchEntity(XDocument doc, string? logicalName)
        {
            var fetch = doc.Root;
            if (fetch == null)
            {
                return null;
            }

            var entity = fetch.Elements().FirstOrDefault(e => e.Name.LocalName == "entity");
            if (entity == null)
            {
                return null;
            }

            var name = entity.Attribute("name")?.Value;
            if (!string.IsNullOrWhiteSpace(logicalName) &&
                !string.Equals(name, logicalName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return entity;
        }

        private static bool TryGetGuid(object? value, out Guid id)
        {
            switch (value)
            {
                case Guid guid:
                    id = guid;
                    return true;
                case string text:
                    return TryParseGuid(text, out id);
                default:
                    id = Guid.Empty;
                    return false;
            }
        }

        private static bool TryParseGuid(string? value, out Guid id)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                id = parsed;
                return true;
            }

            id = Guid.Empty;
            return false;
        }

        private static string BuildKey(string logicalName, Guid id)
        {
            return $"{logicalName}:{id}";
        }

        private static Entity CloneEntity(Entity source, ColumnSet columnSet)
        {
            var clone = new Entity(source.LogicalName) { Id = source.Id };
            if (columnSet == null || columnSet.AllColumns)
            {
                foreach (var attr in source.Attributes)
                {
                    clone[attr.Key] = CloneAttributeValue(attr.Value);
                }

                foreach (var formatted in source.FormattedValues)
                {
                    clone.FormattedValues[formatted.Key] = formatted.Value;
                }

                if (source.KeyAttributes != null && clone.KeyAttributes != null)
                {
                    foreach (var keyAttr in source.KeyAttributes)
                    {
                        clone.KeyAttributes[keyAttr.Key] = CloneAttributeValue(keyAttr.Value);
                    }
                }

                return clone;
            }

            foreach (var column in columnSet.Columns)
            {
                if (source.Attributes.TryGetValue(column, out var value))
                {
                    clone[column] = CloneAttributeValue(value);
                }
            }

            return clone;
        }

        private static object? CloneAttributeValue(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string s:
                    return s;
                case Guid g:
                    return g;
                case bool b:
                    return b;
                case int i:
                    return i;
                case long l:
                    return l;
                case double d:
                    return d;
                case decimal m:
                    return m;
                case DateTime dt:
                    return dt;
                case byte[] bytes:
                    return (byte[])bytes.Clone();
                case Money money:
                    return new Money(money.Value);
                case OptionSetValue osv:
                    return new OptionSetValue(osv.Value);
                case OptionSetValueCollection options:
                    return new OptionSetValueCollection(options.Select(opt => new OptionSetValue(opt.Value)).ToList());
                case EntityReference reference:
                    return new EntityReference(reference.LogicalName, reference.Id)
                    {
                        Name = reference.Name,
                        RowVersion = reference.RowVersion
                    };
                case EntityReferenceCollection references:
                    var clonedRefs = new EntityReferenceCollection();
                    foreach (var item in references)
                    {
                        clonedRefs.Add(new EntityReference(item.LogicalName, item.Id)
                        {
                            Name = item.Name,
                            RowVersion = item.RowVersion
                        });
                    }
                    return clonedRefs;
                case Entity entity:
                    return CloneEntity(entity, new ColumnSet(true));
                case EntityCollection collection:
                    var cloned = new EntityCollection
                    {
                        EntityName = collection.EntityName,
                        MoreRecords = collection.MoreRecords,
                        TotalRecordCount = collection.TotalRecordCount,
                        PagingCookie = collection.PagingCookie,
                        MinActiveRowVersion = collection.MinActiveRowVersion
                    };
                    foreach (var item in collection.Entities)
                    {
                        cloned.Entities.Add(CloneEntity(item, new ColumnSet(true)));
                    }
                    return cloned;
                case AliasedValue aliased:
                    return new AliasedValue(
                        aliased.EntityLogicalName,
                        aliased.AttributeLogicalName,
                        CloneAttributeValue(aliased.Value));
                default:
                    return value;
            }
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

        private static string? ExtractEntityFromFetch(string? fetchXml)
        {
            if (string.IsNullOrWhiteSpace(fetchXml)) return null;
            try
            {
                var doc = XDocument.Parse(fetchXml);
                var entity = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "entity");
                return entity?.Attribute("name")?.Value;
            }
            catch
            {
                return null;
            }
        }

        private readonly struct RequestedAttributes
        {
            public RequestedAttributes(bool allAttributes, HashSet<string> attributes)
            {
                AllAttributes = allAttributes;
                Attributes = attributes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public bool AllAttributes { get; }
            public HashSet<string> Attributes { get; }

            public static RequestedAttributes All()
            {
                return new RequestedAttributes(true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }
    }
}
