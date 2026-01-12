using System;
using System.Collections.Generic;

namespace DataverseDebugger.Runner.Pipeline
{
    internal sealed class ExecutionResult
    {
        public List<string> TraceLines { get; } = new List<string>();

        public Dictionary<string, object?> Outputs { get; } = new Dictionary<string, object?>();

        public Exception? Error { get; set; }
    }
}
