#if NET48
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner;
using Microsoft.Xrm.Sdk.Metadata;

namespace DataverseDebugger.Tests.Runner
{
    [TestClass]
    public sealed class FullEntityMetadataCacheTests
    {
        [TestMethod]
        public void TryGetAttributes_LoadsFromDiskCache()
        {
            var tempRoot = CreateTempRoot();
            try
            {
                var env = new EnvConfig
                {
                    Name = "TestEnv",
                    OrgUrl = "https://example.crm.dynamics.com",
                    EntityMetadataCacheRoot = tempRoot
                };

                var cacheDir = Path.Combine(tempRoot, "entityMetadata");
                Directory.CreateDirectory(cacheDir);
                var filePath = Path.Combine(cacheDir, "account.json");
                var cached = new
                {
                    LogicalName = "account",
                    Attributes = new[]
                    {
                        new { LogicalName = "name", AttributeType = "String", AttributeTypeName = "StringType" }
                    },
                    CachedOnUtc = DateTime.UtcNow,
                    Version = 1
                };
                File.WriteAllText(filePath, JsonSerializer.Serialize(cached));

                var trace = new List<string>();
                var httpClient = new HttpClient(new TestHttpMessageHandler(_ =>
                    new HttpResponseMessage(HttpStatusCode.InternalServerError)));

                var map = FullEntityMetadataCache.TryGetAttributes("account", env, null, httpClient, trace);

                Assert.IsNotNull(map);
                Assert.IsTrue(map.ContainsKey("name"));
                Assert.AreEqual(AttributeTypeCode.String, map["name"].AttributeType);
                Assert.AreEqual("StringType", map["name"].AttributeTypeName);
            }
            finally
            {
                CleanupTempRoot(tempRoot);
            }
        }

        [TestMethod]
        public void TryGetAttributes_FetchesFromWebApi()
        {
            var tempRoot = CreateTempRoot();
            try
            {
                var env = new EnvConfig
                {
                    OrgUrl = "https://example.crm.dynamics.com",
                    EntityMetadataCacheRoot = tempRoot
                };

                var json = "{\"Attributes\":[" +
                           "{\"LogicalName\":\"name\",\"AttributeType\":\"String\",\"AttributeTypeName\":{\"Value\":\"StringType\"}}," +
                           "{\"LogicalName\":\"options\",\"AttributeType\":\"Virtual\",\"AttributeTypeName\":{\"Value\":\"MultiSelectPicklistType\"}}" +
                           "]}";

                var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
                var httpClient = new HttpClient(handler);
                var trace = new List<string>();

                var map = FullEntityMetadataCache.TryGetAttributes("account", env, "token", httpClient, trace);

                Assert.IsNotNull(map);
                Assert.IsTrue(map.ContainsKey("name"));
                Assert.AreEqual(AttributeTypeCode.String, map["name"].AttributeType);
                Assert.IsTrue(File.Exists(Path.Combine(tempRoot, "entityMetadata", "account.json")));
            }
            finally
            {
                CleanupTempRoot(tempRoot);
            }
        }

        private static string CreateTempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "DataverseDebuggerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void CleanupTempRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch
            {
            }
        }

        private sealed class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                return System.Threading.Tasks.Task.FromResult(_handler(request));
            }
        }
    }
}
#endif
