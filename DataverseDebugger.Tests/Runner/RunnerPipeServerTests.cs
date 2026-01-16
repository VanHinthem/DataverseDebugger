#if NET48
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Reflection;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    [DoNotParallelize]
    public sealed class RunnerPipeServerTests
    {
        [TestMethod]
        public void ParseEntityFromJson_ConvertsKnownTypes()
        {
            var map = new Dictionary<string, AttributeShape>(StringComparer.OrdinalIgnoreCase)
            {
                ["flag"] = new AttributeShape { LogicalName = "flag", AttributeType = AttributeTypeCode.Boolean },
                ["count"] = new AttributeShape { LogicalName = "count", AttributeType = AttributeTypeCode.Integer },
                ["big"] = new AttributeShape { LogicalName = "big", AttributeType = AttributeTypeCode.BigInt },
                ["ratio"] = new AttributeShape { LogicalName = "ratio", AttributeType = AttributeTypeCode.Double },
                ["amount"] = new AttributeShape { LogicalName = "amount", AttributeType = AttributeTypeCode.Decimal },
                ["money"] = new AttributeShape { LogicalName = "money", AttributeType = AttributeTypeCode.Money },
                ["status"] = new AttributeShape { LogicalName = "status", AttributeType = AttributeTypeCode.Status },
                ["when"] = new AttributeShape { LogicalName = "when", AttributeType = AttributeTypeCode.DateTime },
                ["idval"] = new AttributeShape { LogicalName = "idval", AttributeType = AttributeTypeCode.Uniqueidentifier },
                ["multi"] = new AttributeShape { LogicalName = "multi", AttributeType = AttributeTypeCode.Virtual, AttributeTypeName = "MultiSelectPicklistType" },
                ["target"] = new AttributeShape { LogicalName = "target", AttributeType = AttributeTypeCode.Lookup }
            };

            var resolver = CreateResolver("account", map);

            var id = Guid.NewGuid();
            var guidValue = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var json = "{" +
                       "\"logicalName\":\"account\"," +
                       "\"id\":\"" + id + "\"," +
                       "\"flag\":true," +
                       "\"count\":\"42\"," +
                       "\"big\":\"900\"," +
                       "\"ratio\":\"1.5\"," +
                       "\"amount\":\"12.34\"," +
                       "\"money\":\"9.99\"," +
                       "\"status\":\"2\"," +
                       "\"when\":\"2024-01-02T03:04:05Z\"," +
                       "\"idval\":\"" + guidValue + "\"," +
                       "\"multi\":\"1,2\"," +
                       "\"target\":{\"id\":\"" + targetId + "\",\"logicalName\":\"contact\"}," +
                       "\"name\":\"Contoso\"" +
                       "}";

            var entity = RunnerPipeServer.ParseEntityFromJson(json, "account", Guid.Empty, resolver, null, null);

            Assert.IsNotNull(entity);
            Assert.AreEqual(id, entity.Id);
            Assert.AreEqual(true, entity.GetAttributeValue<bool>("flag"));
            Assert.AreEqual(42, entity.GetAttributeValue<int>("count"));
            Assert.AreEqual(900L, entity.GetAttributeValue<long>("big"));
            Assert.AreEqual(1.5d, entity.GetAttributeValue<double>("ratio"), 0.0001d);
            Assert.AreEqual(12.34m, entity.GetAttributeValue<decimal>("amount"));
            Assert.AreEqual(9.99m, ((Money)entity["money"]).Value);
            Assert.AreEqual(2, ((OptionSetValue)entity["status"]).Value);
            Assert.AreEqual(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), entity.GetAttributeValue<DateTime>("when"));
            Assert.AreEqual(guidValue, entity.GetAttributeValue<Guid>("idval"));

            var collection = (OptionSetValueCollection)entity["multi"];
            Assert.AreEqual(2, collection.Count);

            var target = (EntityReference)entity["target"];
            Assert.AreEqual("contact", target.LogicalName);
            Assert.AreEqual(targetId, target.Id);

            Assert.AreEqual("Contoso", entity.GetAttributeValue<string>("name"));
        }

        [TestMethod]
        public void TryParseGuid_ReturnsEmptyForInvalid()
        {
            var value = Guid.NewGuid();

            Assert.AreEqual(value, RunnerPipeServer.TryParseGuid(value.ToString()));
            Assert.AreEqual(Guid.Empty, RunnerPipeServer.TryParseGuid("not-a-guid"));
        }

        [TestMethod]
        public void FormatUrlForLog_TruncatesLongValues()
        {
            var longUrl = "https://example.com/" + new string('a', 210);

            var result = InvokePrivateStatic<string>("FormatUrlForLog", longUrl);

            Assert.IsTrue(result.EndsWith("..."));
            Assert.IsTrue(result.Length <= 203);
            Assert.AreEqual("(unknown url)", InvokePrivateStatic<string>("FormatUrlForLog", " "));
        }

        [TestMethod]
        public void BuildRequestSummary_UsesHeaders()
        {
            var request = new InterceptedHttpRequest
            {
                Method = "GET",
                Url = "https://example.com/api/data/v9.2/accounts",
                Headers = new Dictionary<string, List<string>>
                {
                    ["X-MS-CLIENT-REQUEST-ID"] = new List<string> { "client-1" }
                }
            };

            var summary = InvokePrivateStatic<string>("BuildRequestSummary", request, "req-1");

            Assert.IsTrue(summary.Contains("GET"));
            Assert.IsTrue(summary.Contains("client-1"));
            Assert.IsTrue(summary.Contains("req-1"));
        }

        [TestMethod]
        public void BuildRequestSummary_ReturnsFallbackForNull()
        {
            var summary = InvokePrivateStatic<string>("BuildRequestSummary", null!, null!);

            Assert.AreEqual("unknown request", summary);
        }

        [TestMethod]
        public void TryGetHeaderValue_ReturnsNullWhenMissing()
        {
            var headers = new Dictionary<string, List<string>>();

            var value = InvokePrivateStatic<string>("TryGetHeaderValue", headers, "x-ms-client-request-id");

            Assert.IsNull(value);
            Assert.IsNull(InvokePrivateStatic<string>("TryGetHeaderValue", null!, "x-ms-client-request-id"));
        }

        [TestMethod]
        public void TryGetHeaderValue_ReturnsFirstMatch()
        {
            var headers = new Dictionary<string, List<string>>
            {
                ["x-ms-client-request-id"] = new List<string> { "client-2" }
            };

            var value = InvokePrivateStatic<string>("TryGetHeaderValue", headers, "x-ms-client-request-id");

            Assert.AreEqual("client-2", value);
        }

        [TestMethod]
        public void BuildNameValueCollection_CopiesHeaderValues()
        {
            var headers = new Dictionary<string, List<string>>
            {
                ["X-Test"] = new List<string> { "a", "b" },
                ["Empty"] = new List<string>()
            };

            var collection = RunnerPipeServer.BuildNameValueCollection(headers);

            CollectionAssert.AreEqual(new[] { "a", "b" }, collection.GetValues("X-Test"));
            Assert.AreEqual(string.Empty, collection["Empty"]);
        }

        [TestMethod]
        public void GetParameter_ReturnsStoredValue()
        {
            var parameters = new ParameterCollection
            {
                ["name"] = "value"
            };

            Assert.AreEqual("value", RunnerPipeServer.GetParameter(parameters, "name"));
            Assert.IsNull(RunnerPipeServer.GetParameter(parameters, "missing"));
            Assert.IsNull(RunnerPipeServer.GetParameter(null!, "name"));
        }

        [TestMethod]
        public void BuildHealthResponse_ReturnsReadyCapabilities()
        {
            var response = InvokePrivateStatic<HealthCheckResponse>("BuildHealthResponse");

            Assert.AreEqual(HealthStatus.Ready, response.Status);
            Assert.IsTrue(response.Capabilities.HasFlag(CapabilityFlags.TraceStreaming));
            Assert.IsTrue(response.Capabilities.HasFlag(CapabilityFlags.StepCatalog));
        }

        [TestMethod]
        public void ValidateWorkspace_ReportsMissingOrgUrl()
        {
            var request = new InitializeWorkspaceRequest
            {
                Environment = new EnvConfig { OrgUrl = string.Empty },
                Workspace = new PluginWorkspaceManifest()
            };

            var response = InvokePrivateStatic<InitializeWorkspaceResponse>("ValidateWorkspace", request);

            Assert.AreEqual(HealthStatus.Error, response.Status);
            Assert.IsNotNull(response.Message);
            Assert.IsTrue(response.Message!.Contains("OrgUrl"));
        }

        [TestMethod]
        public void ValidateWorkspace_AllowsEmptyAssemblies()
        {
            var request = new InitializeWorkspaceRequest
            {
                Environment = new EnvConfig { OrgUrl = "https://example.crm.dynamics.com" },
                Workspace = new PluginWorkspaceManifest()
            };

            var response = InvokePrivateStatic<InitializeWorkspaceResponse>("ValidateWorkspace", request);

            Assert.AreEqual(HealthStatus.Ready, response.Status);
            Assert.IsNotNull(response.Message);
            Assert.IsTrue(response.Message!.Contains("Workspace validated"));
        }

        [TestMethod]
        public void ValidateWorkspace_ReportsMissingAssemblyFile()
        {
            var request = new InitializeWorkspaceRequest
            {
                Environment = new EnvConfig { OrgUrl = "https://example.crm.dynamics.com" },
                Workspace = new PluginWorkspaceManifest
                {
                    Assemblies = new List<PluginAssemblyRef>
                    {
                        new PluginAssemblyRef { Path = "missing.dll" }
                    }
                }
            };

            var response = InvokePrivateStatic<InitializeWorkspaceResponse>("ValidateWorkspace", request);

            Assert.AreEqual(HealthStatus.Error, response.Status);
            Assert.IsNotNull(response.Message);
            Assert.IsTrue(response.Message!.Contains("Assembly not found"));
        }

        private static AttributeMetadataResolver CreateResolver(string logicalName, Dictionary<string, AttributeShape> map)
        {
            var resolver = new AttributeMetadataResolver(null, null, null, new HttpClient(), null);
            var field = typeof(AttributeMetadataResolver).GetField("_localCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var cache = (Dictionary<string, Dictionary<string, AttributeShape>>)field.GetValue(resolver);
            cache[logicalName] = map;
            return resolver;
        }

        private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
        {
            var method = typeof(RunnerPipeServer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            return (T)method.Invoke(null, args);
        }
    }
}
#endif
