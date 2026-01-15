#if NET48
using System.Collections.Generic;
using DataverseDebugger.Runner;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class StubServiceProviderTests
    {
        [TestMethod]
        public void GetService_ReturnsExactMatch()
        {
            var service = new object();
            var provider = new StubServiceProvider(new Dictionary<System.Type, object>
            {
                { typeof(object), service }
            });

            Assert.AreSame(service, provider.GetService(typeof(object)));
        }

        [TestMethod]
        public void GetService_ReturnsAssignableInstance()
        {
            var service = new List<string>();
            var provider = new StubServiceProvider(new Dictionary<System.Type, object>
            {
                { typeof(List<string>), service }
            });

            Assert.AreSame(service, provider.GetService(typeof(IList<string>)));
        }
    }
}
#endif
