namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Constants for named pipe communication between host and runner.
    /// </summary>
    public static class PipeNames
    {
        /// <summary>Named pipe identifier for host-runner IPC.</summary>
        public const string RunnerPipe = "DataverseDebugger.Runner";
    }
}
