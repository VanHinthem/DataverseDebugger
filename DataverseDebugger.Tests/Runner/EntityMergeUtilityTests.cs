#if NET48
using DataverseDebugger.Runner.Services.Hybrid;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class EntityMergeUtilityTests
    {
        [TestMethod]
        public void Merge_UsesOverlayValues()
        {
            var baseEntity = new Entity("account") { Id = Guid.NewGuid() };
            baseEntity["name"] = "base";
            var overlay = new Entity("account") { Id = Guid.NewGuid() };
            overlay["name"] = "overlay";

            var merged = EntityMergeUtility.Merge(baseEntity, overlay);

            Assert.AreEqual("overlay", merged["name"]);
            Assert.AreEqual(baseEntity.LogicalName, merged.LogicalName);
            Assert.AreEqual(baseEntity.Id, merged.Id);
        }

        [TestMethod]
        public void Merge_FillsMissingLogicalName()
        {
            var baseEntity = new Entity { Id = Guid.NewGuid() };
            var overlay = new Entity("contact") { Id = Guid.NewGuid() };

            var merged = EntityMergeUtility.Merge(baseEntity, overlay);

            Assert.AreEqual("contact", merged.LogicalName);
        }
    }
}
#endif
