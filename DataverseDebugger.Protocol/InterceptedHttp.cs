using System;
using System.Collections.Generic;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Represents an HTTP request intercepted by the debugger proxy.
    /// </summary>
    public sealed class InterceptedHttpRequest
    {
        /// <summary>HTTP method (GET, POST, PATCH, DELETE, etc.).</summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>Full request URL including query string.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>HTTP headers with their values (headers can have multiple values).</summary>
        public Dictionary<string, List<string>> Headers { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raw request body bytes.</summary>
        public byte[] Body { get; set; } = Array.Empty<byte>();

        /// <summary>Optional correlation ID for request tracking.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Represents an HTTP response returned by the debugger proxy.
    /// </summary>
    public sealed class InterceptedHttpResponse
    {
        /// <summary>HTTP status code.</summary>
        public int StatusCode { get; set; }

        /// <summary>HTTP response headers with their values.</summary>
        public Dictionary<string, List<string>> Headers { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raw response body bytes.</summary>
        public byte[] Body { get; set; } = Array.Empty<byte>();
    }
}
