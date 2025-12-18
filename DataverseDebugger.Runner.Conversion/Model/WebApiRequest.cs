using System;
using System.Collections.Specialized;

namespace DataverseDebugger.Runner.Conversion.Model
{
    /// <summary>
    /// Represents an incoming Dataverse Web API request to be converted to an SDK OrganizationRequest.
    /// </summary>
    /// <remarks>
    /// This class encapsulates HTTP request details (method, path, headers, body) from the Web API
    /// and provides factory methods to create instances from different URL formats.
    /// </remarks>
    public class WebApiRequest
    {
        /// <summary>
        /// Gets the HTTP method (GET, POST, PATCH, DELETE).
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// Gets the local path with query string (e.g., "/api/data/v9.2/accounts(guid)?$select=name").
        /// </summary>
        public string LocalPathWithQuery { get; }

        /// <summary>
        /// Gets the request body content (JSON payload for POST/PATCH operations).
        /// </summary>
        public string Body { get; }

        /// <summary>
        /// Gets the HTTP headers collection.
        /// </summary>
        public NameValueCollection Headers { get; }

        /// <summary>
        /// Creates a WebApiRequest from a full or relative URL.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="url">The absolute or relative URL.</param>
        /// <param name="headers">The HTTP headers.</param>
        /// <param name="body">The request body (optional).</param>
        /// <returns>A WebApiRequest instance, or null if the URL is not a valid Web API endpoint.</returns>
        public static WebApiRequest Create(string method, string url, NameValueCollection headers, string body = null)
        {
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                throw new UriFormatException("Invalid URI: " + url);
            }

            string localPathWithQuery;
            if (uri.IsAbsoluteUri)
            {
                localPathWithQuery = uri.LocalPath + uri.Query;
            }
            else
            {
                // Relative URI (common inside $batch); ensure it starts with a slash so LocalPathWithQuery is valid.
                localPathWithQuery = uri.OriginalString.StartsWith("/")
                    ? uri.OriginalString
                    : "/" + uri.OriginalString;
            }

            return CreateFromLocalPathWithQuery(method, localPathWithQuery, headers, body);
        }

        /// <summary>
        /// Creates a WebApiRequest from a local path with query string.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="localPathWithQuery">The local path with query (must start with /api/data/v9.x).</param>
        /// <param name="headers">The HTTP headers.</param>
        /// <param name="body">The request body (optional).</param>
        /// <returns>A WebApiRequest instance, or null if the path is not a valid Web API endpoint.</returns>
        public static WebApiRequest CreateFromLocalPathWithQuery(string method, string localPathWithQuery, NameValueCollection headers, string body = null)
        {
            if (!localPathWithQuery.StartsWith("/api/data/v9.", StringComparison.OrdinalIgnoreCase))
                return null;
            return new WebApiRequest(method, localPathWithQuery, headers, body);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebApiRequest"/> class.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="localPathWithQuery">The local path with query string.</param>
        /// <param name="headers">The HTTP headers.</param>
        /// <param name="body">The request body.</param>
        internal WebApiRequest(string method, string localPathWithQuery, NameValueCollection headers, string body)
        {
            this.Method = method ?? throw new ArgumentNullException(nameof(method));
            this.LocalPathWithQuery = localPathWithQuery ?? throw new ArgumentNullException(nameof(localPathWithQuery));
            this.Headers = headers ?? throw new ArgumentNullException(nameof(headers));
            this.Body = body;
        }


    }
}
