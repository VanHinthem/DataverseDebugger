#if NET48
using System.Collections.Specialized;
using DataverseDebugger.Runner.Conversion.Model;

namespace DataverseDebugger.Tests.Runner.Conversion
{
    [TestClass]
    public sealed class WebApiRequestTests
    {
        [TestMethod]
        public void CreateFromLocalPathWithQuery_RejectsNonWebApiPath()
        {
            var result = WebApiRequest.CreateFromLocalPathWithQuery("GET", "/api/data/v8.2/accounts", new NameValueCollection());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Create_AllowsRelativeBatchPaths()
        {
            var headers = new NameValueCollection();
            var result = WebApiRequest.Create("GET", "api/data/v9.2/accounts", headers);

            Assert.IsNotNull(result);
            Assert.AreEqual("/api/data/v9.2/accounts", result.LocalPathWithQuery);
        }
    }
}
#endif
