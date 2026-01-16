#if NET48
using System.Net.Http;
using DataverseDebugger.Runner;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class RunnerOrganizationServiceTests
    {
        [TestMethod]
        public void Create_StoresOverlayAndRetrieves()
        {
            var service = CreateService();
            var entity = new Entity("account");
            entity["name"] = "Contoso";

            var id = service.Create(entity);
            var retrieved = service.Retrieve("account", id, new ColumnSet("name"));

            Assert.AreEqual(id, retrieved.Id);
            Assert.AreEqual("Contoso", retrieved["name"]);
        }

        [TestMethod]
        public void Retrieve_RespectsColumnSet()
        {
            var service = CreateService();
            var entity = new Entity("account");
            entity["name"] = "Contoso";
            entity["number"] = "A1";

            var id = service.Create(entity);
            var retrieved = service.Retrieve("account", id, new ColumnSet("name"));

            Assert.IsTrue(retrieved.Attributes.ContainsKey("name"));
            Assert.IsFalse(retrieved.Attributes.ContainsKey("number"));
        }

        [TestMethod]
        public void RetrieveMultiple_ReturnsOverlayEntities()
        {
            var service = CreateService();
            service.Create(new Entity("account") { ["name"] = "First" });
            service.Create(new Entity("account") { ["name"] = "Second" });

            var query = new QueryExpression("account") { ColumnSet = new ColumnSet(true) };
            var results = service.RetrieveMultiple(query);

            Assert.AreEqual(2, results.Entities.Count);
            Assert.AreEqual("account", results.EntityName);
        }

        [TestMethod]
        public void Delete_RemovesOverlayAndThrowsOnRetrieve()
        {
            var service = CreateService();
            var id = service.Create(new Entity("account") { ["name"] = "DeleteMe" });

            service.Delete("account", id);

            Assert.ThrowsException<InvalidOperationException>(() =>
                service.Retrieve("account", id, new ColumnSet("name")));
        }

        private static RunnerOrganizationService CreateService()
        {
            return new RunnerOrganizationService(
                _ => { },
                new HttpClient(),
                orgUrl: null,
                accessToken: null,
                writeMode: RunnerWriteMode.FakeWrites,
                entitySetResolver: name => name + "s",
                attributeResolver: null,
                liveService: null);
        }
    }
}
#endif
