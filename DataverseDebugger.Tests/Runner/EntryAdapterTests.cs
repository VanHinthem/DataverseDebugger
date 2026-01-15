#if NET48
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.EntryAdapters;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class EntryAdapterTests
    {
        [TestMethod]
        public void WebApiEntryAdapter_UsesRequestId()
        {
            var adapter = new WebApiEntryAdapter();
            var request = new PluginInvokeRequest { RequestId = "abc123" };

            var result = adapter.Build(request);

            Assert.AreEqual("abc123", result.RequestId);
        }

        [TestMethod]
        public void ProfilerEntryAdapter_HandlesNullRequest()
        {
            var adapter = new ProfilerEntryAdapter();

            var result = adapter.Build(null!);

            Assert.AreEqual(string.Empty, result.RequestId);
        }
    }
}
#endif
