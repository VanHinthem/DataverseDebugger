#if NET48
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Abstractions;
using DataverseDebugger.Runner.Conversion.Model;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Tests.Runner;

[TestClass]
public sealed class RunnerNet48CoverageTests
{
    private static readonly Assembly RunnerAssembly = Assembly.Load("DataverseDebugger.Runner");

    [TestMethod]
    public void RunnerPluginExecutionContext_Defaults_AreInitialized()
    {
        var contextType = GetRunnerType(
            "DataverseDebugger.Runner.ExecutionContext.RunnerPluginExecutionContext");
        var context = Activator.CreateInstance(contextType, nonPublic: true);

        Assert.IsNotNull(context);
        Assert.IsNotNull(contextType.GetProperty("InputParameters")?.GetValue(context));
        Assert.IsNotNull(contextType.GetProperty("OutputParameters")?.GetValue(context));
        Assert.IsNotNull(contextType.GetProperty("SharedVariables")?.GetValue(context));
        Assert.IsNotNull(contextType.GetProperty("PreEntityImages")?.GetValue(context));
        Assert.IsNotNull(contextType.GetProperty("PostEntityImages")?.GetValue(context));

        var correlationId = (Guid)contextType.GetProperty("CorrelationId")?.GetValue(context)!;
        var operationId = (Guid)contextType.GetProperty("OperationId")?.GetValue(context)!;
        var requestId = (Guid?)contextType.GetProperty("RequestId")?.GetValue(context);

        Assert.AreNotEqual(Guid.Empty, correlationId);
        Assert.AreNotEqual(Guid.Empty, operationId);
        Assert.IsNotNull(requestId);
        Assert.AreNotEqual(Guid.Empty, requestId!.Value);
    }

    [TestMethod]
    public void RunnerNotSupportedException_MessageIncludesParts()
    {
        var exceptionType = GetRunnerType(
            "DataverseDebugger.Runner.Abstractions.RunnerNotSupportedException");

        var exception = (Exception)Activator.CreateInstance(
            exceptionType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { "Online", "Execute", "Use Offline mode." },
            culture: null)!;

        StringAssert.Contains(exception.Message, "Mode=Online");
        StringAssert.Contains(exception.Message, "Operation=Execute");
        StringAssert.Contains(exception.Message, "Guidance=Use Offline mode.");
    }

    [TestMethod]
    public void RunnerExecutionOptions_DefaultsToLiveWritesDisabled()
    {
        var optionsType = GetRunnerType(
            "DataverseDebugger.Runner.Configuration.RunnerExecutionOptions");
        var options = Activator.CreateInstance(optionsType, nonPublic: true);
        var allowLiveWrites = (bool)(optionsType.GetProperty("AllowLiveWrites")?.GetValue(options) ?? true);

        Assert.IsFalse(allowLiveWrites);
    }

    [TestMethod]
    public void RunnerExecutionOptions_FromEnvironment_ParsesAllowLiveWrites()
    {
        var optionsType = GetRunnerType(
            "DataverseDebugger.Runner.Configuration.RunnerExecutionOptions");
        var envVarField = optionsType.GetField("AllowLiveWritesEnvVar", BindingFlags.Public | BindingFlags.Static);
        var envVar = (string)envVarField!.GetValue(null)!;
        var originalValue = Environment.GetEnvironmentVariable(envVar);

        try
        {
            Environment.SetEnvironmentVariable(envVar, "1");
            var fromEnvironment = optionsType.GetMethod("FromEnvironment", BindingFlags.Public | BindingFlags.Static);
            var options = fromEnvironment!.Invoke(null, null);
            var allowLiveWrites = (bool)(optionsType.GetProperty("AllowLiveWrites")?.GetValue(options) ?? false);
            Assert.IsTrue(allowLiveWrites);

            Environment.SetEnvironmentVariable(envVar, "0");
            options = fromEnvironment.Invoke(null, null);
            allowLiveWrites = (bool)(optionsType.GetProperty("AllowLiveWrites")?.GetValue(options) ?? true);
            Assert.IsFalse(allowLiveWrites);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, originalValue);
        }
    }

    [TestMethod]
    public void RunnerConversion_WebApiRequestFactoryCreatesRequest()
    {
        var headers = new NameValueCollection();
        var request = WebApiRequest.CreateFromLocalPathWithQuery(
            "GET",
            "/api/data/v9.2/accounts",
            headers);

        Assert.IsNotNull(request);
        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("/api/data/v9.2/accounts", request.LocalPathWithQuery);
        Assert.AreSame(headers, request.Headers);
    }

    [TestMethod]
    public void ExecutionRequest_AllowsRequestIdSet()
    {
        var requestType = GetRunnerType("DataverseDebugger.Runner.Pipeline.ExecutionRequest");
        var request = Activator.CreateInstance(requestType, nonPublic: true);
        var requestIdProperty = requestType.GetProperty("RequestId");

        Assert.AreEqual(string.Empty, requestIdProperty?.GetValue(request));

        requestIdProperty?.SetValue(request, "req-1");
        Assert.AreEqual("req-1", requestIdProperty?.GetValue(request));
    }

    [TestMethod]
    public void ExecutionResult_DefaultsAndErrorAreSettable()
    {
        var resultType = GetRunnerType("DataverseDebugger.Runner.Pipeline.ExecutionResult");
        var result = Activator.CreateInstance(resultType, nonPublic: true);

        var traceLines = (IList)resultType.GetProperty("TraceLines")?.GetValue(result)!;
        Assert.IsNotNull(traceLines);
        Assert.AreEqual(0, traceLines.Count);

        traceLines.Add("line");
        Assert.AreEqual(1, traceLines.Count);

        var errorProperty = resultType.GetProperty("Error");
        Assert.IsNull(errorProperty?.GetValue(result));

        var error = new InvalidOperationException("boom");
        errorProperty?.SetValue(result, error);
        Assert.AreSame(error, errorProperty?.GetValue(result));
    }

    [TestMethod]
    public void ExecutionModeResolver_ResolvesExplicitModes()
    {
        var resolverType = GetRunnerType("DataverseDebugger.Runner.Pipeline.ExecutionModeResolver");
        var resolve = resolverType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);

        var hybridRequest = new PluginInvokeRequest { ExecutionMode = "hyBrid" };
        var hybridResolved = resolve!.Invoke(null, new object[] { hybridRequest });
        Assert.AreEqual("Hybrid", hybridResolved?.ToString());

        var onlineRequest = new PluginInvokeRequest { ExecutionMode = "ONLINE" };
        var onlineResolved = resolve.Invoke(null, new object[] { onlineRequest });
        Assert.AreEqual("Online", onlineResolved?.ToString());
    }

    [TestMethod]
    public void ExecutionModeResolver_FallsBackToWriteMode()
    {
        var resolverType = GetRunnerType("DataverseDebugger.Runner.Pipeline.ExecutionModeResolver");
        var resolve = resolverType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);

        var liveRequest = new PluginInvokeRequest { WriteMode = "LiveWrites" };
        var liveResolved = resolve!.Invoke(null, new object[] { liveRequest });
        Assert.AreEqual("Online", liveResolved?.ToString());

        var fakeRequest = new PluginInvokeRequest { WriteMode = "FakeWrites" };
        var fakeResolved = resolve.Invoke(null, new object[] { fakeRequest });
        Assert.AreEqual("Hybrid", fakeResolved?.ToString());
    }

    [TestMethod]
    public void ExecutionModeResolver_RejectsInvalidMode()
    {
        var resolverType = GetRunnerType("DataverseDebugger.Runner.Pipeline.ExecutionModeResolver");
        var resolve = resolverType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);

        var invalidRequest = new PluginInvokeRequest { ExecutionMode = "NotARealMode" };
        var exception = Assert.ThrowsException<TargetInvocationException>(
            () => resolve!.Invoke(null, new object[] { invalidRequest }));

        var inner = exception.InnerException;
        Assert.IsNotNull(inner);
        var exceptionType = GetRunnerType("DataverseDebugger.Runner.Abstractions.RunnerNotSupportedException");
        Assert.IsInstanceOfType(inner, exceptionType);
        StringAssert.Contains(inner!.Message, "Mode=NotARealMode");
        StringAssert.Contains(inner.Message, "Operation=ExecutionMode");
    }

    [TestMethod]
    public void PluginInvocationEngine_ExecutesPluginAndCapturesTrace()
    {
        var assemblyPath = typeof(MinimalTestPlugin).Assembly.Location;
        var workspace = BuildWorkspace(assemblyPath);

        using var httpClient = new HttpClient();
        var engine = CreateEngine(httpClient, workspace);

        var invokeRequest = new PluginInvokeRequest
        {
            RequestId = "req-001",
            Assembly = assemblyPath,
            TypeName = typeof(MinimalTestPlugin).FullName ?? string.Empty,
            MessageName = "Create",
            PrimaryEntityName = "account",
            PrimaryEntityId = Guid.NewGuid().ToString(),
            Stage = 40,
            Mode = 0,
            WriteMode = "FakeWrites"
        };

        var payload = JsonSerializer.Serialize(invokeRequest);
        var response = InvokeEngine(engine, payload, out var parsedRequest);

        Assert.IsNotNull(parsedRequest);
        Assert.AreEqual(invokeRequest.RequestId, parsedRequest!.RequestId);
        Assert.AreEqual(HealthStatus.Ready, response.Status);
        Assert.IsTrue(response.TraceLines.Any(line => line.Contains("MinimalTestPlugin executed")));
        Assert.IsTrue(response.TraceLines.Any(line => line.Contains("Output:Result=Expected")));
    }

    [TestMethod]
    public void PluginInvocationEngine_BlocksOnlineWhenLiveWritesDisabled()
    {
        var assemblyPath = typeof(MinimalTestPlugin).Assembly.Location;
        var workspace = BuildWorkspace(assemblyPath);

        using var httpClient = new HttpClient();
        var engine = CreateEngine(httpClient, workspace, allowLiveWrites: false);

        var invokeRequest = new PluginInvokeRequest
        {
            RequestId = "req-online-blocked",
            Assembly = assemblyPath,
            TypeName = typeof(MinimalTestPlugin).FullName ?? string.Empty,
            ExecutionMode = "Online",
            MessageName = "Create",
            PrimaryEntityName = "account",
            PrimaryEntityId = Guid.NewGuid().ToString(),
            Stage = 40,
            Mode = 0
        };

        var payload = JsonSerializer.Serialize(invokeRequest);
        var response = InvokeEngine(engine, payload, out _);

        Assert.AreEqual(HealthStatus.Error, response.Status);
        StringAssert.Contains(response.Message ?? string.Empty, "Mode=Online");
        StringAssert.Contains(response.Message ?? string.Empty, "AllowLiveWrites=true");
    }

    [TestMethod]
    public void PluginInvocationEngine_PreservesConstructorSelection()
    {
        var assemblyPath = typeof(DualConstructorPlugin).Assembly.Location;
        var workspace = BuildWorkspace(assemblyPath);

        using var httpClient = new HttpClient();
        var engine = CreateEngine(httpClient, workspace);

        var dualRequest = BuildPluginRequest(
            assemblyPath,
            typeof(DualConstructorPlugin),
            "u-1",
            "s-1");
        var dualResponse = InvokeEngine(engine, JsonSerializer.Serialize(dualRequest), out _);

        Assert.AreEqual(HealthStatus.Ready, dualResponse.Status);
        Assert.IsTrue(dualResponse.TraceLines.Any(line => line.Contains("Using plugin constructor (string unsecure, string secure).")));
        Assert.IsTrue(dualResponse.TraceLines.Any(line => line.Contains("Ctor=dual")));
        Assert.IsTrue(dualResponse.TraceLines.Any(line => line.Contains("Unsecure=u-1")));
        Assert.IsTrue(dualResponse.TraceLines.Any(line => line.Contains("Secure=s-1")));

        var singleRequest = BuildPluginRequest(
            assemblyPath,
            typeof(SingleConstructorPlugin),
            "u-2",
            "s-2");
        var singleResponse = InvokeEngine(engine, JsonSerializer.Serialize(singleRequest), out _);

        Assert.AreEqual(HealthStatus.Ready, singleResponse.Status);
        Assert.IsTrue(singleResponse.TraceLines.Any(line => line.Contains("Using plugin constructor (string unsecure).")));
        Assert.IsTrue(singleResponse.TraceLines.Any(line => line.Contains("Ctor=single")));
        Assert.IsTrue(singleResponse.TraceLines.Any(line => line.Contains("Unsecure=u-2")));
    }

    [TestMethod]
    public void PluginInvocationEngine_OfflineExecutesWithSeededTarget()
    {
        var assemblyPath = typeof(OfflineSeedTestPlugin).Assembly.Location;
        var workspace = BuildWorkspace(assemblyPath);

        using var httpClient = new HttpClient();
        var engine = CreateEngine(httpClient, workspace);

        var targetId = Guid.NewGuid();
        var targetJson = JsonSerializer.Serialize(new
        {
            logicalName = "account",
            id = targetId.ToString(),
            name = "OfflineSeed"
        });

        var invokeRequest = new PluginInvokeRequest
        {
            RequestId = "req-offline-seed",
            Assembly = assemblyPath,
            TypeName = typeof(OfflineSeedTestPlugin).FullName ?? string.Empty,
            ExecutionMode = "Offline",
            MessageName = "Create",
            PrimaryEntityName = "account",
            PrimaryEntityId = targetId.ToString(),
            Stage = 40,
            Mode = 0,
            TargetJson = targetJson
        };

        var payload = JsonSerializer.Serialize(invokeRequest);
        var response = InvokeEngine(engine, payload, out _);

        Assert.AreEqual(HealthStatus.Ready, response.Status);
        Assert.IsTrue(response.TraceLines.Any(line => line.Contains("OfflineName=OfflineSeed")));
    }

    [TestMethod]
    public void EntryAdapters_ReturnExecutionRequest()
    {
        var request = new PluginInvokeRequest { RequestId = "adapter-1" };

        Assert.AreEqual(
            "adapter-1",
            GetEntryAdapterRequestId("DataverseDebugger.Runner.EntryAdapters.WebApiEntryAdapter", request));
        Assert.AreEqual(
            "adapter-1",
            GetEntryAdapterRequestId("DataverseDebugger.Runner.EntryAdapters.ProfilerEntryAdapter", request));
    }

    [TestMethod]
    public void RunnerLogger_ReceivesEntriesFromTracingService()
    {
        var loggerType = GetRunnerType("DataverseDebugger.Runner.Logging.RunnerLogger");
        var logger = Activator.CreateInstance(loggerType, nonPublic: true);
        var tracingType = GetRunnerType("DataverseDebugger.Runner.Logging.TracingServiceAdapter");
        var tracing = Activator.CreateInstance(
            tracingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { logger },
            culture: null);

        var traceMethod = tracingType.GetMethod("Trace");
        traceMethod!.Invoke(tracing, new object[] { "Hello {0}", new object[] { "world" } });

        var entries = (IList)loggerType.GetProperty("Entries")?.GetValue(logger)!;
        Assert.AreEqual(1, entries.Count);

        var entry = entries[0];
        var message = entry.GetType().GetProperty("Message")?.GetValue(entry) as string;
        Assert.AreEqual("Hello world", message);
    }

    [TestMethod]
    public void HybridWriteCache_DefaultCollectionsExist()
    {
        var cacheType = GetRunnerType("DataverseDebugger.Runner.Services.Hybrid.HybridWriteCache");
        var cache = Activator.CreateInstance(cacheType, nonPublic: true);

        Assert.IsNotNull(cacheType.GetProperty("Creates")?.GetValue(cache));
        Assert.IsNotNull(cacheType.GetProperty("Updates")?.GetValue(cache));
        Assert.IsNotNull(cacheType.GetProperty("Deletes")?.GetValue(cache));
    }

    [TestMethod]
    public void EntityMergeUtility_MergeAppliesOverlay()
    {
        var utilityType = GetRunnerType("DataverseDebugger.Runner.Services.Hybrid.EntityMergeUtility");
        var merge = utilityType.GetMethod("Merge", BindingFlags.Public | BindingFlags.Static);

        var id = Guid.NewGuid();
        var baseEntity = new Entity("account") { Id = id };
        baseEntity["name"] = "live";
        baseEntity["number"] = 1;

        var overlay = new Entity("account") { Id = id };
        overlay["name"] = "cached";
        overlay["extra"] = "value";

        var merged = (Entity)merge!.Invoke(null, new object[] { baseEntity, overlay })!;

        Assert.AreEqual("cached", merged.GetAttributeValue<string>("name"));
        Assert.AreEqual(1, merged.GetAttributeValue<int>("number"));
        Assert.AreEqual("value", merged.GetAttributeValue<string>("extra"));
    }

    [TestMethod]
    public void OfflineOrganizationService_CRUD_Roundtrip()
    {
        var service = CreateOfflineService();
        var account = new Entity("account");
        account["name"] = "alpha";

        var id = service.Create(account);
        Assert.AreNotEqual(Guid.Empty, id);

        var retrieved = service.Retrieve("account", id, new ColumnSet(true));
        Assert.AreEqual("alpha", retrieved.GetAttributeValue<string>("name"));

        var update = new Entity("account") { Id = id };
        update["name"] = "beta";
        service.Update(update);

        var updated = service.Retrieve("account", id, new ColumnSet(true));
        Assert.AreEqual("beta", updated.GetAttributeValue<string>("name"));

        service.Delete("account", id);
        var afterDelete = service.Retrieve("account", id, new ColumnSet(true));
        Assert.AreEqual(id, afterDelete.Id);
        Assert.IsFalse(afterDelete.Attributes.Contains("name"));
    }

    [TestMethod]
    public void OfflineOrganizationService_RetrieveMultiple_ReturnsStoredEntities()
    {
        var service = CreateOfflineService();
        service.Create(new Entity("account") { ["name"] = "one" });
        service.Create(new Entity("account") { ["name"] = "two" });

        var query = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name")
        };

        var results = service.RetrieveMultiple(query);
        Assert.AreEqual(2, results.Entities.Count);
        Assert.IsTrue(results.Entities.All(entity => entity.Attributes.Contains("name")));
    }

    [TestMethod]
    public void OfflineOrganizationService_Execute_WhoAmIReturnsDefaults()
    {
        var serviceType = GetRunnerType("DataverseDebugger.Runner.Services.Offline.OfflineOrganizationService");
        var service = CreateOfflineService();

        var response = service.Execute(new WhoAmIRequest());

        var expectedUserId = (Guid)serviceType.GetField("DefaultUserId", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var expectedBusinessUnitId = (Guid)serviceType.GetField("DefaultBusinessUnitId", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var expectedOrganizationId = (Guid)serviceType.GetField("DefaultOrganizationId", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.AreEqual(expectedUserId, response.Results["UserId"]);
        Assert.AreEqual(expectedBusinessUnitId, response.Results["BusinessUnitId"]);
        Assert.AreEqual(expectedOrganizationId, response.Results["OrganizationId"]);
    }

    [TestMethod]
    public void OfflineOrganizationService_Execute_RejectsUnsupportedRequests()
    {
        var service = CreateOfflineService();
        try
        {
            service.Execute(new OrganizationRequest("DoSomething"));
            Assert.Fail("Expected NotSupportedException.");
        }
        catch (NotSupportedException ex)
        {
            StringAssert.Contains(ex.Message, "Mode=Offline");
        }
    }

    [TestMethod]
    public void HybridOrganizationService_WriteOperationsNeverHitLive()
    {
        var live = new FakeLiveOrganizationService();
        var hybrid = CreateHybridService(live);

        var entity = new Entity("account");
        hybrid.Create(entity);

        var update = new Entity("account") { Id = entity.Id };
        update["name"] = "cached";
        hybrid.Update(update);

        hybrid.Delete("account", entity.Id);

        Assert.AreEqual(0, live.CreateCalls);
        Assert.AreEqual(0, live.UpdateCalls);
        Assert.AreEqual(0, live.DeleteCalls);
    }

    [TestMethod]
    public void HybridOrganizationService_Retrieve_MergesCacheAndLive()
    {
        var id = Guid.NewGuid();
        var live = new FakeLiveOrganizationService();
        live.OnRetrieve = (name, entityId, columns) =>
        {
            live.RetrieveCalls++;
            var entity = new Entity(name) { Id = entityId };
            entity["name"] = "live";
            entity["description"] = "live-desc";
            return entity;
        };

        var hybrid = CreateHybridService(live);
        var update = new Entity("account") { Id = id };
        update["name"] = "cached";
        hybrid.Update(update);

        var result = hybrid.Retrieve("account", id, new ColumnSet("name", "description"));

        Assert.AreEqual("cached", result.GetAttributeValue<string>("name"));
        Assert.AreEqual("live-desc", result.GetAttributeValue<string>("description"));
        Assert.AreEqual(1, live.RetrieveCalls);

        live.RetrieveCalls = 0;
        var resultNoLive = hybrid.Retrieve("account", id, new ColumnSet("name"));
        Assert.AreEqual("cached", resultNoLive.GetAttributeValue<string>("name"));
        Assert.AreEqual(0, live.RetrieveCalls);
    }

    [TestMethod]
    public void HybridOrganizationService_Retrieve_EmptyColumnSetDoesNotCallLive()
    {
        var id = Guid.NewGuid();
        var live = new FakeLiveOrganizationService();
        live.OnRetrieve = (name, entityId, columns) =>
        {
            live.RetrieveCalls++;
            var entity = new Entity(name) { Id = entityId };
            entity["name"] = "live";
            return entity;
        };

        var hybrid = CreateHybridService(live);
        hybrid.Update(new Entity("account") { Id = id, ["name"] = "cached" });

        var result = hybrid.Retrieve("account", id, new ColumnSet());

        Assert.AreEqual(0, live.RetrieveCalls);
        Assert.AreEqual(0, result.Attributes.Count);
    }

    [TestMethod]
    public void HybridOrganizationService_RetrieveMultiple_OverlaysUpdatesAndDeletes()
    {
        var idUpdated = Guid.NewGuid();
        var idDeleted = Guid.NewGuid();

        var live = new FakeLiveOrganizationService();
        live.OnRetrieveMultiple = _ =>
        {
            live.RetrieveMultipleCalls++;
            var collection = new EntityCollection();
            collection.Entities.Add(new Entity("account") { Id = idUpdated, ["name"] = "live" });
            collection.Entities.Add(new Entity("account") { Id = idDeleted, ["name"] = "to-delete" });
            return collection;
        };

        var hybrid = CreateHybridService(live);
        hybrid.Update(new Entity("account") { Id = idUpdated, ["name"] = "cached" });
        hybrid.Delete("account", idDeleted);

        var query = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name")
        };

        var results = hybrid.RetrieveMultiple(query);

        Assert.AreEqual(1, results.Entities.Count);
        Assert.AreEqual(idUpdated, results.Entities[0].Id);
        Assert.AreEqual("cached", results.Entities[0].GetAttributeValue<string>("name"));
    }

    [TestMethod]
    public void HybridOrganizationService_RetrieveMultiple_EmptyColumnSetDoesNotBackfillLive()
    {
        var id = Guid.NewGuid();
        var live = new FakeLiveOrganizationService();
        live.OnRetrieveMultiple = _ =>
        {
            var collection = new EntityCollection();
            collection.Entities.Add(new Entity("account") { Id = id, ["name"] = "live" });
            return collection;
        };

        var hybrid = CreateHybridService(live);
        hybrid.Update(new Entity("account") { Id = id, ["name"] = "cached" });

        var query = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet()
        };

        var results = hybrid.RetrieveMultiple(query);

        Assert.AreEqual(1, results.Entities.Count);
        Assert.AreEqual(id, results.Entities[0].Id);
        Assert.AreEqual(0, results.Entities[0].Attributes.Count);
    }

    [TestMethod]
    public void HybridOrganizationService_RetrieveMultiple_IncludesCachedCreatesForIdTargetedQueries()
    {
        var id = Guid.NewGuid();
        var live = new FakeLiveOrganizationService
        {
            OnRetrieveMultiple = _ => new EntityCollection()
        };
        var hybrid = CreateHybridService(live);

        var created = new Entity("account") { Id = id };
        created["name"] = "cached";
        hybrid.Create(created);

        var idQuery = BuildIdTargetedQuery("account", id);
        var idResults = hybrid.RetrieveMultiple(idQuery);
        Assert.AreEqual(1, idResults.Entities.Count);
        Assert.AreEqual(id, idResults.Entities[0].Id);

        var nonIdQuery = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name")
        };
        nonIdQuery.Criteria.AddCondition("name", ConditionOperator.Equal, "cached");
        var nonIdResults = hybrid.RetrieveMultiple(nonIdQuery);
        Assert.AreEqual(0, nonIdResults.Entities.Count);
    }

    [TestMethod]
    public void HybridOrganizationService_RetrieveMultiple_FetchXmlIdTargetedIncludesCachedCreate()
    {
        var id = Guid.NewGuid();
        var live = new FakeLiveOrganizationService
        {
            OnRetrieveMultiple = _ => new EntityCollection()
        };
        var hybrid = CreateHybridService(live);

        var created = new Entity("account") { Id = id };
        created["name"] = "cached";
        hybrid.Create(created);

        var fetchXml = $@"
<fetch>
  <entity name=""account"">
    <attribute name=""name"" />
    <filter>
      <condition attribute=""accountid"" operator=""eq"" value=""{id}"" />
    </filter>
  </entity>
</fetch>";

        var results = hybrid.RetrieveMultiple(new FetchExpression(fetchXml));
        Assert.AreEqual(1, results.Entities.Count);
        Assert.AreEqual(id, results.Entities[0].Id);
        Assert.AreEqual("cached", results.Entities[0].GetAttributeValue<string>("name"));
    }

    [TestMethod]
    public void HybridOrganizationService_Execute_WhitelistEnforced()
    {
        var live = new FakeLiveOrganizationService();
        live.OnExecute = request =>
        {
            live.ExecuteCalls++;
            var response = new OrganizationResponse();
            response.Results["UserId"] = Guid.NewGuid();
            return response;
        };
        var hybrid = CreateHybridService(live);

        var whoResponse = hybrid.Execute(new WhoAmIRequest());
        Assert.IsTrue(whoResponse.Results.Contains("UserId"));
        Assert.AreEqual(1, live.ExecuteCalls);

        try
        {
            hybrid.Execute(new OrganizationRequest("DoSomething"));
            Assert.Fail("Expected NotSupportedException.");
        }
        catch (NotSupportedException ex)
        {
            StringAssert.Contains(ex.Message, "Mode=Hybrid");
        }
    }

    [TestMethod]
    public void PluginInvocationEngine_Online_UsesLiveFactory()
    {
        var assemblyPath = typeof(OnlineCreateTestPlugin).Assembly.Location;
        var workspace = BuildWorkspace(assemblyPath);

        var liveService = new FakeLiveOrganizationService();
        liveService.OnCreate = entity =>
        {
            liveService.CreateCalls++;
            return entity.Id != Guid.Empty ? entity.Id : Guid.NewGuid();
        };

        var factory = new FakeLiveOrganizationServiceFactory
        {
            LiveService = liveService
        };

        using var httpClient = new HttpClient();
        var engine = CreateEngineWithFactory(httpClient, workspace, factory, allowLiveWrites: true);

        var invokeRequest = new PluginInvokeRequest
        {
            RequestId = "req-online-factory",
            Assembly = assemblyPath,
            TypeName = typeof(OnlineCreateTestPlugin).FullName ?? string.Empty,
            ExecutionMode = "Online",
            MessageName = "Create",
            PrimaryEntityName = "account",
            PrimaryEntityId = Guid.NewGuid().ToString(),
            Stage = 40,
            Mode = 0,
            OrgUrl = "https://example.crm.dynamics.com",
            AccessToken = "token"
        };

        var payload = JsonSerializer.Serialize(invokeRequest);
        var response = InvokeEngine(engine, payload, out _);

        Assert.AreEqual(HealthStatus.Ready, response.Status);
        Assert.IsTrue(factory.CreateLiveServiceCalls >= 1);
        Assert.IsTrue(liveService.CreateCalls >= 1);
    }

    [TestMethod]
    public void PluginInvocationEngine_Hybrid_UsesLiveFactoryForReads()
    {
        var assemblyPath = typeof(HybridReadTestPlugin).Assembly.Location;
        var workspace = BuildWorkspace(assemblyPath);

        var liveService = new FakeLiveOrganizationService();
        liveService.OnRetrieve = (name, id, columns) =>
        {
            liveService.RetrieveCalls++;
            var entity = new Entity(name) { Id = id };
            entity["name"] = "live";
            return entity;
        };

        var factory = new FakeLiveOrganizationServiceFactory
        {
            LiveService = liveService
        };

        using var httpClient = new HttpClient();
        var engine = CreateEngineWithFactory(httpClient, workspace, factory);

        var invokeRequest = new PluginInvokeRequest
        {
            RequestId = "req-hybrid-factory",
            Assembly = assemblyPath,
            TypeName = typeof(HybridReadTestPlugin).FullName ?? string.Empty,
            ExecutionMode = "Hybrid",
            MessageName = "Retrieve",
            PrimaryEntityName = "account",
            PrimaryEntityId = Guid.NewGuid().ToString(),
            Stage = 40,
            Mode = 0,
            OrgUrl = "https://example.crm.dynamics.com",
            AccessToken = "token"
        };

        var payload = JsonSerializer.Serialize(invokeRequest);
        var response = InvokeEngine(engine, payload, out _);

        Assert.AreEqual(HealthStatus.Ready, response.Status);
        Assert.AreEqual(1, factory.CreateLiveServiceCalls);
        Assert.AreEqual(1, liveService.RetrieveCalls);
        Assert.AreEqual(0, liveService.CreateCalls);
        Assert.AreEqual(0, liveService.UpdateCalls);
        Assert.AreEqual(0, liveService.DeleteCalls);
    }

    [TestMethod]
    public void ContextBuilders_ThrowNotImplemented()
    {
        AssertBuilderThrows("DataverseDebugger.Runner.ExecutionContext.WebApiContextBuilder");
        AssertBuilderThrows("DataverseDebugger.Runner.ExecutionContext.ProfilerContextBuilder");
    }

    private static Type GetRunnerType(string typeName)
    {
        return RunnerAssembly.GetType(typeName, throwOnError: true)!;
    }

    private static PluginWorkspaceManifest BuildWorkspace(string assemblyPath)
    {
        var workspace = new PluginWorkspaceManifest();
        workspace.Assemblies.Add(new PluginAssemblyRef { Path = assemblyPath });
        return workspace;
    }

    private static PluginInvokeRequest BuildPluginRequest(string assemblyPath, Type pluginType, string unsecure, string secure)
    {
        return new PluginInvokeRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Assembly = assemblyPath,
            TypeName = pluginType.FullName ?? string.Empty,
            MessageName = "Create",
            PrimaryEntityName = "account",
            PrimaryEntityId = Guid.NewGuid().ToString(),
            Stage = 40,
            Mode = 0,
            WriteMode = "FakeWrites",
            UnsecureConfiguration = unsecure,
            SecureConfiguration = secure
        };
    }

    private static object CreateEngine(HttpClient httpClient, PluginWorkspaceManifest workspace, bool allowLiveWrites = false)
    {
        var engineType = GetRunnerType("DataverseDebugger.Runner.Pipeline.PluginInvocationEngine");
        Func<PluginWorkspaceManifest?> workspaceAccessor = () => workspace;
        Func<EnvConfig?> envAccessor = () => null;
        var optionsAccessor = CreateOptionsAccessor(allowLiveWrites);
        return Activator.CreateInstance(
            engineType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { httpClient, workspaceAccessor, envAccessor, optionsAccessor },
            culture: null)!;
    }

    private static object CreateEngineWithFactory(
        HttpClient httpClient,
        PluginWorkspaceManifest workspace,
        ILiveOrganizationServiceFactory factory,
        bool allowLiveWrites = false)
    {
        var engineType = GetRunnerType("DataverseDebugger.Runner.Pipeline.PluginInvocationEngine");
        Func<PluginWorkspaceManifest?> workspaceAccessor = () => workspace;
        Func<EnvConfig?> envAccessor = () => null;
        var optionsAccessor = CreateOptionsAccessor(allowLiveWrites);
        return Activator.CreateInstance(
            engineType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { httpClient, workspaceAccessor, envAccessor, optionsAccessor, factory },
            culture: null)!;
    }

    private static object CreateOptionsAccessor(bool allowLiveWrites)
    {
        var optionsType = GetRunnerType("DataverseDebugger.Runner.Configuration.RunnerExecutionOptions");
        var options = Activator.CreateInstance(optionsType, nonPublic: true);
        optionsType.GetProperty("AllowLiveWrites")?.SetValue(options, allowLiveWrites);

        var funcType = typeof(Func<>).MakeGenericType(optionsType);
        var constant = Expression.Constant(options, optionsType);
        return Expression.Lambda(funcType, constant).Compile();
    }

    private static PluginInvokeResponse InvokeEngine(object engine, string payload, out PluginInvokeRequest? parsedRequest)
    {
        var method = engine.GetType().GetMethod("Invoke");
        var args = new object?[] { payload, null };
        var response = (PluginInvokeResponse)method!.Invoke(engine, args)!;
        parsedRequest = args[1] as PluginInvokeRequest;
        return response;
    }

    private static string GetEntryAdapterRequestId(string adapterTypeName, PluginInvokeRequest request)
    {
        var adapterType = GetRunnerType(adapterTypeName);
        var adapter = Activator.CreateInstance(adapterType, nonPublic: true);
        var build = adapterType.GetMethod("Build");
        var executionRequest = build!.Invoke(adapter, new object[] { request });
        var requestId = executionRequest?.GetType().GetProperty("RequestId")?.GetValue(executionRequest) as string;

        return requestId ?? string.Empty;
    }

    private static void AssertBuilderThrows(string builderTypeName)
    {
        var builderType = GetRunnerType(builderTypeName);
        var builder = Activator.CreateInstance(builderType, nonPublic: true);
        var requestType = GetRunnerType("DataverseDebugger.Runner.Pipeline.ExecutionRequest");
        var request = Activator.CreateInstance(requestType, nonPublic: true);
        var build = builderType.GetMethod("Build");

        var exception = Assert.ThrowsException<TargetInvocationException>(
            () => build!.Invoke(builder, new[] { request }));

        Assert.IsInstanceOfType(exception.InnerException, typeof(NotImplementedException));
    }

    private static IOrganizationService CreateOfflineService()
    {
        var serviceType = GetRunnerType("DataverseDebugger.Runner.Services.Offline.OfflineOrganizationService");
        return (IOrganizationService)Activator.CreateInstance(serviceType, nonPublic: true)!;
    }

    private static IOrganizationService CreateHybridService(IOrganizationService? liveService)
    {
        var serviceType = GetRunnerType("DataverseDebugger.Runner.Services.Hybrid.HybridOrganizationService");
        var log = new Action<string>(_ => { });
        return (IOrganizationService)Activator.CreateInstance(
            serviceType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { liveService, log },
            culture: null)!;
    }

    private static QueryExpression BuildIdTargetedQuery(string entityName, params Guid[] ids)
    {
        var query = new QueryExpression(entityName)
        {
            ColumnSet = new ColumnSet("name")
        };

        if (ids.Length == 1)
        {
            query.Criteria.AddCondition(entityName + "id", ConditionOperator.Equal, ids[0]);
        }
        else
        {
            query.Criteria.AddCondition(entityName + "id", ConditionOperator.In, ids.Cast<object>().ToArray());
        }

        return query;
    }
}

internal sealed class FakeLiveOrganizationService : IOrganizationService
{
    public int CreateCalls { get; set; }
    public int UpdateCalls { get; set; }
    public int DeleteCalls { get; set; }
    public int RetrieveCalls { get; set; }
    public int RetrieveMultipleCalls { get; set; }
    public int ExecuteCalls { get; set; }

    public Func<string, Guid, ColumnSet, Entity>? OnRetrieve { get; set; }
    public Func<QueryBase, EntityCollection>? OnRetrieveMultiple { get; set; }
    public Func<OrganizationRequest, OrganizationResponse>? OnExecute { get; set; }
    public Func<Entity, Guid>? OnCreate { get; set; }
    public Action<Entity>? OnUpdate { get; set; }
    public Action<string, Guid>? OnDelete { get; set; }

    public Guid Create(Entity entity)
    {
        CreateCalls++;
        if (OnCreate != null)
        {
            return OnCreate(entity);
        }
        throw new InvalidOperationException("Live create should not be called in Hybrid.");
    }

    public void Update(Entity entity)
    {
        UpdateCalls++;
        if (OnUpdate != null)
        {
            OnUpdate(entity);
            return;
        }
        throw new InvalidOperationException("Live update should not be called in Hybrid.");
    }

    public void Delete(string entityName, Guid id)
    {
        DeleteCalls++;
        if (OnDelete != null)
        {
            OnDelete(entityName, id);
            return;
        }
        throw new InvalidOperationException("Live delete should not be called in Hybrid.");
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
    {
        return OnRetrieve != null
            ? OnRetrieve(entityName, id, columnSet)
            : new Entity(entityName) { Id = id };
    }

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        return OnRetrieveMultiple != null
            ? OnRetrieveMultiple(query)
            : new EntityCollection();
    }

    public OrganizationResponse Execute(OrganizationRequest request)
    {
        return OnExecute != null
            ? OnExecute(request)
            : new OrganizationResponse();
    }

    public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
    {
    }

    public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
    {
    }
}

internal sealed class FakeLiveOrganizationServiceFactory : ILiveOrganizationServiceFactory
{
    public int CreateLiveServiceCalls { get; private set; }
    public IOrganizationService? LiveService { get; set; }
    public IDisposable? Disposable { get; set; }

    public IOrganizationService? CreateLiveService(string? orgUrl, string? accessToken, out IDisposable? disposable)
    {
        CreateLiveServiceCalls++;
        disposable = Disposable;
        return LiveService;
    }
}
#endif
