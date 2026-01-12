using System.Collections.Generic;

namespace DataverseDebugger.Runner.Logging
{
    internal sealed class RunnerLogger : IRunnerLogger
    {
        private readonly List<RunnerLogEntry> _entries = new List<RunnerLogEntry>();

        public IReadOnlyList<RunnerLogEntry> Entries => _entries;

        public void Log(RunnerLogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            _entries.Add(entry);
        }
    }
}
