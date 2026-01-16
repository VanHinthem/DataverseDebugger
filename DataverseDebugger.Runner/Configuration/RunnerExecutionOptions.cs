using System;

namespace DataverseDebugger.Runner.Configuration
{
    internal sealed class RunnerExecutionOptions
    {
        public const string AllowLiveWritesEnvVar = "DATAVERSE_DEBUGGER_ALLOW_LIVE_WRITES";

        public bool AllowLiveWrites { get; set; } = false;

        public static RunnerExecutionOptions FromEnvironment()
        {
            var options = new RunnerExecutionOptions();
            var raw = Environment.GetEnvironmentVariable(AllowLiveWritesEnvVar);
            if (TryParseBoolean(raw, out var allow))
            {
                options.AllowLiveWrites = allow;
            }

            return options;
        }

        private static bool TryParseBoolean(string? raw, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            if (bool.TryParse(raw, out value))
            {
                return true;
            }

            var trimmed = raw!.Trim();
            if (string.Equals(trimmed, "1", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(trimmed, "0", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }
    }
}
