using DataverseDebugger.Protocol;

namespace DataverseDebugger.Tests.Protocol
{
    [TestClass]
    public sealed class OperationParameterTypeMapperTests
    {
        [TestMethod]
        public void FromParser_MapsKnownTypes()
        {
            var result = OperationParameterTypeMapper.FromParser("System.String, mscorlib");

            Assert.AreEqual(OperationParameterType.String, result);
        }

        [TestMethod]
        public void FromFormattedActionType_NormalizesLabels()
        {
            var result = OperationParameterTypeMapper.FromFormattedActionType("Date and Time");

            Assert.AreEqual(OperationParameterType.DateTime, result);
        }

        [TestMethod]
        public void FromFormattedActionType_UnknownReturnsNull()
        {
            var result = OperationParameterTypeMapper.FromFormattedActionType("NotAType");

            Assert.IsNull(result);
        }
    }
}
