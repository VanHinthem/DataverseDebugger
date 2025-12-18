using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DataverseDebugger.App.Models;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Service for fetching entity records to populate plugin pre/post images.
    /// </summary>
    /// <remarks>
    /// Retrieves entity data from Dataverse Web API and converts it to the
    /// JSON format expected by the plugin execution context images.
    /// </remarks>
    public static class PluginImageFetchService
    {
        private static readonly HttpClient Http = new HttpClient();

        /// <summary>
        /// Fetches an entity record as JSON for use as a plugin image.
        /// </summary>
        /// <param name="profile">The environment profile.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        /// <param name="entitySetName">The OData entity set name.</param>
        /// <param name="logicalName">The entity logical name.</param>
        /// <param name="id">The record ID.</param>
        /// <param name="attributes">Optional list of attributes to select.</param>
        /// <returns>JSON representation of the entity, or null on failure.</returns>
        public static async Task<string?> FetchEntityJsonAsync(EnvironmentProfile profile, string accessToken, string entitySetName, string logicalName, Guid id, IEnumerable<string>? attributes)
        {
            if (string.IsNullOrWhiteSpace(profile.OrgUrl) || string.IsNullOrWhiteSpace(accessToken) || id == Guid.Empty)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(entitySetName))
            {
                return null;
            }

            var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/{entitySetName}({id})";
            var select = attributes?
                .Select(a => a?.Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (select != null && select.Count > 0)
            {
                var selectValue = string.Join(",", select);
                url += "?$select=" + Uri.EscapeDataString(selectValue);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.TryAddWithoutValidation("Prefer", "odata.include-annotations=\"Microsoft.Dynamics.CRM.lookuplogicalname\"");

            var response = await Http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var entity = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["logicalName"] = logicalName,
                ["id"] = id.ToString()
            };

            var lookupLogicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.IndexOf("@Microsoft.Dynamics.CRM.lookuplogicalname", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var baseName = prop.Name.Split('@')[0];
                var value = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(baseName) && !string.IsNullOrWhiteSpace(value))
                {
                    lookupLogicalNames[baseName] = value;
                }
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var name = prop.Name;
                if (name.StartsWith("@", StringComparison.Ordinal) || name.Contains("@"))
                {
                    continue;
                }

                if (name.StartsWith("_", StringComparison.OrdinalIgnoreCase) && name.EndsWith("_value", StringComparison.OrdinalIgnoreCase))
                {
                    var attrName = name.Substring(1, name.Length - "_value".Length - 1);
                    if (Guid.TryParse(prop.Value.GetString(), out var lookupId))
                    {
                        if (lookupLogicalNames.TryGetValue(name, out var lookupLogical))
                        {
                            entity[attrName] = new Dictionary<string, object?>
                            {
                                ["id"] = lookupId.ToString(),
                                ["logicalName"] = lookupLogical
                            };
                        }
                        else
                        {
                            entity[attrName] = lookupId.ToString();
                        }
                    }
                    continue;
                }

                object? value = null;
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        value = prop.Value.GetString();
                        break;
                    case JsonValueKind.Number:
                        if (prop.Value.TryGetInt64(out var l)) value = l;
                        else if (prop.Value.TryGetDouble(out var d)) value = d;
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        value = prop.Value.GetBoolean();
                        break;
                    case JsonValueKind.Null:
                        value = null;
                        break;
                    default:
                        value = prop.Value.ToString();
                        break;
                }

                if (value != null)
                {
                    entity[name] = value;
                }
            }

            return JsonSerializer.Serialize(entity);
        }
    }
}
