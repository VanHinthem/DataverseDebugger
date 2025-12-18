using System.Collections.Specialized;

namespace DataverseDebugger.Runner.Conversion.Model
{
    /// <summary>
    /// Represents an outgoing Dataverse Web API response converted from an SDK OrganizationResponse.
    /// </summary>
    /// <remarks>
    /// This class encapsulates HTTP response details (status code, headers, body) that are returned
    /// to the Web API client after executing an OrganizationRequest.
    /// </remarks>
    public class WebApiResponse
    {
        /// <summary>
        /// Gets or sets the response body as raw bytes.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets the HTTP response headers.
        /// </summary>
        public NameValueCollection Headers { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebApiResponse"/> class.
        /// </summary>
        public WebApiResponse()
        {
        }
    }
}
