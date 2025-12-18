using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataverseDebugger.App.Models;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Calls the Dataverse global discovery endpoint to enumerate environments for a signed-in user.
    /// </summary>
    public static class DiscoveryService
    {
        private static readonly Uri DiscoveryUri = new("https://globaldisco.crm.dynamics.com/api/discovery/v2.0/Instances?api-version=2024-10-01");

        /// <summary>
        /// Retrieves all environments visible to the authenticated user.
        /// </summary>
        /// <param name="accessToken">Bearer token for the global discovery resource.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public static async Task<IReadOnlyList<DataverseInstance>> GetInstancesAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return Array.Empty<DataverseInstance>();
            }

            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, DiscoveryUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("OData-Version", "4.0");
            request.Headers.Add("OData-MaxVersion", "4.0");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var reason = string.IsNullOrWhiteSpace(errorBody) ? response.ReasonPhrase : errorBody;
                throw new InvalidOperationException($"Discovery failed ({(int)response.StatusCode} {response.ReasonPhrase}). {reason}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<DataverseInstance>();
            }

            var results = new List<DataverseInstance>();
            foreach (var instanceElement in valueElement.EnumerateArray())
            {
                var friendlyName = instanceElement.GetPropertyOrDefault("FriendlyName");
                var uniqueName = instanceElement.GetPropertyOrDefault("UniqueName");
                var geo = instanceElement.GetPropertyOrDefault("Region") ?? instanceElement.GetPropertyOrDefault("Geo") ?? instanceElement.GetPropertyOrDefault("EnvironmentSku") ?? string.Empty;
                var environmentId = instanceElement.GetPropertyOrDefault("EnvironmentId") ?? instanceElement.GetPropertyOrDefault("Id") ?? string.Empty;
                Guid? organizationId = null;
                if (instanceElement.TryGetProperty("OrganizationId", out var orgIdElement) && orgIdElement.ValueKind == JsonValueKind.String && Guid.TryParse(orgIdElement.GetString(), out var parsedGuid))
                {
                    organizationId = parsedGuid;
                }

                var webUrl = ExtractWebUrl(instanceElement);
                var apiUrl = ExtractApiUrl(instanceElement);
                if (string.IsNullOrWhiteSpace(webUrl))
                {
                    continue;
                }

                results.Add(new DataverseInstance
                {
                    FriendlyName = friendlyName ?? uniqueName ?? webUrl,
                    UniqueName = uniqueName ?? string.Empty,
                    Geo = geo,
                    WebUrl = webUrl,
                    ApiUrl = apiUrl ?? string.Empty,
                    EnvironmentId = environmentId,
                    OrganizationId = organizationId
                });
            }

            results.Sort(static (a, b) => string.Compare(a.FriendlyName, b.FriendlyName, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private static string? ExtractWebUrl(JsonElement instanceElement)
        {
            string? direct = TryGetString(instanceElement, "WebApplicationUrl")
                             ?? TryGetString(instanceElement, "Url")
                             ?? TryGetString(instanceElement, "InstanceUrl")
                             ?? TryGetEndpoint(instanceElement, "WebApplicationUrl")
                             ?? TryGetEndpoint(instanceElement, "WebApplication");
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            var apiUrl = ExtractApiUrl(instanceElement);
            if (!string.IsNullOrWhiteSpace(apiUrl) && apiUrl.Contains(".api.", StringComparison.OrdinalIgnoreCase))
            {
                return apiUrl.Replace(".api.", ".", StringComparison.OrdinalIgnoreCase);
            }

            var urlName = TryGetString(instanceElement, "UrlName") ?? TryGetString(instanceElement, "EnvironmentUrlName");
            var region = TryGetString(instanceElement, "Region") ?? TryGetString(instanceElement, "Geo") ?? "crm";
            if (!string.IsNullOrWhiteSpace(urlName))
            {
                return $"https://{urlName}.{region}.dynamics.com";
            }

            return null;
        }

        private static string? ExtractApiUrl(JsonElement instanceElement)
        {
            var apiUrl = TryGetString(instanceElement, "ApiUrl")
                         ?? TryGetEndpoint(instanceElement, "OrganizationService")
                         ?? TryGetEndpoint(instanceElement, "ApiUrl");
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                return apiUrl;
            }

            if (instanceElement.TryGetProperty("Properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in propertiesElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, "ApiUrl", StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value.GetString();
                    }
                }
            }

            return null;
        }

        private static string? TryGetEndpoint(JsonElement instanceElement, string desiredName)
        {
            if (instanceElement.TryGetProperty("Endpoints", out var endpointsElement) && endpointsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var endpoint in endpointsElement.EnumerateObject())
                {
                    if (string.Equals(endpoint.Name, desiredName, StringComparison.OrdinalIgnoreCase))
                    {
                        return endpoint.Value.GetString();
                    }
                }
            }

            return null;
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static string? GetPropertyOrDefault(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.ToString(),
                    _ => null
                };
            }

            return null;
        }
    }
}
