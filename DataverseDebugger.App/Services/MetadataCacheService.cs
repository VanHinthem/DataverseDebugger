using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using DataverseDebugger.App.Models;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Result of a metadata cache operation.
    /// </summary>
    public sealed class MetadataCacheResult
    {
        /// <summary>Gets or sets the path to the cached metadata file.</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>Gets or sets whether metadata was fetched (vs cached).</summary>
        public bool Fetched { get; set; }
        /// <summary>Gets or sets when the metadata was last updated.</summary>
        public DateTimeOffset LastUpdatedUtc { get; set; }
        /// <summary>Gets or sets the file size in bytes.</summary>
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Service for fetching and caching OData metadata ($metadata) from Dataverse.
    /// </summary>
    /// <remarks>
    /// Metadata is cached per environment and used for OData URI parsing and
    /// entity-set to logical-name mapping.
    /// </remarks>
    public static class MetadataCacheService
    {
        private const string WebApiVersion = "v9.2";
        private static readonly HttpClient Http = CreateHttpClient();
        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            return client;
        }
        private static readonly object LookupLock = new object();
        private static readonly Dictionary<string, HashSet<string>> LookupCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the metadata cache file path for an environment.
        /// </summary>
        public static string GetMetadataPath(EnvironmentProfile profile)
        {
            var root = EnvironmentPathService.GetEnvironmentCacheRoot(profile);
            return Path.Combine(root, "metadata.xml");
        }

        /// <summary>
        /// Gets the cached operation (custom API/action) metadata path for an environment.
        /// </summary>
        public static string GetOperationParametersPath(EnvironmentProfile profile)
        {
            var root = EnvironmentPathService.GetEnvironmentCacheRoot(profile);
            return Path.Combine(root, "operationparameters.json");
        }

        public static async Task<MetadataCacheResult> EnsureMetadataAsync(EnvironmentProfile profile, string accessToken, bool force = false)
        {
            var path = GetMetadataPath(profile);
            if (!force && File.Exists(path))
            {
                await EnsureOperationSnapshotAsync(profile, accessToken, force).ConfigureAwait(false);
                var info = new FileInfo(path);
                return new MetadataCacheResult
                {
                    Path = path,
                    Fetched = false,
                    LastUpdatedUtc = info.LastWriteTimeUtc,
                    SizeBytes = info.Length
                };
            }

            return await RefreshMetadataAsync(profile, accessToken).ConfigureAwait(false);
        }

        public static async Task<MetadataCacheResult> RefreshMetadataAsync(EnvironmentProfile profile, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(profile.OrgUrl))
            {
                throw new InvalidOperationException("Org URL is required to fetch metadata.");
            }

            var url = BuildWebApiUrl(profile.OrgUrl, "$metadata");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.ParseAdd("application/xml");

            var response = await Http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            var path = GetMetadataPath(profile);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory);
            await File.WriteAllBytesAsync(path, content).ConfigureAwait(false);

            await RefreshOperationParameterMetadataAsync(profile, accessToken).ConfigureAwait(false);

            return new MetadataCacheResult
            {
                Path = path,
                Fetched = true,
                LastUpdatedUtc = DateTimeOffset.UtcNow,
                SizeBytes = content.LongLength
            };
        }

        private static async Task EnsureOperationSnapshotAsync(EnvironmentProfile profile, string accessToken, bool force)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return;
            }

            try
            {
                var path = GetOperationParametersPath(profile);
                if (!force && File.Exists(path))
                {
                    var info = new FileInfo(path);
                    if ((DateTimeOffset.UtcNow - info.LastWriteTimeUtc).TotalDays <= 7)
                    {
                        return;
                    }
                }

                await RefreshOperationParameterMetadataAsync(profile, accessToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogService.Append($"[MetadataCacheService] Operation snapshot ensure failed: {ex.Message}");
            }
        }

        public static async Task RefreshOperationParameterMetadataAsync(EnvironmentProfile profile, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(profile.OrgUrl))
            {
                return;
            }

            try
            {
                LogService.Append($"[MetadataCacheService] Operation snapshot refresh started for {profile.Name}.");
                var snapshot = await DownloadOperationParameterSnapshotAsync(profile.OrgUrl, accessToken).ConfigureAwait(false);
                if (snapshot == null)
                {
                    LogService.Append("[MetadataCacheService] Operation snapshot download returned null; skipping write.");
                    return;
                }

                var path = GetOperationParametersPath(profile);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory);
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
                LogService.Append($"[MetadataCacheService] Operation snapshot written to {path} ({snapshot.Parameters.Count} parameters).");
            }
            catch (Exception ex)
            {
                LogService.Append($"[MetadataCacheService] Operation snapshot refresh failed: {ex}");
            }
        }

        private static async Task<OperationParameterSnapshot?> DownloadOperationParameterSnapshotAsync(string orgUrl, string accessToken)
        {
            var snapshot = new OperationParameterSnapshot
            {
                GeneratedOnUtc = DateTimeOffset.UtcNow
            };

            var customApis = await DownloadCustomApiParametersAsync(orgUrl, accessToken).ConfigureAwait(false);
            if (customApis != null)
            {
                snapshot.Parameters.AddRange(customApis);
                foreach (var parameter in customApis)
                {
                    RecordOperationSource(snapshot, parameter.OperationName, OperationParameterSource.CustomApi);
                }
                LogService.Append($"[MetadataCacheService] Downloaded {customApis.Count} custom API parameters.");
            }

            var customActions = await DownloadCustomActionParametersAsync(orgUrl, accessToken).ConfigureAwait(false);
            if (customActions != null)
            {
                snapshot.Parameters.AddRange(customActions);
                foreach (var parameter in customActions)
                {
                    RecordOperationSource(snapshot, parameter.OperationName, OperationParameterSource.CustomAction);
                }
                LogService.Append($"[MetadataCacheService] Downloaded {customActions.Count} custom action parameters.");
            }

            var customApiNames = await DownloadCustomApiOperationNamesAsync(orgUrl, accessToken).ConfigureAwait(false);
            if (customApiNames != null)
            {
                foreach (var name in customApiNames)
                {
                    RecordOperationSource(snapshot, name, OperationParameterSource.CustomApi);
                }
            }

            var customActionNames = await DownloadCustomActionOperationNamesAsync(orgUrl, accessToken).ConfigureAwait(false);
            if (customActionNames != null)
            {
                foreach (var name in customActionNames)
                {
                    RecordOperationSource(snapshot, name, OperationParameterSource.CustomAction);
                }
            }

            return snapshot;
        }

        private static async Task<List<OperationParameterSnapshotItem>?> DownloadCustomApiParametersAsync(string orgUrl, string accessToken)
        {
            try
            {
                var items = new List<OperationParameterSnapshotItem>();
                var nextLink = BuildWebApiUrl(orgUrl, "customapirequestparameters?$select=uniquename,name,type,isoptional&$expand=CustomAPIId($select=customapiid,uniquename)");

                while (!string.IsNullOrWhiteSpace(nextLink))
                {
                    using var document = await SendOperationRequestAsync(nextLink, accessToken).ConfigureAwait(false);
                    if (document == null)
                    {
                        break;
                    }

                    if (document.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in value.EnumerateArray())
                        {
                            var parameter = ParseCustomApiParameter(element);
                            if (parameter != null)
                            {
                                items.Add(parameter);
                            }
                        }
                    }

                    nextLink = document.RootElement.TryGetProperty("@odata.nextLink", out var next)
                        ? next.GetString()
                        : null;
                }

                LogService.Append($"[MetadataCacheService] Custom API parameter download complete. Total={items.Count}.");
                return items;
            }
            catch (Exception ex)
            {
                LogService.Append($"[MetadataCacheService] Custom API parameter download failed: {ex.Message}");
                return null;
            }
        }

        private static async Task<List<OperationParameterSnapshotItem>?> DownloadCustomActionParametersAsync(string orgUrl, string accessToken)
        {
                        try
                        {
                                var items = new List<OperationParameterSnapshotItem>();
                                var fetchXml = """
<fetch distinct="true">
    <entity name="sdkmessagerequestfield">
        <attribute name="name" />
        <attribute name="parameterbindinginformation" />
        <attribute name="optional" />
        <attribute name="position" />
        <attribute name="parser" />
        <order attribute="position" />
        <link-entity name="sdkmessagerequest" from="sdkmessagerequestid" to="sdkmessagerequestid">
            <link-entity name="sdkmessagepair" from="sdkmessagepairid" to="sdkmessagepairid">
                <link-entity name="sdkmessage" from="sdkmessageid" to="sdkmessageid" alias="sdkmessage">
                    <attribute name="name" />
                </link-entity>
            </link-entity>
        </link-entity>
    </entity>
</fetch>
""";

                var encodedFetch = Uri.EscapeDataString(fetchXml);
                var nextLink = BuildWebApiUrl(orgUrl, "sdkmessagerequestfields?fetchXml=" + encodedFetch);

                while (!string.IsNullOrWhiteSpace(nextLink))
                {
                    using var document = await SendOperationRequestAsync(nextLink, accessToken).ConfigureAwait(false);
                    if (document == null)
                    {
                        break;
                    }

                    if (document.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in value.EnumerateArray())
                        {
                            var parameter = ParseCustomActionParameter(element);
                            if (parameter != null)
                            {
                                items.Add(parameter);
                            }
                        }
                    }

                    nextLink = document.RootElement.TryGetProperty("@odata.nextLink", out var next)
                        ? next.GetString()
                        : null;
                }

                LogService.Append($"[MetadataCacheService] Custom action parameter download complete. Total={items.Count}.");
                return items;
            }
            catch (Exception ex)
            {
                LogService.Append($"[MetadataCacheService] Custom action parameter download failed: {ex.Message}");
                return null;
            }
        }

        private static OperationParameterSnapshotItem? ParseCustomApiParameter(JsonElement element)
        {
            if (!element.TryGetProperty("uniquename", out var uniqueNameProp) || uniqueNameProp.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            if (!TryGetCustomApiNavigation(element, out var apiProp))
            {
                return null;
            }

            if (!apiProp.TryGetProperty("uniquename", out var apiNameProp) || apiNameProp.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var parameter = new OperationParameterSnapshotItem
            {
                Source = OperationParameterSource.CustomApi,
                OperationName = apiNameProp.GetString() ?? string.Empty,
                PrimaryParameterName = uniqueNameProp.GetString() ?? string.Empty,
                AlternateParameterName = element.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                    ? nameProp.GetString()
                    : null,
                EntityLogicalName = element.TryGetProperty("entitylogicalname", out var entityProp) && entityProp.ValueKind == JsonValueKind.String
                    ? entityProp.GetString()
                    : null,
                LogicalEntityName = element.TryGetProperty("logicalentityname", out var logicalProp) && logicalProp.ValueKind == JsonValueKind.String
                    ? logicalProp.GetString()
                    : null
            };

            if (element.TryGetProperty("isoptional", out var optionalProp) && optionalProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                parameter.IsOptional = optionalProp.GetBoolean();
            }

            if (element.TryGetProperty("position", out var positionProp) && positionProp.ValueKind == JsonValueKind.Number && positionProp.TryGetInt32(out var positionValue))
            {
                parameter.Position = positionValue;
            }

            if (element.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.Number && typeProp.TryGetInt32(out var typeValue))
            {
                parameter.Type = typeValue;
            }

            if (string.IsNullOrWhiteSpace(parameter.OperationName) || string.IsNullOrWhiteSpace(parameter.PrimaryParameterName))
            {
                return null;
            }

            return parameter;
        }

        private static bool TryGetCustomApiNavigation(JsonElement element, out JsonElement apiProp)
        {
            if (element.TryGetProperty("CustomAPIId", out apiProp) && apiProp.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            if (element.TryGetProperty("customapiid", out apiProp) && apiProp.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            apiProp = default;
            return false;
        }

        private static OperationParameterSnapshotItem? ParseCustomActionParameter(JsonElement element)
        {
            var operationName = GetStringProperty(element, "sdkmessage.name");
            var parameterName = GetStringProperty(element, "name");

            if (string.IsNullOrWhiteSpace(operationName) || string.IsNullOrWhiteSpace(parameterName))
            {
                return null;
            }

            var parser = GetStringProperty(element, "parser");
            var typeFormatted = GetStringProperty(element, "parameter.type@OData.Community.Display.V1.FormattedValue")
                ?? GetStringProperty(element, "type@OData.Community.Display.V1.FormattedValue");

            var parameter = new OperationParameterSnapshotItem
            {
                Source = OperationParameterSource.CustomAction,
                OperationName = operationName!,
                PrimaryParameterName = parameterName!,
                AlternateParameterName = GetStringProperty(element, "parameter.publicname") ?? GetStringProperty(element, "publicname"),
                EntityLogicalName = GetStringProperty(element, "parameter.logicalentityname") ?? GetStringProperty(element, "logicalentityname"),
                LogicalEntityName = GetStringProperty(element, "parameter.logicalentityname") ?? GetStringProperty(element, "logicalentityname"),
                BindingInformation = GetStringProperty(element, "parameterbindinginformation"),
                Parser = parser
            };

            var isOptional = GetBooleanProperty(element, "optional");
            if (isOptional.HasValue)
            {
                parameter.IsOptional = isOptional;
            }

            var position = GetIntProperty(element, "position");
            if (position.HasValue)
            {
                parameter.Position = position;
            }

            var typeFromLabel = OperationParameterTypeMapper.FromFormattedActionType(typeFormatted);
            if (typeFromLabel.HasValue)
            {
                parameter.Type = typeFromLabel;
            }
            else
            {
                var typeOption = GetIntProperty(element, "parameter.type") ?? GetIntProperty(element, "type");
                if (typeOption.HasValue)
                {
                    parameter.Type = typeOption;
                }
            }

            var parserType = OperationParameterTypeMapper.FromParser(parser);
            if (parserType.HasValue)
            {
                parameter.Type = parserType;
            }

            if (!string.IsNullOrWhiteSpace(parameter.BindingInformation))
            {
                var binding = parameter.BindingInformation;
                if (binding.StartsWith("Bound", StringComparison.OrdinalIgnoreCase) || binding.StartsWith("Bounded", StringComparison.OrdinalIgnoreCase))
                {
                    parameter.Type = OperationParameterType.EntityReference;
                    parameter.PrimaryParameterName = "Target";
                }
                else if (binding.Contains(":", StringComparison.Ordinal))
                {
                    parameter.Type = OperationParameterType.EntityReference;
                    parameter.PrimaryParameterName = "Target";
                }
            }
            else if (parameter.Position == 0 && string.Equals(parameter.PrimaryParameterName, "Target", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Type = OperationParameterType.EntityReference;
            }

            return parameter;
        }

        private static int? GetIntProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            return null;
        }

        private static bool? GetBooleanProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return property.GetBoolean();
            }

            return null;
        }

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            return null;
        }

        private static async Task<List<string>?> DownloadCustomApiOperationNamesAsync(string orgUrl, string accessToken)
        {
            try
            {
                var results = new List<string>();
                var nextLink = BuildWebApiUrl(orgUrl, "customapis?$select=uniquename");

                while (!string.IsNullOrWhiteSpace(nextLink))
                {
                    using var document = await SendOperationRequestAsync(nextLink, accessToken).ConfigureAwait(false);
                    if (document == null)
                    {
                        break;
                    }

                    if (document.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in value.EnumerateArray())
                        {
                            if (element.TryGetProperty("uniquename", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                            {
                                var name = nameProp.GetString();
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    results.Add(name);
                                }
                            }
                        }
                    }

                    nextLink = document.RootElement.TryGetProperty("@odata.nextLink", out var next)
                        ? next.GetString()
                        : null;
                }

                LogService.Append($"[MetadataCacheService] Custom API list download complete. Total={results.Count}.");
                return results;
            }
            catch (Exception ex)
            {
                LogService.Append($"[MetadataCacheService] Custom API list download failed: {ex.Message}");
                return null;
            }
        }

        private static async Task<List<string>?> DownloadCustomActionOperationNamesAsync(string orgUrl, string accessToken)
        {
            try
            {
                var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var nextLink = BuildWebApiUrl(orgUrl, "workflows?$select=name,uniquename&$filter=category eq 3");

                while (!string.IsNullOrWhiteSpace(nextLink))
                {
                    using var document = await SendOperationRequestAsync(nextLink, accessToken).ConfigureAwait(false);
                    if (document == null)
                    {
                        break;
                    }

                    if (document.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in value.EnumerateArray())
                        {
                            var uniqueName = element.TryGetProperty("uniquename", out var uniqueProp) && uniqueProp.ValueKind == JsonValueKind.String
                                ? uniqueProp.GetString()
                                : null;
                            if (!string.IsNullOrWhiteSpace(uniqueName))
                            {
                                results.Add(uniqueName);
                                continue;
                            }

                            var name = element.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                                ? nameProp.GetString()
                                : null;
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                results.Add(name);
                            }
                        }
                    }

                    nextLink = document.RootElement.TryGetProperty("@odata.nextLink", out var next)
                        ? next.GetString()
                        : null;
                }

                LogService.Append($"[MetadataCacheService] Custom action list download complete. Total={results.Count}.");
                return results.ToList();
            }
            catch (Exception ex)
            {
                LogService.Append($"[MetadataCacheService] Custom action list download failed: {ex.Message}");
                return null;
            }
        }

        private static void RecordOperationSource(OperationParameterSnapshot snapshot, string? operationName, OperationParameterSource source)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(operationName))
            {
                return;
            }

            if (snapshot.OperationSources.Any(hint => string.Equals(hint.OperationName, operationName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            snapshot.OperationSources.Add(new OperationSourceSnapshotItem
            {
                OperationName = operationName,
                Source = source
            });
        }

        private static async Task<JsonDocument?> SendOperationRequestAsync(string url, string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Add("OData-MaxVersion", "4.0");
            request.Headers.Add("OData-Version", "4.0");
            request.Headers.Add("Prefer", "odata.maxpagesize=500");
            request.Headers.Add("Prefer", "odata.include-annotations=\"*\"");

            var response = await Http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LogService.Append($"[MetadataCacheService] Request to {url} failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body={content}");
                response.EnsureSuccessStatusCode();
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        }

        private static string BuildWebApiUrl(string orgUrl, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(orgUrl))
            {
                throw new ArgumentException("Org URL is required", nameof(orgUrl));
            }

            var trimmed = (relativePath ?? string.Empty).TrimStart('/');
            return $"{orgUrl.TrimEnd('/')}/api/data/{WebApiVersion}/{trimmed}";
        }

        public static System.Collections.Generic.Dictionary<string, string> LoadEntitySetMap(EnvironmentProfile profile)
        {
            var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var path = GetMetadataPath(profile);
            if (!File.Exists(path)) return result;
            try
            {
                var text = File.ReadAllText(path);
                var regex = new Regex(@"EntitySet\s+Name\s*=\s*""(?<set>[^""]+)""\s+EntityType\s*=\s*""[^""]*\.?(?<logical>[^"".>]+)""", RegexOptions.IgnoreCase);
                foreach (Match m in regex.Matches(text))
                {
                    var set = m.Groups["set"]?.Value;
                    var logical = m.Groups["logical"]?.Value;
                    if (!string.IsNullOrWhiteSpace(set) && !string.IsNullOrWhiteSpace(logical))
                    {
                        result[set] = logical;
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }
            return result;
        }

        public static HashSet<string> LoadLookupAttributes(string? metadataPath, string logicalName)
        {
            if (string.IsNullOrWhiteSpace(metadataPath) || string.IsNullOrWhiteSpace(logicalName))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!File.Exists(metadataPath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var key = metadataPath + "|" + logicalName;
            lock (LookupLock)
            {
                if (LookupCache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }

            var lookups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var settings = new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    DtdProcessing = DtdProcessing.Prohibit
                };

                using var stream = File.OpenRead(metadataPath);
                using var reader = XmlReader.Create(stream, settings);
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "EntityType")
                    {
                        continue;
                    }

                    var name = reader.GetAttribute("Name");
                    if (!string.Equals(name, logicalName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (reader.IsEmptyElement)
                    {
                        break;
                    }

                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "EntityType")
                        {
                            break;
                        }

                        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "NavigationProperty")
                        {
                            continue;
                        }

                        var navName = reader.GetAttribute("Name");
                        var navType = reader.GetAttribute("Type");
                        if (string.IsNullOrWhiteSpace(navName))
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(navType) &&
                            navType.StartsWith("Collection(", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        lookups.Add(navName);
                    }

                    break;
                }
            }
            catch
            {
                // ignore parse errors
            }

            lock (LookupLock)
            {
                LookupCache[key] = lookups;
            }

            return lookups;
        }

    }
}
