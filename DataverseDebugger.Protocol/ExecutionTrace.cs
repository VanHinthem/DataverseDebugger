using System.Collections.Generic;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Trace information captured during plugin execution.
    /// </summary>
    public sealed class ExecutionTrace
    {
        /// <summary>Indicates whether the request was handled by local emulation.</summary>
        public bool Emulated { get; set; }

        /// <summary>Trace log lines captured during execution.</summary>
        public List<string> TraceLines { get; set; } = new List<string>();

        /// <summary>Exception message if an error occurred during execution.</summary>
        public string? Exception { get; set; }
    }
}
