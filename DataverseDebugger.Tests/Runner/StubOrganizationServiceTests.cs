#if NET48
using System;
using System.Collections.Generic;
using System.Linq;
using DataverseDebugger.Runner;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class StubOrganizationServiceTests
    {
        [TestMethod]
        public void StubOrganizationService_LogsOperations()
        {
            var entries = new List<string>();
            var service = new StubOrganizationService(entries.Add);

            var entity = new Entity("account") { ["name"] = "Test" };
            var id = service.Create(entity);
            service.Update(entity);
            service.Delete("account", id);
            service.Retrieve("account", id, new ColumnSet(true));
            service.RetrieveMultiple(new QueryExpression("account"));
            service.Execute(new OrganizationRequest { RequestName = "DoStuff" });
            service.Associate("account", id, new Relationship("rel"), new EntityReferenceCollection());
            service.Disassociate("account", id, new Relationship("rel"), new EntityReferenceCollection());

            Assert.IsTrue(entries.Any(e => e.Contains("Create")));
            Assert.IsTrue(entries.Any(e => e.Contains("Execute")));
            Assert.IsTrue(entries.Any(e => e.Contains("Associate")));
        }
    }
}
#endif
