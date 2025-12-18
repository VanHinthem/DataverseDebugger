using System;
using System.Collections.Generic;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Payload used to mirror a captured request inside the REST Builder view.
    /// </summary>
    public sealed class RestBuilderInjectionRequest
    {
        public string RequestName { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? PrimaryEntityLogicalName { get; set; }
        public string? EntitySetName { get; set; }
        public string? PrimaryId { get; set; }
        public string? DataverseOperationName { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Query { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? QueryType { get; set; }
        public string? FetchXml { get; set; }
        public string? Body { get; set; }
        public bool BodyIsBinary { get; set; }
        public string? BodyBase64 { get; set; }
        public string? ContentType { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
