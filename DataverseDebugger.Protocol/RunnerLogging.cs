using System;
using System.Collections.Generic;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Log verbosity level for runner output.
    /// </summary>
    public enum RunnerLogLevel
    {
        /// <summary>Standard informational messages.</summary>
        Info = 0,

        /// <summary>Detailed debug messages.</summary>
        Debug = 1
    }

    /// <summary>
    /// Categories of runner log messages for filtering.
    /// </summary>
    [Flags]
    public enum RunnerLogCategory
    {
        /// <summary>No categories.</summary>
        None = 0,

        /// <summary>Runner process lifecycle events.</summary>
        RunnerLifecycle = 1 << 0,

        /// <summary>Inter-process communication messages.</summary>
        Ipc = 1 << 1,

        /// <summary>Workspace initialization events.</summary>
        WorkspaceInit = 1 << 2,

        /// <summary>Assembly loading events.</summary>
        AssemblyLoad = 1 << 3,

        /// <summary>Plugin cache operations.</summary>
        PluginCache = 1 << 4,

        /// <summary>Metadata retrieval and caching.</summary>
        Metadata = 1 << 5,

        /// <summary>Plugin emulation execution.</summary>
        Emulator = 1 << 6,

        /// <summary>Debugger integration events.</summary>
        Debugger = 1 << 7,

        /// <summary>Performance metrics.</summary>
        Perf = 1 << 8,

        /// <summary>Error conditions.</summary>
        Errors = 1 << 9,

        /// <summary>All categories enabled.</summary>
        All = RunnerLifecycle | Ipc | WorkspaceInit | AssemblyLoad | PluginCache | Metadata | Emulator | Debugger | Perf | Errors
    }

    /// <summary>
    /// Request to configure runner logging settings.
    /// </summary>
    public sealed class RunnerLogConfigRequest
    {
        /// <summary>Protocol version.</summary>
        public int Version { get; set; } = ProtocolVersion.Current;

        /// <summary>Minimum log level to capture.</summary>
        public RunnerLogLevel Level { get; set; } = RunnerLogLevel.Info;

        /// <summary>Categories to include in log output.</summary>
        public RunnerLogCategory Categories { get; set; } = RunnerLogCategory.All;

        /// <summary>Maximum number of log entries to retain in the buffer.</summary>
        public int MaxEntries { get; set; } = 1000;
    }

    /// <summary>
    /// Response confirming log configuration was applied.
    /// </summary>
    public sealed class RunnerLogConfigResponse
    {
        /// <summary>Protocol version.</summary>
        public int Version { get; set; } = ProtocolVersion.Current;

        /// <summary>Whether the configuration was successfully applied.</summary>
        public bool Applied { get; set; }

        /// <summary>Optional message or error details.</summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Request to fetch recent log entries from the runner.
    /// </summary>
    public sealed class RunnerLogFetchRequest
    {
        /// <summary>Protocol version.</summary>
        public int Version { get; set; } = ProtocolVersion.Current;

        /// <summary>ID of the last log entry received (for incremental fetching).</summary>
        public long LastId { get; set; }

        /// <summary>Maximum number of entries to return.</summary>
        public int MaxEntries { get; set; } = 200;
    }

    /// <summary>
    /// Response containing log entries from the runner.
    /// </summary>
    public sealed class RunnerLogFetchResponse
    {
        /// <summary>Protocol version.</summary>
        public int Version { get; set; } = ProtocolVersion.Current;

        /// <summary>ID of the last log entry in this response.</summary>
        public long LastId { get; set; }

        /// <summary>Log lines returned.</summary>
        public List<string> Lines { get; set; } = new List<string>();
    }
}
