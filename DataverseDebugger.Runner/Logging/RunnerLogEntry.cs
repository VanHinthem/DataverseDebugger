using System;

namespace DataverseDebugger.Runner.Logging
{
    internal sealed class RunnerLogEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        public string Message { get; set; } = string.Empty;
    }
}
