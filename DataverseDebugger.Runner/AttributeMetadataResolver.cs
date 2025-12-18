using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Conversion.Utils;
using Microsoft.Xrm.Sdk.Metadata;

namespace DataverseDebugger.Runner
{
    /// <summary>
    /// Resolves attribute metadata for entities, combining the offline metadata cache with
    /// live Web API metadata (cached per environment/entity on disk).
    /// </summary>
    internal sealed class AttributeMetadataResolver
    {
        private readonly MetadataCache? _metadataCache;
        private readonly EnvConfig? _environment;
        private readonly string? _accessToken;
        private readonly HttpClient _httpClient;
        private readonly List<string>? _trace;
        private readonly Dictionary<string, Dictionary<string, AttributeShape>> _localCache = new(StringComparer.OrdinalIgnoreCase);

        public AttributeMetadataResolver(
            MetadataCache? metadataCache,
            EnvConfig? environment,
            string? accessToken,
            HttpClient httpClient,
            List<string>? trace)
        {
            _metadataCache = metadataCache;
            _environment = environment;
            _accessToken = accessToken;
            _httpClient = httpClient;
            _trace = trace;
        }

        public Dictionary<string, AttributeShape>? GetAttributeMap(string logicalName)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return null;
            }

            if (_localCache.TryGetValue(logicalName, out var existing))
            {
                return existing;
            }

            var map = FullEntityMetadataCache.TryGetAttributes(
                logicalName,
                _environment,
                _accessToken,
                _httpClient,
                _trace);

            if (map == null)
            {
                map = BuildFromMetadataCache(logicalName);
            }

            if (map != null)
            {
                _localCache[logicalName] = map;
            }

            return map;
        }

        private Dictionary<string, AttributeShape>? BuildFromMetadataCache(string logicalName)
        {
            var metadata = _metadataCache?.GetEntityMetadataWithAttributes(logicalName);
            if (metadata?.Attributes == null || metadata.Attributes.Length == 0)
            {
                return null;
            }

            var map = new Dictionary<string, AttributeShape>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in metadata.Attributes)
            {
                if (attribute?.LogicalName == null)
                {
                    continue;
                }

                if (!map.ContainsKey(attribute.LogicalName))
                {
                    map[attribute.LogicalName] = AttributeShape.FromMetadata(attribute);
                }
            }

            return map;
        }
    }

    internal sealed class AttributeShape
    {
        public string LogicalName { get; set; } = string.Empty;
        public AttributeTypeCode? AttributeType { get; set; }
        public string? AttributeTypeName { get; set; }

        public static AttributeShape FromMetadata(AttributeMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            return new AttributeShape
            {
                LogicalName = metadata.LogicalName ?? string.Empty,
                AttributeType = metadata.AttributeType,
                AttributeTypeName = metadata.AttributeTypeName?.Value
            };
        }
    }

    internal static class FullEntityMetadataCache
    {
        private sealed class CachedAttribute
        {
            public string LogicalName { get; set; } = string.Empty;
            public string? AttributeType { get; set; }
            public string? AttributeTypeName { get; set; }
        }

        private sealed class CachedEntityMetadata
        {
            public string LogicalName { get; set; } = string.Empty;
            public List<CachedAttribute> Attributes { get; set; } = new List<CachedAttribute>();
            public DateTime CachedOnUtc { get; set; }
            public int Version { get; set; } = 1;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Dictionary<string, AttributeShape>> Memory = new(StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, AttributeShape>? TryGetAttributes(
            string logicalName,
            EnvConfig? environment,
            string? accessToken,
            HttpClient httpClient,
            List<string>? trace)
        {
            var envKey = BuildEnvironmentKey(environment);
            var memoryKey = envKey + ":" + logicalName.ToLowerInvariant();

            lock (Sync)
            {
                if (Memory.TryGetValue(memoryKey, out var cached))
                {
                    return cached;
                }
            }

            var cacheDir = GetCacheDirectory(environment);
            var cacheFile = Path.Combine(cacheDir, logicalName.ToLowerInvariant() + ".json");
            var diskEntry = TryLoadFromDisk(cacheFile, trace);
            if (diskEntry != null)
            {
                var map = ConvertToMap(diskEntry);
                if (map != null)
                {
                    lock (Sync)
                    {
                        Memory[memoryKey] = map;
                    }
                    return map;
                }
            }

            var fetchedEntry = FetchFromWebApi(logicalName, environment, accessToken, httpClient, trace);
            if (fetchedEntry == null)
            {
                return null;
            }

            var fetchedMap = ConvertToMap(fetchedEntry);
            if (fetchedMap == null)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(cacheDir);
                var json = JsonSerializer.Serialize(fetchedEntry);
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                trace?.Add($"Metadata cache write failed for {logicalName}: {ex.Message}");
            }

            lock (Sync)
            {
                Memory[memoryKey] = fetchedMap;
            }

            return fetchedMap;
        }

        private static CachedEntityMetadata? TryLoadFromDisk(string filePath, List<string>? trace)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<CachedEntityMetadata>(json);
            }
            catch (Exception ex)
            {
                trace?.Add($"Metadata cache read failed ({Path.GetFileName(filePath)}): {ex.Message}");
                return null;
            }
        }

        private static CachedEntityMetadata? FetchFromWebApi(
            string logicalName,
            EnvConfig? environment,
            string? accessToken,
            HttpClient httpClient,
            List<string>? trace)
        {
            if (environment == null || string.IsNullOrWhiteSpace(environment.OrgUrl))
            {
                trace?.Add("Metadata fetch skipped: environment URL not set.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                trace?.Add("Metadata fetch skipped: access token not provided.");
                return null;
            }

            var escapedName = logicalName.Replace("'", "''");
            var url = environment.OrgUrl.TrimEnd('/') + "/api/data/v9.0/EntityDefinitions(LogicalName='" + escapedName + "')" +
                      "?$select=LogicalName&$expand=Attributes($select=LogicalName,AttributeType,AttributeTypeName)";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    trace?.Add($"Metadata fetch failed for {logicalName}: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return null;
                }

                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Attributes", out var attributes) || attributes.ValueKind != JsonValueKind.Array)
                {
                    trace?.Add($"Metadata fetch failed for {logicalName}: Attributes missing in response.");
                    return null;
                }

                var cached = new CachedEntityMetadata
                {
                    LogicalName = logicalName,
                    CachedOnUtc = DateTime.UtcNow,
                    Attributes = new List<CachedAttribute>()
                };

                foreach (var attr in attributes.EnumerateArray())
                {
                    if (!attr.TryGetProperty("LogicalName", out var nameProp))
                    {
                        continue;
                    }

                    var attrName = nameProp.GetString();
                    if (string.IsNullOrWhiteSpace(attrName))
                    {
                        continue;
                    }
                    var safeAttrName = attrName!;

                    string? attributeType = null;
                    if (attr.TryGetProperty("AttributeType", out var typeProp))
                    {
                        attributeType = typeProp.GetString();
                    }

                    string? attributeTypeName = null;
                    if (attr.TryGetProperty("AttributeTypeName", out var typeNameProp) &&
                        typeNameProp.ValueKind == JsonValueKind.Object &&
                        typeNameProp.TryGetProperty("Value", out var valueProp))
                    {
                        attributeTypeName = valueProp.GetString();
                    }

                    cached.Attributes.Add(new CachedAttribute
                    {
                        LogicalName = safeAttrName,
                        AttributeType = attributeType,
                        AttributeTypeName = attributeTypeName
                    });
                }

                trace?.Add($"Fetched metadata for {logicalName} ({cached.Attributes.Count} attributes).");
                return cached;
            }
            catch (Exception ex)
            {
                trace?.Add($"Metadata fetch failed for {logicalName}: {ex.Message}");
                return null;
            }
        }

        private static Dictionary<string, AttributeShape>? ConvertToMap(CachedEntityMetadata? cached)
        {
            if (cached?.Attributes == null || cached.Attributes.Count == 0)
            {
                return null;
            }

            return cached.Attributes
                .Where(a => !string.IsNullOrWhiteSpace(a.LogicalName))
                .GroupBy(a => a.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => ToShape(g.First()), StringComparer.OrdinalIgnoreCase);
        }

        private static AttributeShape ToShape(CachedAttribute cached)
        {
            AttributeTypeCode? type = null;
            if (!string.IsNullOrWhiteSpace(cached.AttributeType))
            {
                if (Enum.TryParse<AttributeTypeCode>(cached.AttributeType, ignoreCase: true, out var parsed))
                {
                    type = parsed;
                }
            }

            return new AttributeShape
            {
                LogicalName = cached.LogicalName,
                AttributeType = type,
                AttributeTypeName = cached.AttributeTypeName
            };
        }

        private static string GetCacheDirectory(EnvConfig? environment)
        {
            var root = environment?.EntityMetadataCacheRoot;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(AppContext.BaseDirectory, "envcache");
                var safeName = SafeName(environment?.Name ?? environment?.OrgUrl ?? "default");
                return Path.Combine(root, safeName, "entityMetadata");
            }

            try
            {
                Directory.CreateDirectory(root);
            }
            catch
            {
                // best effort
            }

            return Path.Combine(root, "entityMetadata");
        }

        private static string BuildEnvironmentKey(EnvConfig? environment)
        {
            var name = environment?.OrgUrl ?? environment?.Name ?? "default";
            return name.ToLowerInvariant();
        }

        private static string SafeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var parts = name.Split(invalid, StringSplitOptions.RemoveEmptyEntries);
            var safe = string.Join("_", parts);
            return string.IsNullOrWhiteSpace(safe) ? "env" : safe;
        }
    }
}
