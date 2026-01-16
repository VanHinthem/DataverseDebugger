#if NET8_0_WINDOWS
using DataverseDebugger.App.Models;

namespace DataverseDebugger.Tests.App
{
    [TestClass]
    public sealed class RunnerSettingsModelTests
    {
        [TestMethod]
        public void ExecutionMode_DefaultsToHybrid()
        {
            var model = new RunnerSettingsModel();

            Assert.AreEqual("Hybrid", model.ExecutionMode);
        }

        [TestMethod]
        public void ExecutionMode_NullNormalizesToHybrid()
        {
            var model = new RunnerSettingsModel
            {
                ExecutionMode = null!
            };

            Assert.AreEqual("Hybrid", model.ExecutionMode);
        }

        [TestMethod]
        public void WriteMode_LiveWritesForcesOnline()
        {
            var model = new RunnerSettingsModel
            {
                WriteMode = "LiveWrites"
            };

            Assert.AreEqual("Online", model.ExecutionMode);
            Assert.IsTrue(model.AllowLiveWrites);
        }
    }
}
#endif
