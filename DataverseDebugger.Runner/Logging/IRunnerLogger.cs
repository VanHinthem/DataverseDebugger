using System.Collections.Generic;

namespace DataverseDebugger.Runner.Logging
{
    internal interface IRunnerLogger
    {
        void Log(RunnerLogEntry entry);

        IReadOnlyList<RunnerLogEntry> Entries { get; }
    }
}
