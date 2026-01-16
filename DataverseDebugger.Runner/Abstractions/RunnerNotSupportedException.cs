using System;

namespace DataverseDebugger.Runner.Abstractions
{
    internal sealed class RunnerNotSupportedException : NotSupportedException
    {
        public RunnerNotSupportedException(string mode, string operation, string guidance)
            : base(BuildMessage(mode, operation, guidance))
        {
            Mode = mode ?? string.Empty;
            Operation = operation ?? string.Empty;
            Guidance = guidance ?? string.Empty;
        }

        public string Mode { get; }
        public string Operation { get; }
        public string Guidance { get; }

        private static string BuildMessage(string? mode, string? operation, string? guidance)
        {
            var safeMode = string.IsNullOrWhiteSpace(mode) ? "Unknown" : mode!.Trim();
            var safeOperation = string.IsNullOrWhiteSpace(operation) ? "UnknownOperation" : operation!.Trim();
            var safeGuidance = string.IsNullOrWhiteSpace(guidance) ? "Not supported." : guidance!.Trim();
            return $"Mode={safeMode}; Operation={safeOperation}; Guidance={safeGuidance}";
        }
    }
}
