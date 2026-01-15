#if NET48
using System.Collections;
using System.Linq;
using System.Reflection;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    [DoNotParallelize]
    public sealed class RunnerLoggerTests
    {
        [TestInitialize]
        public void ResetLogger()
        {
            var type = typeof(RunnerLogger);
            var entriesField = type.GetField("Entries", BindingFlags.NonPublic | BindingFlags.Static);
            if (entriesField?.GetValue(null) is IList entries)
            {
                entries.Clear();
            }

            var nextIdField = type.GetField("_nextId", BindingFlags.NonPublic | BindingFlags.Static);
            nextIdField?.SetValue(null, 1L);
        }

        [TestMethod]
        public void Log_RespectsCategoryFilter()
        {
            RunnerLogger.Configure(new RunnerLogConfigRequest
            {
                Categories = RunnerLogCategory.RunnerLifecycle,
                Level = RunnerLogLevel.Info,
                MaxEntries = 200
            });

            RunnerLogger.Log(RunnerLogCategory.RunnerLifecycle, RunnerLogLevel.Info, "allowed");
            RunnerLogger.Log(RunnerLogCategory.Ipc, RunnerLogLevel.Info, "blocked");

            var response = RunnerLogger.Fetch(new RunnerLogFetchRequest { LastId = 0, MaxEntries = 10 });

            Assert.IsTrue(response.Lines.Any(line => line.Contains("allowed")));
            Assert.IsFalse(response.Lines.Any(line => line.Contains("blocked")));
        }

        [TestMethod]
        public void Configure_ClampsMaxEntriesToMinimum()
        {
            RunnerLogger.Configure(new RunnerLogConfigRequest
            {
                Categories = RunnerLogCategory.All,
                Level = RunnerLogLevel.Info,
                MaxEntries = 1
            });

            var type = typeof(RunnerLogger);
            var maxEntriesField = type.GetField("_maxEntries", BindingFlags.NonPublic | BindingFlags.Static);
            var maxEntries = (int)(maxEntriesField?.GetValue(null) ?? 0);

            Assert.AreEqual(200, maxEntries);
        }
    }
}
#endif
