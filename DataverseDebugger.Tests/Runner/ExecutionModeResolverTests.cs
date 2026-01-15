#if NET48
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Abstractions;
using DataverseDebugger.Runner.Pipeline;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class ExecutionModeResolverTests
    {
        [TestMethod]
        public void Resolve_UsesExplicitExecutionMode()
        {
            var request = new PluginInvokeRequest
            {
                ExecutionMode = "Offline"
            };

            var mode = ExecutionModeResolver.Resolve(request);

            Assert.AreEqual(ExecutionMode.Offline, mode);
        }

        [TestMethod]
        public void Resolve_LiveWriteModeForcesOnline()
        {
            var request = new PluginInvokeRequest
            {
                WriteMode = "LiveWrites"
            };

            var mode = ExecutionModeResolver.Resolve(request);

            Assert.AreEqual(ExecutionMode.Online, mode);
        }

        [TestMethod]
        public void Resolve_DefaultsToHybrid()
        {
            var request = new PluginInvokeRequest();

            var mode = ExecutionModeResolver.Resolve(request);

            Assert.AreEqual(ExecutionMode.Hybrid, mode);
        }

        [TestMethod]
        public void Resolve_InvalidExecutionModeThrows()
        {
            var request = new PluginInvokeRequest
            {
                ExecutionMode = "NotValid"
            };

            Assert.ThrowsException<RunnerNotSupportedException>(() => ExecutionModeResolver.Resolve(request));
        }

        [TestMethod]
        public void TryParse_RecognizesKnownValues()
        {
            Assert.IsTrue(ExecutionModeResolver.TryParse("Hybrid", out var mode));
            Assert.AreEqual(ExecutionMode.Hybrid, mode);

            Assert.IsTrue(ExecutionModeResolver.TryParse("Online", out mode));
            Assert.AreEqual(ExecutionMode.Online, mode);

            Assert.IsTrue(ExecutionModeResolver.TryParse("Offline", out mode));
            Assert.AreEqual(ExecutionMode.Offline, mode);
        }
    }
}
#endif
