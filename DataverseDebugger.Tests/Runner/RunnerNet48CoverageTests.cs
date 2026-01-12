#if NET48
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Conversion.Model;
using Microsoft.Xrm.Sdk;

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
    public void EntityMergeUtility_MergeThrowsNotImplemented()
    {
        var utilityType = GetRunnerType("DataverseDebugger.Runner.Services.Hybrid.EntityMergeUtility");
        var merge = utilityType.GetMethod("Merge", BindingFlags.Public | BindingFlags.Static);

        var exception = Assert.ThrowsException<TargetInvocationException>(
            () => merge!.Invoke(null, new object[] { new Entity("account"), new Entity("account") }));

        Assert.IsInstanceOfType(exception.InnerException, typeof(NotImplementedException));
    }

    [TestMethod]
    public void OfflineOrganizationService_CreateThrowsNotImplemented()
    {
        var serviceType = GetRunnerType("DataverseDebugger.Runner.Services.Offline.OfflineOrganizationService");
        var service = Activator.CreateInstance(serviceType, nonPublic: true);
        var create = serviceType.GetMethod("Create");

        var exception = Assert.ThrowsException<TargetInvocationException>(
            () => create!.Invoke(service, new object[] { new Entity("account") }));

        Assert.IsInstanceOfType(exception.InnerException, typeof(NotImplementedException));
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
}
#endif
