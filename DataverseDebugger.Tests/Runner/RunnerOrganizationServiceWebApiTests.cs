#if NET48
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DataverseDebugger.Runner;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class RunnerOrganizationServiceWebApiTests
    {
        [TestMethod]
        public void Retrieve_ParsesWebApiResponseWithTypedAttributes()
        {
            var handler = new TestHttpMessageHandler(request =>
            {
                var json = "{" +
                           "\"Id\":\"4d4f3120-5d1c-4b34-9ba2-96f0f37e5f2b\"," +
                           "\"name\":\"Contoso\"," +
                           "\"revenue\":12.5," +
                           "\"statuscode\":2," +
                           "\"createdon\":\"2024-01-02T03:04:05Z\"," +
                           "\"_ownerid_value\":\"d2719c3a-1a43-4a61-8c72-88cc5d658b17\"," +
                           "\"ownerid@Microsoft.Dynamics.CRM.lookuplogicalname\":\"systemuser\"" +
                           "}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

            var attributeMap = new Dictionary<string, AttributeShape>(StringComparer.OrdinalIgnoreCase)
            {
                ["ownerid"] = new AttributeShape { LogicalName = "ownerid", AttributeType = AttributeTypeCode.Owner },
                ["revenue"] = new AttributeShape { LogicalName = "revenue", AttributeType = AttributeTypeCode.Money },
                ["statuscode"] = new AttributeShape { LogicalName = "statuscode", AttributeType = AttributeTypeCode.Status },
                ["createdon"] = new AttributeShape { LogicalName = "createdon", AttributeType = AttributeTypeCode.DateTime }
            };

            var resolver = CreateResolver("account", attributeMap);
            var httpClient = new HttpClient(handler);
            var service = CreateService(httpClient, resolver);

            var entity = service.Retrieve("account", Guid.NewGuid(), new ColumnSet("ownerid", "revenue", "statuscode", "createdon"));

            Assert.AreEqual("Contoso", entity.GetAttributeValue<string>("name"));
            Assert.AreEqual(12.5m, ((Money)entity["revenue"]).Value);
            Assert.AreEqual(2, ((OptionSetValue)entity["statuscode"]).Value);
            Assert.AreEqual("2024-01-02T03:04:05Z", entity.GetAttributeValue<string>("createdon"));

            var owner = (EntityReference)entity["ownerid"];
            Assert.AreEqual("systemuser", owner.LogicalName);

            var request = handler.LastRequest;
            Assert.IsNotNull(request);
            var url = request!.RequestUri.ToString();
            Assert.IsTrue(url.Contains("$select=_ownerid_value%2Crevenue%2Cstatuscode%2Ccreatedon"));
        }

        [TestMethod]
        public void Execute_WhoAmI_ParsesIds()
        {
            var handler = new TestHttpMessageHandler(_ =>
            {
                var json = "{" +
                           "\"UserId\":\"c3c5c488-1b1c-45b6-9b71-85dcd9e14c6a\"," +
                           "\"BusinessUnitId\":\"72a9c645-2b0b-4f39-8ee6-ea30e8e6a6f1\"," +
                           "\"OrganizationId\":\"1a97f8c3-50c9-4bb6-8c4f-8b2e89c4a7d4\"" +
                           "}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

            var service = CreateService(new HttpClient(handler), resolver: null);

            var response = service.Execute(new WhoAmIRequest());

            Assert.IsTrue(response.Results.Contains("UserId"));
            Assert.IsTrue(response.Results.Contains("BusinessUnitId"));
            Assert.IsTrue(response.Results.Contains("OrganizationId"));
        }

        private static RunnerOrganizationService CreateService(HttpClient httpClient, AttributeMetadataResolver? resolver)
        {
            return new RunnerOrganizationService(
                _ => { },
                httpClient,
                orgUrl: "https://example.crm.dynamics.com",
                accessToken: "token",
                writeMode: RunnerWriteMode.FakeWrites,
                entitySetResolver: name => name + "s",
                attributeResolver: resolver,
                liveService: null);
        }

        private static AttributeMetadataResolver CreateResolver(string logicalName, Dictionary<string, AttributeShape> map)
        {
            var resolver = new AttributeMetadataResolver(null, null, null, new HttpClient(), null);
            var field = typeof(AttributeMetadataResolver).GetField("_localCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = (Dictionary<string, Dictionary<string, AttributeShape>>)field.GetValue(resolver);
            cache[logicalName] = map;
            return resolver;
        }

        private sealed class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(_handler(request));
            }
        }
    }
}
#endif
