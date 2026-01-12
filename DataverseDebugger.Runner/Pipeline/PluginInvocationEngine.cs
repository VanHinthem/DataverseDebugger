using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Abstractions;
using DataverseDebugger.Runner.Configuration;
using DataverseDebugger.Runner.Conversion.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Pipeline
{
    /// <summary>
    /// Extracted engine for plugin invocation. Preserves the original RunnerPipeServer
    /// execution behavior while providing a single delegation seam.
    /// </summary>
    internal sealed class PluginInvocationEngine
    {
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly Func<PluginWorkspaceManifest?> _getWorkspace;
        private readonly Func<EnvConfig?> _getEnvironment;
        private readonly Func<RunnerExecutionOptions> _getExecutionOptions;

        public PluginInvocationEngine(
            System.Net.Http.HttpClient httpClient,
            Func<PluginWorkspaceManifest?> getWorkspace,
            Func<EnvConfig?> getEnvironment,
            Func<RunnerExecutionOptions> getExecutionOptions)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _getWorkspace = getWorkspace ?? throw new ArgumentNullException(nameof(getWorkspace));
            _getEnvironment = getEnvironment ?? throw new ArgumentNullException(nameof(getEnvironment));
            _getExecutionOptions = getExecutionOptions ?? throw new ArgumentNullException(nameof(getExecutionOptions));
        }

        /// <summary>
        /// Executes a single plugin invocation using the legacy RunnerPipeServer behavior.
        /// </summary>
        public PluginInvokeResponse Invoke(string? payload, out PluginInvokeRequest? request)
        {
            RunnerPipeServer.EnsureSdkAssemblyResolver();
            var sw = Stopwatch.StartNew();
            request = null;
            List<string>? trace = null;
            ServiceClient? serviceClient = null;
            try
            {
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return new PluginInvokeResponse
                    {
                        Status = HealthStatus.Error,
                        Message = "Empty plugin payload"
                    };
                }

                request = JsonSerializer.Deserialize<PluginInvokeRequest>(
                    payload!,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request == null)
                {
                    return new PluginInvokeResponse
                    {
                        Status = HealthStatus.Error,
                        Message = "Invalid plugin payload"
                    };
                }

                var resolvedMode = ExecutionModeResolver.Resolve(request);
                var options = _getExecutionOptions();
                if (resolvedMode == ExecutionMode.Online && !options.AllowLiveWrites)
                {
                    throw new RunnerNotSupportedException(
                        "Online",
                        "LiveWrites",
                        $"Set AllowLiveWrites=true ({RunnerExecutionOptions.AllowLiveWritesEnvVar}).");
                }

                if (resolvedMode == ExecutionMode.Offline)
                {
                    throw new RunnerNotSupportedException(
                        "Offline",
                        "PluginInvocation",
                        "Offline mode is not supported yet.");
                }

                var requestAssembly = request.Assembly;
                var requestTypeName = request.TypeName;
                var environment = _getEnvironment();
                var effectiveOrgUrl = string.IsNullOrWhiteSpace(request.OrgUrl)
                    ? environment?.OrgUrl
                    : request.OrgUrl;

                if (resolvedMode == ExecutionMode.Online || resolvedMode == ExecutionMode.Hybrid)
                {
                    serviceClient = TryCreateServiceClient(effectiveOrgUrl, request.AccessToken);
                    if (resolvedMode == ExecutionMode.Online && serviceClient == null)
                    {
                        throw new RunnerNotSupportedException(
                            "Online",
                            "LiveWrites",
                            "OrgUrl and AccessToken are required for Online mode.");
                    }
                }

                var ws = _getWorkspace();
                if (ws == null)
                {
                    return new PluginInvokeResponse
                    {
                        RequestId = request.RequestId,
                        Status = HealthStatus.Error,
                        Message = "Workspace not initialized",
                        TraceLines = new List<string> { "Workspace not initialized" }
                    };
                }

                trace = new List<string>();
                var searchDirs = new List<string>
                {
                    AppContext.BaseDirectory
                };
                var shadowDirs = new List<string>();

                var resolvedAssemblyPath = RunnerPipeServer.ResolvePath(requestAssembly);
                var loadAssemblyPath = RunnerPipeServer.GetShadowCopyPath(resolvedAssemblyPath);
                var asmEntry = ws.Assemblies?.FirstOrDefault(a =>
                    string.Equals(a.Path, requestAssembly, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(a.Path), Path.GetFileName(requestAssembly), StringComparison.OrdinalIgnoreCase));

                if (asmEntry != null && asmEntry.DependencyFolders != null)
                {
                    foreach (var dep in asmEntry.DependencyFolders)
                    {
                        var depPath = RunnerPipeServer.ResolvePath(dep);
                        if (Directory.Exists(depPath))
                        {
                            searchDirs.Add(depPath);
                            shadowDirs.Add(depPath);
                        }
                    }
                }

                if (File.Exists(resolvedAssemblyPath))
                {
                    var dir = Path.GetDirectoryName(resolvedAssemblyPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        searchDirs.Add(dir);
                        shadowDirs.Add(dir);
                    }
                }
                else
                {
                    return new PluginInvokeResponse
                    {
                        RequestId = request.RequestId,
                        Status = HealthStatus.Error,
                        Message = $"Assembly not found: {requestAssembly}",
                        TraceLines = new List<string> { $"Assembly not found: {requestAssembly}" }
                    };
                }

                ResolveEventHandler resolver = (s, e) =>
                {
                    var name = new AssemblyName(e.Name).Name + ".dll";
                    foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var candidate = Path.Combine(dir, name);
                        if (File.Exists(candidate))
                        {
                            try
                            {
                                var candidateToLoad = RunnerPipeServer.ShouldShadowCopy(candidate, shadowDirs)
                                    ? RunnerPipeServer.GetShadowCopyPath(candidate)
                                    : candidate;
                                return Assembly.LoadFrom(candidateToLoad);
                            }
                            catch
                            {
                            }
                        }
                    }
                    return null;
                };

                Assembly? loadedAssembly = null;
                Type? pluginType = null;
                AppDomain.CurrentDomain.AssemblyResolve += resolver;
                try
                {
                    loadedAssembly = Assembly.LoadFrom(loadAssemblyPath);
                    trace.Add($"Loaded assembly: {loadedAssembly.FullName}");
                    try
                    {
                        var typeList = new List<string>();
                        var types = loadedAssembly.GetTypes().Where(t => t.IsClass).Select(t => t.FullName ?? t.Name).ToList();
                        typeList.AddRange(types);
                        var limited = typeList.Take(20).ToList();
                        trace.Add($"Types in assembly ({typeList.Count}):");
                        foreach (var name in limited)
                        {
                            trace.Add($" - {name}");
                        }
                    }
                    catch (ReflectionTypeLoadException rtle)
                    {
                        if (rtle.Types != null)
                        {
                            var typeList = rtle.Types.Where(t => t != null && t.IsClass).Select(t => t!.FullName ?? t.Name).ToList();
                            var limited = typeList.Take(20).ToList();
                            trace.Add($"Types in assembly ({typeList.Count}):");
                            foreach (var name in limited)
                            {
                                trace.Add($" - {name}");
                            }
                        }
                        if (rtle.LoaderExceptions != null)
                        {
                            trace.Add("Loader exceptions during type enumeration:");
                            foreach (var ex in rtle.LoaderExceptions.Where(ex => ex != null).Take(5))
                            {
                                trace.Add($" - {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        trace.Add($"Type enumeration failed: {ex.Message}");
                    }
                    pluginType = loadedAssembly.GetType(requestTypeName, throwOnError: false, ignoreCase: true);
                    if (pluginType == null)
                    {
                        trace.Add($"Type not found: {requestTypeName}");

                        var typeNames = new List<string>();
                        try
                        {
                            typeNames.AddRange(loadedAssembly
                                .GetTypes()
                                .Where(t => t.IsClass)
                                .Select(t => t.FullName ?? t.Name));
                        }
                        catch (ReflectionTypeLoadException rtle)
                        {
                            if (rtle.Types != null)
                            {
                                foreach (var t in rtle.Types.Where(t => t != null && t.IsClass))
                                {
                                    typeNames.Add(t!.FullName ?? t.Name);
                                }
                            }
                            if (rtle.LoaderExceptions != null)
                            {
                                trace.Add("Loader exceptions:");
                                foreach (var ex in rtle.LoaderExceptions.Where(ex => ex != null).Take(5))
                                {
                                    trace.Add($" - {ex.GetType().Name}: {ex.Message}");
                                }
                            }
                        }

                        var candidates = typeNames.Take(10).ToList();
                        if (candidates.Count > 0)
                        {
                            trace.Add("Types in assembly (first 10):");
                            foreach (var c in candidates) trace.Add($" - {c}");
                        }

                        // Try name-only match as a fallback
                        var byName = typeNames.FirstOrDefault(t => string.Equals(Path.GetFileNameWithoutExtension(t), requestTypeName, StringComparison.OrdinalIgnoreCase) || string.Equals(t?.Split('.').LastOrDefault(), requestTypeName, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(byName))
                        {
                            trace.Add($"Found similar type by name: {byName}");
                        }

                        return new PluginInvokeResponse
                        {
                            RequestId = request.RequestId,
                            Status = HealthStatus.Error,
                            Message = $"Type not found: {requestTypeName}",
                            TraceLines = trace
                        };
                    }

                    trace.Add($"Found type: {pluginType.FullName}");
                    var tracingService = new StubTracingService(line => trace.Add(line));
                    DataverseContext? conversionContext = null;
                    if (RunnerPipeServer.TryGetConversionContext(out var resolvedContext, out _))
                    {
                        conversionContext = resolvedContext;
                    }

                    var writeMode = resolvedMode == ExecutionMode.Online
                        ? RunnerWriteMode.LiveWrites
                        : RunnerWriteMode.FakeWrites;

                    var attributeResolver = new AttributeMetadataResolver(
                        conversionContext?.MetadataCache,
                        environment,
                        request.AccessToken,
                        _httpClient,
                        trace);
                    var orgService = new RunnerOrganizationService(
                        line => trace.Add(line),
                        _httpClient,
                        effectiveOrgUrl,
                        request.AccessToken,
                        writeMode,
                        logicalName =>
                        {
                            var entity = conversionContext?.MetadataCache?.GetEntityFromLogicalName(logicalName);
                            return entity?.EntitySetName;
                        },
                        attributeResolver,
                        serviceClient);
                    trace.Add($"OrgService write mode: {writeMode}");
                    var orgFactory = new StubOrganizationServiceFactory(orgService);
                    var primaryName = string.IsNullOrWhiteSpace(request.PrimaryEntityName) ? "entity" : request.PrimaryEntityName;
                    var context = new StubPluginExecutionContext
                    {
                        MessageName = string.IsNullOrWhiteSpace(request.MessageName) ? "Create" : request.MessageName,
                        PrimaryEntityName = primaryName,
                        PrimaryEntityId = RunnerPipeServer.TryParseGuid(request.PrimaryEntityId),
                        Stage = request.Stage <= 0 ? 40 : request.Stage,
                        Mode = request.Mode,
                        Depth = 1,
                        OrganizationId = Guid.NewGuid(),
                        OrganizationName = "DataverseDebugger",
                        UserId = Guid.NewGuid(),
                        InitiatingUserId = Guid.NewGuid(),
                        BusinessUnitId = Guid.NewGuid(),
                        CorrelationId = Guid.NewGuid(),
                        OperationId = Guid.NewGuid(),
                        OperationCreatedOn = DateTime.UtcNow,
                        RequestId = Guid.NewGuid(),
                        IsInTransaction = false,
                        IsExecutingOffline = false,
                        IsOfflinePlayback = false
                    };

                    var populatedFromHttp = RunnerPipeServer.TryPopulateContextFromHttp(request, context, trace);

                    // Target and images from JSON (fallback to empty target)
                    if (!populatedFromHttp || !context.InputParameters.Contains("Target"))
                    {
                        var target = RunnerPipeServer.ParseEntityFromJson(request.TargetJson, primaryName, context.PrimaryEntityId, attributeResolver, trace, "Target");
                        if (target == null && !string.IsNullOrWhiteSpace(primaryName))
                        {
                            target = new Entity(primaryName);
                            if (context.PrimaryEntityId != Guid.Empty)
                            {
                                target.Id = context.PrimaryEntityId;
                            }
                        }
                        if (target != null)
                        {
                            context.InputParameters["Target"] = target;
                        }
                    }
                    if (request.Images != null && request.Images.Count > 0)
                    {
                        foreach (var image in request.Images)
                        {
                            if (image == null || string.IsNullOrWhiteSpace(image.EntityJson)) continue;
                            var imageType = image.ImageType ?? string.Empty;
                            var isPre = imageType.Equals("PreImage", StringComparison.OrdinalIgnoreCase) ||
                                imageType.Equals("Both", StringComparison.OrdinalIgnoreCase);
                            var isPost = imageType.Equals("PostImage", StringComparison.OrdinalIgnoreCase) ||
                                imageType.Equals("Both", StringComparison.OrdinalIgnoreCase);
                            if (!isPre && !isPost) continue;

                            var alias = string.IsNullOrWhiteSpace(image.EntityAlias)
                                ? (isPost && !isPre ? "PostImage" : "PreImage")
                                : image.EntityAlias;

                            if (isPre)
                            {
                                var pre = RunnerPipeServer.ParseEntityFromJson(image.EntityJson, primaryName, Guid.Empty, attributeResolver, trace, $"PreImage:{alias}");
                                if (pre != null)
                                {
                                    context.PreEntityImages[alias] = pre;
                                }
                            }

                            if (isPost)
                            {
                                var post = (isPre && isPost)
                                    ? RunnerPipeServer.ParseEntityFromJson(image.EntityJson, primaryName, Guid.Empty, attributeResolver, trace, $"PostImage:{alias}")
                                    : RunnerPipeServer.ParseEntityFromJson(image.EntityJson, primaryName, Guid.Empty, attributeResolver, trace, $"PostImage:{alias}");
                                if (post != null)
                                {
                                    context.PostEntityImages[alias] = post;
                                }
                            }
                        }
                    }
                    else
                    {
                        var preImage = RunnerPipeServer.ParseEntityFromJson(request.PreImageJson, primaryName, Guid.Empty, attributeResolver, trace, "PreImage");
                        if (preImage != null)
                        {
                            context.PreEntityImages["PreImage"] = preImage;
                        }
                        var postImage = RunnerPipeServer.ParseEntityFromJson(request.PostImageJson, primaryName, Guid.Empty, attributeResolver, trace, "PostImage");
                        if (postImage != null)
                        {
                            context.PostEntityImages["PostImage"] = postImage;
                        }
                    }

                    var logDelegate = new Action<string>(trace.Add);
                    var loggerFactory = new StubLoggerFactory(logDelegate);
                    var logger = loggerFactory.CreateLogger(pluginType.FullName ?? "Plugin");
                    var telemetryLogger = new StubPluginTelemetryLogger(logDelegate);
                    var notificationService = new StubServiceEndpointNotificationService(logDelegate);
                    var featureControlService = new StubFeatureControlService(logDelegate);

                    var services = new Dictionary<Type, object>
                    {
                        { typeof(ITracingService), tracingService },
                        { typeof(IOrganizationServiceFactory), orgFactory },
                        { typeof(IOrganizationService), orgService },
                        { typeof(IPluginExecutionContext), context },
                        { typeof(ILoggerFactory), loggerFactory },
                        { typeof(ILogger), logger },
                        { typeof(Microsoft.Xrm.Sdk.PluginTelemetry.ILogger), telemetryLogger },
                        { typeof(IServiceEndpointNotificationService), notificationService },
                        { typeof(IFeatureControlService), featureControlService }
                    };

                    var serviceProvider = new StubServiceProvider(services);
                    trace.Add("Invoking plugin Execute(...)");
                    try
                    {
                        var plugin = RunnerPipeServer.CreatePluginInstance(pluginType, request.UnsecureConfiguration, request.SecureConfiguration, trace);
                        if (plugin != null)
                        {
                            plugin.Execute(serviceProvider);
                            trace.Add("Plugin execution completed.");
                        }
                        else
                        {
                            trace.Add("Type does not implement IPlugin; cannot execute.");
                        }
                    }
                    catch (Exception ex)
                    {
                        trace.Add($"Plugin execution threw: {ex.GetType().Name}: {ex.Message}");
                        RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                            $"Plugin execution threw: {ex.GetType().Name}: {ex.Message}");
                        var stack = ex.StackTrace;
                        if (stack != null && stack.Length > 0)
                        {
                            trace.Add(stack);
                            RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, stack);
                        }
                    }
                }
                catch (Exception ex)
                {
                    trace.Add($"Assembly load/type resolution failed: {ex.Message}");
                    return new PluginInvokeResponse
                    {
                        RequestId = request.RequestId,
                        Status = HealthStatus.Error,
                        Message = $"Plugin load failed: {ex.Message}",
                        TraceLines = trace
                    };
                }
                finally
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= resolver;
                }

                return new PluginInvokeResponse
                {
                    RequestId = request.RequestId,
                    Status = HealthStatus.Ready,
                    Message = $"Stubbed invoke for {requestTypeName}",
                    TraceLines = trace
                };
            }
            catch (RunnerNotSupportedException ex)
            {
                return new PluginInvokeResponse
                {
                    RequestId = request?.RequestId ?? string.Empty,
                    Status = HealthStatus.Error,
                    Message = ex.Message,
                    TraceLines = trace ?? new List<string>()
                };
            }
            catch (Exception ex)
            {
                return new PluginInvokeResponse
                {
                    Status = HealthStatus.Error,
                    Message = $"Plugin invoke failed: {ex.Message}"
                };
            }
            finally
            {
                serviceClient?.Dispose();
                sw.Stop();
                RunnerPipeServer.LogSlowCommand("executePlugin", sw.Elapsed, RunnerPipeServer.BuildPluginSummary(request));
            }
        }

        private static ServiceClient? TryCreateServiceClient(string? orgUrl, string? accessToken)
        {
            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            try
            {
                // Uses the caller-provided access token; token refresh is not implemented here.
                var connectionString = $"AuthType=OAuth;Url={orgUrl};AccessToken={accessToken};";
                return new ServiceClient(connectionString);
            }
            catch
            {
                return null;
            }
        }
    }
}
