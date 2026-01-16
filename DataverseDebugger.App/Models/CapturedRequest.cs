using System;
using System.Collections.Generic;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Represents a captured HTTP request from the browser with its response and debug info.
    /// </summary>
    /// <remarks>
    /// Stores both the original request data and the response after execution through the runner,
    /// including execution trace, plugin images, and matching step information.
    /// </remarks>
    public class CapturedRequest
    {
        /// <summary>Gets or sets the HTTP method (GET, POST, PATCH, DELETE).</summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>Gets or sets the original unmodified URL.</summary>
        public string OriginalUrl { get; set; } = string.Empty;

        /// <summary>Gets or sets the URL (may be modified for display).</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Gets or sets the request headers as a formatted string.</summary>
        public string Headers { get; set; } = string.Empty;

        /// <summary>Gets or sets a preview of the request body.</summary>
        public string BodyPreview { get; set; } = string.Empty;

        /// <summary>Gets or sets the raw request body bytes.</summary>
        public byte[]? Body { get; set; }

        /// <summary>Gets or sets the client request ID from headers.</summary>
        public string? ClientRequestId { get; set; }

        /// <summary>Gets or sets when the request was captured.</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>Gets or sets the request headers as a dictionary.</summary>
        public Dictionary<string, List<string>> HeadersDictionary { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets or sets the HTTP response status code.</summary>
        public int? ResponseStatus { get; set; }

        /// <summary>Gets or sets a preview of the response body.</summary>
        public string? ResponseBodyPreview { get; set; }

        /// <summary>Gets or sets the raw response body bytes.</summary>
        public byte[]? ResponseBody { get; set; }

        /// <summary>Gets or sets the response headers as a dictionary.</summary>
        public Dictionary<string, List<string>> ResponseHeadersDictionary { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets or sets the execution trace lines from the runner.</summary>
        public List<string> ResponseTraceLines { get; set; } = new();

        /// <summary>Gets or sets the plugin step images (pre/post images by step ID).</summary>
        public Dictionary<Guid, List<PluginImagePayload>> StepImages { get; set; } = new();

        /// <summary>Gets or sets the count of matching plugin types for this request.</summary>
        public int MatchingTypesCount { get; set; }

        /// <summary>Gets or sets whether the request was auto-proxied through the runner.</summary>
        public bool AutoProxied { get; set; }

        /// <summary>Gets or sets whether the request was auto-responded by a WebResource rule.</summary>
        public bool AutoResponded { get; set; }

        /// <summary>Gets or sets whether a WebResource AutoResponder rule matched this request.</summary>
        public bool AutoResponderMatched { get; set; }

        /// <summary>Gets or sets the matching AutoResponder rule summary.</summary>
        public string? AutoResponderRule { get; set; }

        /// <summary>Gets or sets the resolved AutoResponder target (path or URL).</summary>
        public string? AutoResponderResolved { get; set; }

        /// <summary>Gets or sets the AutoResponder status or fallback reason.</summary>
        public string? AutoResponderStatus { get; set; }

        /// <summary>Gets or sets whether the request can be converted to an SDK request.</summary>
        public bool CanConvert { get; set; }

        /// <summary>Gets or sets whether the request has matching plugin steps.</summary>
        public bool HasSteps { get; set; }

        /// <summary>Gets whether there are matching plugin types or an AutoResponder hit.</summary>
        public bool HasMatch => MatchingTypesCount > 0 || AutoResponded;

        /// <summary>Gets whether there are matching steps or an AutoResponder rule match.</summary>
        public bool HasStepsIndicator => IsWebResource ? AutoResponderMatched : HasSteps;

        /// <summary>Gets whether the request targets a WebResource URL.</summary>
        public bool IsWebResource
        {
            get
            {
                var url = !string.IsNullOrWhiteSpace(OriginalUrl) ? OriginalUrl : Url;
                return url.IndexOf("/webresources/", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>Gets a display string for the request.</summary>
        public string Display => $"{Method} {Url}";
    }
}
