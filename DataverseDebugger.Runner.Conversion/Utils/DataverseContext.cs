using System.Net.Http;
using Microsoft.OData.Edm;
using Microsoft.Xrm.Tooling.Connector;

namespace DataverseDebugger.Runner.Conversion.Utils
{
    /// <summary>
    /// Provides context and services required for Web API to SDK conversion.
    /// </summary>
    /// <remarks>
    /// This class aggregates all the dependencies needed by the request and response converters,
    /// including the OData model, metadata cache, HTTP client, and CRM service client.
    /// </remarks>
    public class DataverseContext
    {
        /// <summary>
        /// Gets or sets the Dataverse environment host name (e.g., "org.crm.dynamics.com").
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the CRM service client for SDK operations and authentication.
        /// </summary>
        public CrmServiceClient CrmServiceClient { get; set; }

        /// <summary>
        /// Gets or sets the HTTP client for making Web API requests (e.g., for retrieve proxy).
        /// </summary>
        public HttpClient HttpClient { get; set; }

        /// <summary>
        /// Gets or sets the metadata cache for entity and attribute lookups.
        /// </summary>
        public MetadataCache MetadataCache { get; set; }

        /// <summary>
        /// Gets the base URL for Web API requests.
        /// </summary>
        public string WebApiBaseUrl => $"https://{this.Host}/api/data/v9.2/";

        /// <summary>
        /// Gets or sets the OData EDM model for parsing and validating Web API requests.
        /// </summary>
        public IEdmModel Model { get; set; }

        /// <summary>
        /// Adds authorization headers to an HTTP request using the current access token.
        /// </summary>
        /// <param name="httpRequest">The HTTP request to add headers to.</param>
        public void AddAuthorizationHeaders(HttpRequestMessage httpRequest)
        {
            httpRequest.Headers.Add("Authorization", "Bearer " + this.CrmServiceClient.CurrentAccessToken);
        }
    }
}
