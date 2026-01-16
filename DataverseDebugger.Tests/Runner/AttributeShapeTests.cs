#if NET48
using System;
using DataverseDebugger.Runner;
using Microsoft.Xrm.Sdk.Metadata;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class AttributeShapeTests
    {
        [TestMethod]
        public void FromMetadata_MapsValues()
        {
            var metadata = new AttributeMetadata { LogicalName = "name" };

            var shape = AttributeShape.FromMetadata(metadata);

            Assert.AreEqual("name", shape.LogicalName);
            Assert.IsNull(shape.AttributeType);
            Assert.IsNull(shape.AttributeTypeName);
        }

        [TestMethod]
        public void FromMetadata_ThrowsOnNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => AttributeShape.FromMetadata(null!));
        }
    }
}
#endif
