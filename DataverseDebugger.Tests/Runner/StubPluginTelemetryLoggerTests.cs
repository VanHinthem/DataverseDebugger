#if NET48
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataverseDebugger.Runner;
using Microsoft.Xrm.Sdk.PluginTelemetry;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class StubPluginTelemetryLoggerTests
    {
        [TestMethod]
        public void Log_WritesMessagesAndExceptions()
        {
            var entries = new List<string>();
            var logger = new StubPluginTelemetryLogger(entries.Add);

            logger.LogInformation(new EventId(12, "Info"), "Hello {0}", "World");
            logger.LogError(new Exception("boom"), "Error {0}", "Now");
            logger.LogWarning(new EventId(99, "Warn"), new Exception("warn"), "Warn {0}", "x");
            logger.Log(LogLevel.Debug, new EventId(3, "Debug"), "Debug {0}", "Value");
            logger.Log(LogLevel.Trace, "Trace {0}", "Value");
            logger.Log(LogLevel.Debug, new EventId(4, "State"), "state", null!, (state, ex) => "State " + state);
            logger.LogMetric("requests", 3);
            logger.LogMetric("timing", new Dictionary<string, string> { ["env"] = "dev" }, 5);

            Assert.IsTrue(entries.Any(e => e.Contains("Hello World")));
            Assert.IsTrue(entries.Any(e => e.Contains("Error Now")));
            Assert.IsTrue(entries.Any(e => e.Contains("boom")));
            Assert.IsTrue(entries.Any(e => e.IndexOf("metric", StringComparison.OrdinalIgnoreCase) >= 0));
            Assert.IsTrue(entries.Any(e => e.IndexOf("dims=env=dev", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [TestMethod]
        public void CustomProperties_AppearInLog()
        {
            var entries = new List<string>();
            var logger = new StubPluginTelemetryLogger(entries.Add);

            logger.AddCustomProperty("key", "value");
            logger.LogInformation("Message");

            Assert.IsTrue(entries.Any(e => e.Contains("props=key=value")));
        }

        [TestMethod]
        public void Execute_LogsSuccessAndFailure()
        {
            var entries = new List<string>();
            var logger = new StubPluginTelemetryLogger(entries.Add);

            logger.Execute("Work", () => { }, new[] { new KeyValuePair<string, string>("a", "b") });

            Assert.IsTrue(entries.Any(e => e.Contains("Execute Work (sync) completed")));

            Assert.ThrowsException<InvalidOperationException>(() =>
                logger.Execute("Fail", () => throw new InvalidOperationException("nope"), null!));

            Assert.IsTrue(entries.Any(e => e.Contains("Execute Fail (sync) failed")));
        }

        [TestMethod]
        public async Task ExecuteAsync_LogsCompletion()
        {
            var entries = new List<string>();
            var logger = new StubPluginTelemetryLogger(entries.Add);

            await logger.ExecuteAsync("Async", () => Task.CompletedTask, null!);

            Assert.IsTrue(entries.Any(e => e.Contains("Execute Async (async) completed")));
        }

        [TestMethod]
        public void BeginScope_FormatsMessage()
        {
            var entries = new List<string>();
            var logger = new StubPluginTelemetryLogger(entries.Add);

            using (logger.BeginScope("Scope {0}", "Run"))
            {
            }

            Assert.IsTrue(entries.Any(e => e.Contains("Scope")));
        }

        [TestMethod]
        public void FormatMessage_FallsBackOnBadFormat()
        {
            var entries = new List<string>();
            var logger = new StubPluginTelemetryLogger(entries.Add);

            logger.LogInformation("Bad {0");

            Assert.IsTrue(entries.Any(e => e.Contains("Bad {0")));
        }
    }
}
#endif
