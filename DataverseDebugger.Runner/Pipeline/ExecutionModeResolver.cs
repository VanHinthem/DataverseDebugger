using System;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Abstractions;

namespace DataverseDebugger.Runner.Pipeline
{
    internal static class ExecutionModeResolver
    {
        public static ExecutionMode Resolve(PluginInvokeRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var explicitMode = request.ExecutionMode;
            if (!string.IsNullOrWhiteSpace(explicitMode))
            {
                if (TryParse(explicitMode, out var mode))
                {
                    return mode;
                }

                throw new RunnerNotSupportedException(
                    explicitMode!,
                    "ExecutionMode",
                    "Use Offline, Hybrid, or Online.");
            }

            var writeMode = request.WriteMode;
            if (string.Equals(writeMode, "LiveWrites", StringComparison.OrdinalIgnoreCase)
                || string.Equals(writeMode, "Live", StringComparison.OrdinalIgnoreCase))
            {
                return ExecutionMode.Online;
            }

            return ExecutionMode.Hybrid;
        }

        public static bool TryParse(string? value, out ExecutionMode mode)
        {
            mode = ExecutionMode.Hybrid;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value!.Trim();
            if (string.Equals(normalized, "Offline", StringComparison.OrdinalIgnoreCase))
            {
                mode = ExecutionMode.Offline;
                return true;
            }

            if (string.Equals(normalized, "Hybrid", StringComparison.OrdinalIgnoreCase))
            {
                mode = ExecutionMode.Hybrid;
                return true;
            }

            if (string.Equals(normalized, "Online", StringComparison.OrdinalIgnoreCase))
            {
                mode = ExecutionMode.Online;
                return true;
            }

            return false;
        }
    }
}
