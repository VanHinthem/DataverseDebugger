#if NET48
using DataverseDebugger.Runner.Logging;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class LoggingTests
    {
        [TestMethod]
        public void RunnerLogger_IgnoresNullEntries()
        {
            var logger = new RunnerLogger();

            logger.Log(null!);

            Assert.AreEqual(0, logger.Entries.Count);
        }

        [TestMethod]
        public void TracingServiceAdapter_FormatsMessages()
        {
            var logger = new TestRunnerLogger();
            var adapter = new TracingServiceAdapter(logger);

            adapter.Trace("Hello {0}", "World");

            Assert.AreEqual(1, logger.Entries.Count);
            Assert.AreEqual("Hello World", logger.Entries[0].Message);
        }

        [TestMethod]
        public void TracingServiceAdapter_HandlesBadFormat()
        {
            var logger = new TestRunnerLogger();
            var adapter = new TracingServiceAdapter(logger);

            adapter.Trace("{0} {", "bad");

            Assert.AreEqual("{0} {", logger.Entries[0].Message);
        }

        private sealed class TestRunnerLogger : IRunnerLogger
        {
            private readonly List<RunnerLogEntry> _entries = new();

            public IReadOnlyList<RunnerLogEntry> Entries => _entries;

            public void Log(RunnerLogEntry entry)
            {
                if (entry != null)
                {
                    _entries.Add(entry);
                }
            }
        }
    }
}
#endif
