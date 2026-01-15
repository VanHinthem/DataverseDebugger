#if NET48
using DataverseDebugger.Runner.ExecutionContext;
using DataverseDebugger.Runner.Pipeline;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class ExecutionContextBuilderTests
    {
        [TestMethod]
        public void WebApiContextBuilder_ThrowsNotImplemented()
        {
            var builder = new WebApiContextBuilder();

            Assert.ThrowsException<NotImplementedException>(() => builder.Build(new ExecutionRequest()));
        }

        [TestMethod]
        public void ProfilerContextBuilder_ThrowsNotImplemented()
        {
            var builder = new ProfilerContextBuilder();

            Assert.ThrowsException<NotImplementedException>(() => builder.Build(new ExecutionRequest()));
        }
    }
}
#endif
