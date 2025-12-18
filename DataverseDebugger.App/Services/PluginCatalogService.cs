using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Container for the plugin catalog data fetched from Dataverse.
    /// </summary>
    public sealed class PluginCatalog
    {
        /// <summary>Gets or sets the plugin assemblies.</summary>
        public List<PluginAssemblyItem> Assemblies { get; set; } = new();
        /// <summary>Gets or sets the plugin types.</summary>
        public List<PluginTypeItem> Types { get; set; } = new();
        /// <summary>Gets or sets the plugin steps.</summary>
        public List<PluginStepItem> Steps { get; set; } = new();
        /// <summary>Gets or sets the plugin images.</summary>
        public List<PluginImageItem> Images { get; set; } = new();
        /// <summary>Gets or sets when the catalog was fetched.</summary>
        public DateTimeOffset FetchedOnUtc { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>Gets or sets whether the catalog is filtered to selected assemblies.</summary>
        public bool IsFiltered { get; set; }
    }

    /// <summary>Represents a plugin assembly registration.</summary>
    public sealed class PluginAssemblyItem
    {
        /// <summary>Gets or sets the assembly ID.</summary>
        public Guid Id { get; set; }
        /// <summary>Gets or sets the assembly name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the assembly version.</summary>
        public string? Version { get; set; }
        /// <summary>Gets or sets the culture.</summary>
        public string? Culture { get; set; }
        /// <summary>Gets or sets the public key token.</summary>
        public string? PublicKeyToken { get; set; }
        /// <summary>Gets or sets the isolation mode.</summary>
        public int IsolationMode { get; set; }
        /// <summary>Gets or sets whether the assembly is managed.</summary>
        public bool IsManaged { get; set; }
    }

    /// <summary>Represents a plugin type registration.</summary>
    public sealed class PluginTypeItem
    {
        /// <summary>Gets or sets the plugin type ID.</summary>
        public Guid Id { get; set; }
        /// <summary>Gets or sets the friendly name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the full type name.</summary>
        public string TypeName { get; set; } = string.Empty;
        /// <summary>Gets or sets the containing assembly name.</summary>
        public string AssemblyName { get; set; } = string.Empty;
        /// <summary>Gets or sets the containing assembly ID.</summary>
        public Guid AssemblyId { get; set; }
    }

    /// <summary>Represents a plugin step registration.</summary>
    public sealed class PluginStepItem
    {
        /// <summary>Gets or sets the step ID.</summary>
        public Guid Id { get; set; }
        /// <summary>Gets or sets the step name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the plugin type ID.</summary>
        public Guid PluginTypeId { get; set; }
        /// <summary>Gets or sets the assembly ID.</summary>
        public Guid AssemblyId { get; set; }
        /// <summary>Gets or sets the message name.</summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>Gets or sets the primary entity.</summary>
        public string PrimaryEntity { get; set; } = string.Empty;
        /// <summary>Gets or sets the pipeline stage.</summary>
        public int Stage { get; set; }
        /// <summary>Gets or sets the execution mode (sync/async).</summary>
        public int Mode { get; set; }
        /// <summary>Gets or sets the filtering attributes.</summary>
        public string? FilteringAttributes { get; set; }
        /// <summary>Gets or sets the execution rank.</summary>
        public int Rank { get; set; }
        /// <summary>Gets or sets the supported deployment.</summary>
        public int SupportedDeployment { get; set; }
        /// <summary>Gets or sets whether async jobs auto-delete.</summary>
        public bool AsyncAutoDelete { get; set; }
        /// <summary>Gets or sets the unsecure configuration.</summary>
        public string? UnsecureConfiguration { get; set; }
        /// <summary>Gets or sets the secure configuration.</summary>
        public string? SecureConfiguration { get; set; }
    }

    /// <summary>Represents a plugin image configuration.</summary>
    public sealed class PluginImageItem
    {
        /// <summary>Gets or sets the image ID.</summary>
        public Guid Id { get; set; }
        /// <summary>Gets or sets the parent step ID.</summary>
        public Guid StepId { get; set; }
        /// <summary>Gets or sets the image type (PreImage/PostImage).</summary>
        public string ImageType { get; set; } = string.Empty;
        /// <summary>Gets or sets the entity alias.</summary>
        public string EntityAlias { get; set; } = string.Empty;
        /// <summary>Gets or sets the selected attributes.</summary>
        public string? Attributes { get; set; }
    }

    /// <summary>
    /// Service for fetching and caching plugin registration data from Dataverse.
    /// </summary>
    public static class PluginCatalogService
    {
        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            return client;
        }

        /// <summary>
        /// Gets the catalog cache file path for an environment.
        /// </summary>
        public static string GetCatalogPath(EnvironmentProfile profile)
        {
            var root = EnvironmentPathService.GetEnvironmentCacheRoot(profile);
            return Path.Combine(root, "plugin-catalog.json");
        }

        public static async Task<PluginCatalog?> LoadCatalogAsync(EnvironmentProfile profile)
        {
            var path = GetCatalogPath(profile);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<PluginCatalog>(json);
        }

        public static async Task<PluginCatalog> RefreshCatalogAsync(EnvironmentProfile profile, string accessToken)
            => await RefreshCatalogAsync(profile, accessToken, null).ConfigureAwait(false);

        public static async Task<PluginCatalog> RefreshCatalogAsync(EnvironmentProfile profile, string accessToken, IEnumerable<string>? selectedAssemblyPaths)
        {
            if (string.IsNullOrWhiteSpace(profile.OrgUrl))
            {
                throw new InvalidOperationException("Org URL is required to fetch plugin catalog.");
            }

            var selectedNames = new HashSet<string>(
                selectedAssemblyPaths?
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            LogService.Append($"Catalog fetch started for {profile.Name}. Assembly filter: {(selectedNames.Count == 0 ? "(none)" : string.Join(", ", selectedNames))}");

            var catalog = new PluginCatalog();
            catalog.Assemblies = await FetchAssembliesAsync(profile, accessToken).ConfigureAwait(false);
            catalog.Types = await FetchTypesAsync(profile, accessToken).ConfigureAwait(false);
            catalog.Steps = await FetchStepsAsync(profile, accessToken).ConfigureAwait(false);

            catalog.Images = await FetchImagesAsync(profile, accessToken).ConfigureAwait(false);
            catalog.FetchedOnUtc = DateTimeOffset.UtcNow;
            catalog.IsFiltered = selectedAssemblyPaths != null && selectedNames.Count > 0;

            var path = GetCatalogPath(profile);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory);
            var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);

            return catalog;
        }

        private static async Task<List<JsonElement>> FetchPagedAsync(string url, string token)
        {
            var items = new List<JsonElement>();
            var next = url;
            while (!string.IsNullOrWhiteSpace(next))
            {
                using var json = await GetJsonAsync(next, token).ConfigureAwait(false);
                if (json.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in value.EnumerateArray())
                    {
                        items.Add(element.Clone()); // clone so we can dispose the document
                    }
                }

                if (json.RootElement.TryGetProperty("@odata.nextLink", out var nextLink) && nextLink.ValueKind == JsonValueKind.String)
                {
                    next = nextLink.GetString();
                }
                else
                {
                    next = null;
                }
            }

            return items;
        }

        private static async Task<List<PluginAssemblyItem>> FetchAssembliesAsync(EnvironmentProfile profile, string token)
        {
            var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/pluginassemblies?$select=pluginassemblyid,name,version,culture,publickeytoken,isolationmode,ismanaged";
            var raw = await FetchPagedAsync(url, token).ConfigureAwait(false);
            var list = new List<PluginAssemblyItem>();
            foreach (var item in raw)
            {
                list.Add(new PluginAssemblyItem
                {
                    Id = item.GetPropertyGuid("pluginassemblyid"),
                    Name = item.GetPropertyString("name"),
                    Version = item.GetPropertyString("version"),
                    Culture = item.GetPropertyString("culture"),
                    PublicKeyToken = item.GetPropertyString("publickeytoken"),
                    IsolationMode = item.TryGetInt("isolationmode"),
                    IsManaged = item.TryGetBool("ismanaged")
                });
            }
            return list;
        }

        private static async Task<List<PluginTypeItem>> FetchTypesAsync(EnvironmentProfile profile, string token)
        {
            var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/plugintypes?$select=plugintypeid,name,typename,assemblyname,_pluginassemblyid_value&$expand=pluginassemblyid($select=name,pluginassemblyid)";
            var raw = await FetchPagedAsync(url, token).ConfigureAwait(false);

            var list = new List<PluginTypeItem>();
            foreach (var item in raw)
            {
                list.Add(new PluginTypeItem
                {
                    Id = item.GetPropertyGuid("plugintypeid"),
                    Name = item.GetPropertyString("name"),
                    TypeName = item.GetPropertyString("typename"),
                    AssemblyName = item.TryGetNestedString("pluginassemblyid", "name"),
                    AssemblyId = item.TryGetNestedGuid("pluginassemblyid", "pluginassemblyid")
                });
            }
            return list;
        }

        private static async Task<List<PluginStepItem>> FetchStepsAsync(EnvironmentProfile profile, string token)
        {
            // Pull steps with plugintypeid and message/filter info. AssemblyId is inferred from the expanded plugintypeid.
            var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,stage,mode,rank,filteringattributes,supporteddeployment,asyncautodelete,configuration,_plugintypeid_value&$expand=plugintypeid($select=plugintypeid,_pluginassemblyid_value),sdkmessageid($select=name),sdkmessagefilterid($select=primaryobjecttypecode,secondaryobjecttypecode),sdkmessageprocessingstepsecureconfigid($select=secureconfig)";
            List<JsonElement> raw;
            try
            {
                raw = await FetchPagedAsync(url, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogService.Append($"Catalog step fetch: secure config expansion failed; retrying without secure config. {ex.Message}");
                var fallbackUrl = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,stage,mode,rank,filteringattributes,supporteddeployment,asyncautodelete,configuration,_plugintypeid_value&$expand=plugintypeid($select=plugintypeid,_pluginassemblyid_value),sdkmessageid($select=name),sdkmessagefilterid($select=primaryobjecttypecode,secondaryobjecttypecode)";
                raw = await FetchPagedAsync(fallbackUrl, token).ConfigureAwait(false);
            }

            var list = new List<PluginStepItem>();
            foreach (var item in raw)
            {
                var typeId = item.GetPropertyGuid("_plugintypeid_value");
                if (typeId == Guid.Empty)
                {
                    typeId = item.TryGetNestedGuid("plugintypeid", "plugintypeid");
                }
                // ignore secondary entity steps (mimic emulator skip)
                var secondary = item.TryGetNestedString("sdkmessagefilterid", "secondaryobjecttypecode");
                if (!string.IsNullOrWhiteSpace(secondary) && !secondary.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var unsecureConfig = item.GetPropertyString("configuration");
                var secureConfig = item.TryGetNestedString("sdkmessageprocessingstepsecureconfigid", "secureconfig");

                list.Add(new PluginStepItem
                {
                    Id = item.GetPropertyGuid("sdkmessageprocessingstepid"),
                    Name = item.GetPropertyString("name"),
                    PluginTypeId = typeId,
                    AssemblyId = item.TryGetNestedGuid("plugintypeid", "_pluginassemblyid_value"),
                    Message = item.TryGetNestedString("sdkmessageid", "name"),
                    PrimaryEntity = item.TryGetNestedString("sdkmessagefilterid", "primaryobjecttypecode"),
                    Stage = item.TryGetInt("stage"),
                    Mode = item.TryGetInt("mode"),
                    FilteringAttributes = item.GetPropertyString("filteringattributes"),
                    Rank = item.TryGetInt("rank"),
                    SupportedDeployment = item.TryGetInt("supporteddeployment"),
                    AsyncAutoDelete = item.TryGetBool("asyncautodelete"),
                    UnsecureConfiguration = string.IsNullOrWhiteSpace(unsecureConfig) ? null : unsecureConfig,
                    SecureConfiguration = string.IsNullOrWhiteSpace(secureConfig) ? null : secureConfig
                });
            }
            return list;
        }

        private static async Task<List<PluginStepItem>> FetchStepsByAssemblyAsync(EnvironmentProfile profile, string token, HashSet<Guid> assemblyIds)
        {
            var combined = new List<PluginStepItem>();
            foreach (var asmId in assemblyIds)
            {
                var filter = $"$filter=plugintypeid/_pluginassemblyid_value eq {asmId}";
                var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,stage,mode,rank,filteringattributes,supporteddeployment,asyncautodelete,configuration,_plugintypeid_value&$expand=plugintypeid($select=plugintypeid,_pluginassemblyid_value),sdkmessageid($select=name),sdkmessagefilterid($select=primaryobjecttypecode,secondaryobjecttypecode),sdkmessageprocessingstepsecureconfigid($select=secureconfig)&{filter}";
                List<JsonElement> raw;
                try
                {
                    raw = await FetchPagedAsync(url, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogService.Append($"Catalog step fetch: secure config expansion failed; retrying without secure config. {ex.Message}");
                    var fallbackUrl = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,stage,mode,rank,filteringattributes,supporteddeployment,asyncautodelete,configuration,_plugintypeid_value&$expand=plugintypeid($select=plugintypeid,_pluginassemblyid_value),sdkmessageid($select=name),sdkmessagefilterid($select=primaryobjecttypecode,secondaryobjecttypecode)&{filter}";
                    raw = await FetchPagedAsync(fallbackUrl, token).ConfigureAwait(false);
                }
                foreach (var item in raw)
                {
                    var typeId = item.GetPropertyGuid("_plugintypeid_value");
                    if (typeId == Guid.Empty)
                    {
                        typeId = item.TryGetNestedGuid("plugintypeid", "plugintypeid");
                    }
                    var secondary = item.TryGetNestedString("sdkmessagefilterid", "secondaryobjecttypecode");
                    if (!string.IsNullOrWhiteSpace(secondary) && !secondary.Equals("none", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var unsecureConfig = item.GetPropertyString("configuration");
                    var secureConfig = item.TryGetNestedString("sdkmessageprocessingstepsecureconfigid", "secureconfig");

                    combined.Add(new PluginStepItem
                    {
                        Id = item.GetPropertyGuid("sdkmessageprocessingstepid"),
                        Name = item.GetPropertyString("name"),
                        PluginTypeId = typeId,
                        AssemblyId = item.TryGetNestedGuid("plugintypeid", "_pluginassemblyid_value"),
                        Message = item.TryGetNestedString("sdkmessageid", "name"),
                        PrimaryEntity = item.TryGetNestedString("sdkmessagefilterid", "primaryobjecttypecode"),
                        Stage = item.TryGetInt("stage"),
                        Mode = item.TryGetInt("mode"),
                        FilteringAttributes = item.GetPropertyString("filteringattributes"),
                        Rank = item.TryGetInt("rank"),
                        SupportedDeployment = item.TryGetInt("supporteddeployment"),
                        AsyncAutoDelete = item.TryGetBool("asyncautodelete"),
                        UnsecureConfiguration = string.IsNullOrWhiteSpace(unsecureConfig) ? null : unsecureConfig,
                        SecureConfiguration = string.IsNullOrWhiteSpace(secureConfig) ? null : secureConfig
                    });
                }
            }
            return combined;
        }

        private static async Task<List<PluginImageItem>> FetchImagesAsync(EnvironmentProfile profile, string token)
        {
            var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid,imagetype,entityalias,attributes,_sdkmessageprocessingstepid_value";
            var raw = await FetchPagedAsync(url, token).ConfigureAwait(false);
            return ParsePluginImages(raw);
        }

        private static List<PluginImageItem> ParsePluginImages(List<JsonElement> raw)
        {
            var list = new List<PluginImageItem>();
            foreach (var item in raw)
            {
                var imageTypeRaw = item.TryGetInt("imagetype");
                var imageType = imageTypeRaw switch
                {
                    0 => "PreImage",
                    1 => "PostImage",
                    2 => "Both",
                    _ => imageTypeRaw.ToString()
                };
                list.Add(new PluginImageItem
                {
                    Id = item.GetPropertyGuid("sdkmessageprocessingstepimageid"),
                    StepId = item.GetPropertyGuid("_sdkmessageprocessingstepid_value"),
                    ImageType = imageType,
                    EntityAlias = item.GetPropertyString("entityalias"),
                    Attributes = item.GetPropertyString("attributes")
                });
            }
            return list;
        }

        private static async Task<JsonDocument> GetJsonAsync(string url, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.TryAddWithoutValidation("Prefer", "odata.maxpagesize=5000");
            var response = await Http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return JsonDocument.Parse(content);
        }

    }

    internal static class JsonExtensions
    {
        public static string GetPropertyString(this JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        public static Guid GetPropertyGuid(this JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var guid))
            {
                return guid;
            }
            return Guid.Empty;
        }

        public static int TryGetInt(this JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val))
            {
                return val;
            }
            return 0;
        }

        public static bool TryGetBool(this JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                {
                    return prop.GetBoolean();
                }
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var num))
                {
                    return num != 0;
                }
            }
            return false;
        }

        public static Guid TryGetNestedGuid(this JsonElement element, string nestedName, string propertyName)
        {
            if (element.TryGetProperty(nestedName, out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                if (nested.TryGetProperty(propertyName, out var idProp) && idProp.ValueKind == JsonValueKind.String && Guid.TryParse(idProp.GetString(), out var guid))
                {
                    return guid;
                }
            }
            return Guid.Empty;
        }

        public static string TryGetNestedString(this JsonElement element, string nestedName, string property)
        {
            if (element.TryGetProperty(nestedName, out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                if (nested.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
