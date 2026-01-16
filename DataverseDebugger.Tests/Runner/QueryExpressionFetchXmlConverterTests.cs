#if NET48
using DataverseDebugger.Runner;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class QueryExpressionFetchXmlConverterTests
    {
        [TestMethod]
        public void TryConvert_ReturnsFalseOnMissingEntity()
        {
            var query = new QueryExpression();

            var success = QueryExpressionFetchXmlConverter.TryConvert(query, out var fetchXml);

            Assert.IsFalse(success);
            Assert.AreEqual(string.Empty, fetchXml);
        }

        [TestMethod]
        public void TryConvert_BuildsBasicFetchXml()
        {
            var query = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("name"),
                Distinct = true,
                TopCount = 5,
                Criteria = new FilterExpression(LogicalOperator.And)
            };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, "Contoso");
            query.Orders.Add(new OrderExpression("name", OrderType.Descending));

            var success = QueryExpressionFetchXmlConverter.TryConvert(query, out var fetchXml);

            Assert.IsTrue(success);
            Assert.IsTrue(fetchXml.Contains("distinct=\"true\""));
            Assert.IsTrue(fetchXml.Contains("top=\"5\""));
            Assert.IsTrue(fetchXml.Contains("<attribute name=\"name\" />"));
            Assert.IsTrue(fetchXml.Contains("<order attribute=\"name\" descending=\"true\""));
            Assert.IsTrue(fetchXml.Contains("condition attribute=\"name\" operator=\"eq\""));
        }

        [TestMethod]
        public void TryConvert_AllColumnsRendersAllAttributes()
        {
            var query = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet(true)
            };

            var success = QueryExpressionFetchXmlConverter.TryConvert(query, out var fetchXml);

            Assert.IsTrue(success);
            Assert.IsTrue(fetchXml.Contains("<all-attributes />"));
        }
    }
}
#endif
