#if NET48
using DataverseDebugger.Runner.Configuration;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    [DoNotParallelize]
    public sealed class RunnerExecutionOptionsTests
    {
        [TestMethod]
        public void FromEnvironment_ParsesTrueValues()
        {
            var original = Environment.GetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar);
            try
            {
                Environment.SetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar, "1");
                var options = RunnerExecutionOptions.FromEnvironment();

                Assert.IsTrue(options.AllowLiveWrites);
            }
            finally
            {
                Environment.SetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar, original);
            }
        }

        [TestMethod]
        public void FromEnvironment_ParsesFalseValues()
        {
            var original = Environment.GetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar);
            try
            {
                Environment.SetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar, "0");
                var options = RunnerExecutionOptions.FromEnvironment();

                Assert.IsFalse(options.AllowLiveWrites);
            }
            finally
            {
                Environment.SetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar, original);
            }
        }

        [TestMethod]
        public void FromEnvironment_IgnoresInvalidValue()
        {
            var original = Environment.GetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar);
            try
            {
                Environment.SetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar, "not-a-bool");
                var options = RunnerExecutionOptions.FromEnvironment();

                Assert.IsFalse(options.AllowLiveWrites);
            }
            finally
            {
                Environment.SetEnvironmentVariable(RunnerExecutionOptions.AllowLiveWritesEnvVar, original);
            }
        }
    }
}
#endif
