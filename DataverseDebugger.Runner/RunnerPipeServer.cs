using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Conversion.Converters;
using DataverseDebugger.Runner.Conversion.Model;
using DataverseDebugger.Runner.Conversion.Utils;
using DataverseDebugger.Runner.Pipeline;
using DataverseDebugger.Runner.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Security.Cryptography;

namespace DataverseDebugger.Runner
{
    /// <summary>
    /// Named pipe server that handles IPC commands from the host application.
    /// Processes workspace initialization, HTTP request execution, and plugin invocation.
    /// </summary>
    internal sealed class RunnerPipeServer
    {
        private const int MaxInstances = 1;
        private static readonly TimeSpan HttpProxyTimeout = TimeSpan.FromMinutes(5);
        private static readonly System.Net.Http.HttpClient HttpClient;
        private static readonly object WorkspaceLock = new object();
        private static readonly object ShadowCopyLock = new object();
        private static PluginWorkspaceManifest? _workspace;
        private static readonly System.Collections.Generic.List<System.IO.FileSystemWatcher> WorkspaceWatchers = new System.Collections.Generic.List<System.IO.FileSystemWatcher>();
        private static readonly System.Collections.Generic.Dictionary<string, ShadowCopyInfo> ShadowCopies =
            new System.Collections.Generic.Dictionary<string, ShadowCopyInfo>(StringComparer.OrdinalIgnoreCase);
        private static string ShadowRoot = GetDefaultShadowRoot();
        private static bool _sdkResolverRegistered;
        private static EnvConfig? _environment;
        private static readonly object ConversionLock = new object();
        private static DataverseContext? _conversionContext;
        private static string? _conversionMetadataPath;
        private static DateTime? _conversionMetadataStampUtc;
        private const int MaxLogUrlLength = 200;
        private static readonly TimeSpan SlowCommandThreshold = TimeSpan.FromSeconds(2);
        private static readonly PluginInvocationEngine PluginInvocationEngine;
        private static readonly RunnerExecutionOptions ExecutionOptions;

        static RunnerPipeServer()
        {
            var handler = new System.Net.Http.HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = false // we pass Cookie header manually from capture
            };
            HttpClient = new System.Net.Http.HttpClient(handler)
            {
                Timeout = HttpProxyTimeout
            };
            ExecutionOptions = RunnerExecutionOptions.FromEnvironment();
            PluginInvocationEngine = new PluginInvocationEngine(
                HttpClient,
                () =>
                {
                    lock (WorkspaceLock)
                    {
                        return _workspace;
                    }
                },
                () => _environment,
                () => ExecutionOptions);
        }

        /// <summary>
        /// Formats a URL for logging, truncating if necessary.
        /// </summary>
        /// <param name="url">The URL to format.</param>
        /// <returns>The formatted URL, truncated with "..." if longer than MaxLogUrlLength.</returns>
        private static string FormatUrlForLog(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "(unknown url)";
            }

            // After IsNullOrWhiteSpace check, url is guaranteed non-null
            if (url!.Length <= MaxLogUrlLength)
            {
                return url;
            }

            return url.Substring(0, MaxLogUrlLength) + "...";
        }

        private static string BuildRequestSummary(InterceptedHttpRequest? request, string? requestId)
        {
            if (request == null)
            {
                return "unknown request";
            }

            var method = string.IsNullOrWhiteSpace(request.Method) ? "(unknown method)" : request.Method;
            var url = FormatUrlForLog(request.Url);
            var clientRequestId = TryGetHeaderValue(request.Headers, "x-ms-client-request-id");
            return $"{method} {url} (id={requestId ?? "none"}, cid={clientRequestId ?? "none"})";
        }

        internal static string BuildPluginSummary(PluginInvokeRequest? request)
        {
            if (request == null)
            {
                return "unknown plugin request";
            }

            var typeName = string.IsNullOrWhiteSpace(request.TypeName) ? "(unknown type)" : request.TypeName;
            var message = string.IsNullOrWhiteSpace(request.MessageName) ? "(unknown message)" : request.MessageName;
            return $"{typeName} msg={message} stage={request.Stage} mode={request.Mode} id={request.RequestId}";
        }

        internal static void LogSlowCommand(string command, TimeSpan elapsed, string? detail)
        {
            if (elapsed < SlowCommandThreshold)
            {
                return;
            }

            var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";
            RunnerLogger.Log(RunnerLogCategory.Perf, RunnerLogLevel.Info,
                $"Slow {command} ({(int)elapsed.TotalMilliseconds} ms){suffix}");
        }

        /// <summary>
        /// Starts the named pipe server and processes incoming commands until cancelled.
        /// </summary>
        /// <param name="cancellationToken">Token to signal shutdown.</param>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            RunnerLogger.Log(RunnerLogCategory.RunnerLifecycle, RunnerLogLevel.Info, "Runner pipe server started.");
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        PipeNames.RunnerPipe,
                        PipeDirection.InOut,
                        MaxInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                }
                catch (IOException)
                {
                    // Pipe name already in use (previous runner still exiting). Back off briefly.
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                using (server)
                {
                    using (cancellationToken.Register(() => SafeDispose(server)))
                    {
                        try
                        {
                            await Task.Factory.FromAsync(server.BeginWaitForConnection, server.EndWaitForConnection, null)
                                .ConfigureAwait(false);
                        }
                        catch (ObjectDisposedException)
                        {
                            // Cancellation triggered; exit loop if requested.
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }
                            continue;
                        }

                        if (!server.IsConnected)
                        {
                            continue;
                        }

                        try
                        {
                            await HandleClientAsync(server, cancellationToken).ConfigureAwait(false);
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine($"[Runner] Pipe IO error: {ex.Message}");
                        }
                    }
                }
            }
        }

        private static async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken cancellationToken)
        {
            try
            {
                var message = await PipeProtocol.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
                if (message == null)
                {
                    return;
                }

                if (!string.Equals(message.Command, "execute", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(message.Command, "executePlugin", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(message.Command, "runnerLogFetch", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(message.Command, "runnerLogConfig", StringComparison.OrdinalIgnoreCase))
                {
                    RunnerLogger.Log(RunnerLogCategory.Ipc, RunnerLogLevel.Debug, $"IPC command: {message.Command}");
                }

                if (string.Equals(message.Command, "health", StringComparison.OrdinalIgnoreCase))
                {
                    var response = BuildHealthResponse();
                    await PipeProtocol.WriteAsync(stream, "healthResponse", response, cancellationToken).ConfigureAwait(false);
                }
                else if (string.Equals(message.Command, "initWorkspace", StringComparison.OrdinalIgnoreCase))
                {
                    var response = HandleInitWorkspace(message.Payload);
                    await PipeProtocol.WriteAsync(stream, "initWorkspaceResponse", response, cancellationToken).ConfigureAwait(false);
                }
                else if (string.Equals(message.Command, "runnerLogConfig", StringComparison.OrdinalIgnoreCase))
                {
                    var request = JsonSerializer.Deserialize<RunnerLogConfigRequest>(message.Payload ?? string.Empty, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    RunnerLogger.Configure(request);
                    var response = new RunnerLogConfigResponse
                    {
                        Applied = true,
                        Message = "Runner log configuration applied."
                    };
                    await PipeProtocol.WriteAsync(stream, "runnerLogConfigResponse", response, cancellationToken).ConfigureAwait(false);
                }
                else if (string.Equals(message.Command, "runnerLogFetch", StringComparison.OrdinalIgnoreCase))
                {
                    var request = JsonSerializer.Deserialize<RunnerLogFetchRequest>(message.Payload ?? string.Empty, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                  ?? new RunnerLogFetchRequest();
                    var response = RunnerLogger.Fetch(request);
                    await PipeProtocol.WriteAsync(stream, "runnerLogFetchResponse", response, cancellationToken).ConfigureAwait(false);
                }
                else if (string.Equals(message.Command, "execute", StringComparison.OrdinalIgnoreCase))
                {
                    var response = await HandleExecuteAsync(message.Payload, stream, cancellationToken).ConfigureAwait(false);
                    await PipeProtocol.WriteAsync(stream, "executeResponse", response, cancellationToken).ConfigureAwait(false);
                }
                else if (string.Equals(message.Command, "executePlugin", StringComparison.OrdinalIgnoreCase))
                {
                    var response = HandleExecutePlugin(message.Payload);
                    await PipeProtocol.WriteAsync(stream, "executePluginResponse", response, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var error = new HealthCheckResponse
                    {
                        Version = ProtocolVersion.Current,
                        Status = HealthStatus.Error,
                        Message = $"Unknown command: {message.Command}"
                    };
                    await PipeProtocol.WriteAsync(stream, "error", error, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore cancellation
            }
            catch (IOException ex)
            {
                RunnerLogger.Log(RunnerLogCategory.Ipc, RunnerLogLevel.Debug,
                    $"IPC pipe closed: {ex.Message}");
            }
            catch (Exception ex)
            {
                RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                    $"IPC handler failed: {ex.GetType().Name}: {ex.Message}");
                if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, ex.StackTrace);
                }
            }
        }

        private static HealthCheckResponse BuildHealthResponse()
        {
            var capabilities = CapabilityFlags.TraceStreaming | CapabilityFlags.StepCatalog | CapabilityFlags.BatchSupport;
            return new HealthCheckResponse
            {
                Version = ProtocolVersion.Current,
                Status = HealthStatus.Ready,
                Capabilities = capabilities,
                Message = "Runner ready"
            };
        }

        private static InitializeWorkspaceResponse HandleInitWorkspace(string? payload)
        {
            var sw = Stopwatch.StartNew();
            InitializeWorkspaceRequest? request = null;
            try
            {
                RunnerLogger.Log(RunnerLogCategory.WorkspaceInit, RunnerLogLevel.Info, "Initialize workspace requested.");
                if (string.IsNullOrWhiteSpace(payload))
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, "Init workspace failed: empty payload.");
                    return new InitializeWorkspaceResponse
                    {
                        Status = HealthStatus.Error,
                        Message = "No payload provided"
                    };
                }

                request = JsonSerializer.Deserialize<InitializeWorkspaceRequest>(payload!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request == null)
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, "Init workspace failed: invalid payload.");
                    return new InitializeWorkspaceResponse
                    {
                        Status = HealthStatus.Error,
                        Message = "Invalid init payload"
                    };
                }

                var validation = ValidateWorkspace(request);
                if (validation.Status != HealthStatus.Ready)
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, $"Init workspace validation failed: {validation.Message}");
                    return validation;
                }

                lock (WorkspaceLock)
                {
                    _workspace = request.Workspace;
                    ResetWorkspaceWatchers(_workspace);
                }
                lock (ConversionLock)
                {
                    _environment = request.Environment;
                    _conversionContext = null;
                    _conversionMetadataPath = null;
                    _conversionMetadataStampUtc = null;
                }

                ConfigureShadowRoot(request.Environment);

                var pluginCollection = CollectPluginTypes(request.Workspace);
                RunnerLogger.Log(RunnerLogCategory.WorkspaceInit, RunnerLogLevel.Info,
                    $"Workspace loaded. Assemblies={request.Workspace.Assemblies.Count}, Types={pluginCollection.Types.Count}, Steps={pluginCollection.Steps.Count}.");

                return new InitializeWorkspaceResponse
                {
                    Version = ProtocolVersion.Current,
                    Status = HealthStatus.Ready,
                    Message = $"Workspace loaded ({request.Workspace.Assemblies.Count} assemblies)",
                    PluginTypes = pluginCollection.Types,
                    Steps = pluginCollection.Steps
                };
            }
            catch (Exception ex)
            {
                RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, $"Init workspace failed: {ex.Message}");
                return new InitializeWorkspaceResponse
                {
                    Status = HealthStatus.Error,
                    Message = $"Init failed: {ex.Message}"
                };
            }
            finally
            {
                sw.Stop();
                var asmCount = request?.Workspace?.Assemblies?.Count ?? 0;
                LogSlowCommand("initWorkspace", sw.Elapsed, $"assemblies={asmCount}");
            }
        }

        internal static bool TryGetConversionContext(out DataverseContext context, out string error)
        {
            context = null!;
            error = string.Empty;

            EnvConfig? environment;
            lock (ConversionLock)
            {
                environment = _environment;
            }

            if (environment == null || string.IsNullOrWhiteSpace(environment.OrgUrl))
            {
                error = "Environment not configured.";
                return false;
            }

            var rawMetadataPath = environment.MetadataPath;
            if (string.IsNullOrWhiteSpace(rawMetadataPath))
            {
                error = "Metadata path not configured.";
                return false;
            }

            var metadataPath = ResolvePath(rawMetadataPath!);
            if (!File.Exists(metadataPath))
            {
                error = "Metadata file not found: " + metadataPath;
                return false;
            }

            var stamp = File.GetLastWriteTimeUtc(metadataPath);

            lock (ConversionLock)
            {
                if (_conversionContext != null
                    && string.Equals(_conversionMetadataPath, metadataPath, StringComparison.OrdinalIgnoreCase)
                    && _conversionMetadataStampUtc.HasValue
                    && _conversionMetadataStampUtc.Value == stamp)
                {
                    context = _conversionContext;
                    return true;
                }
            }

            var stopwatch = Stopwatch.StartNew();
            if (!ConversionContextFactory.TryCreate(metadataPath, environment.OrgUrl, out var newContext, out var createError))
            {
                error = createError ?? "Metadata parse failed.";
                RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                    $"Metadata context creation failed: {error}");
                return false;
            }
            stopwatch.Stop();

            lock (ConversionLock)
            {
                _conversionContext = newContext;
                _conversionMetadataPath = metadataPath;
                _conversionMetadataStampUtc = stamp;
                context = newContext;
            }

            RunnerLogger.Log(RunnerLogCategory.Metadata, RunnerLogLevel.Info,
                $"Metadata context loaded: {metadataPath} ({stopwatch.ElapsedMilliseconds} ms)");
            RunnerLogger.Log(RunnerLogCategory.Perf, RunnerLogLevel.Debug,
                $"Metadata parse time: {stopwatch.ElapsedMilliseconds} ms");
            return true;
        }

        private static string GetDefaultShadowRoot()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "runner-shadow",
                Process.GetCurrentProcess().Id.ToString());
        }

        private static void ConfigureShadowRoot(EnvConfig? environment)
        {
            var baseRoot = environment?.RunnerShadowRoot;
            var newRoot = string.IsNullOrWhiteSpace(baseRoot)
                ? GetDefaultShadowRoot()
                : Path.Combine(baseRoot, Process.GetCurrentProcess().Id.ToString());

            lock (ShadowCopyLock)
            {
                if (!string.Equals(ShadowRoot, newRoot, StringComparison.OrdinalIgnoreCase))
                {
                    ShadowRoot = newRoot;
                    ShadowCopies.Clear();
                }
            }
        }

        internal static bool TryPopulateContextFromHttp(PluginInvokeRequest request, StubPluginExecutionContext context, System.Collections.Generic.List<string> trace)
        {
            var httpRequest = request.HttpRequest;
            if (httpRequest == null)
            {
                return false;
            }

            // Extract MSCRMCallerID header for impersonation
            TryExtractImpersonationHeader(httpRequest.Headers, context, trace);

            if (!TryGetConversionContext(out var conversionContext, out var error))
            {
                trace.Add("Conversion skipped: " + error);
                return false;
            }

            var method = string.IsNullOrWhiteSpace(httpRequest.Method) ? "GET" : httpRequest.Method.ToUpperInvariant();
            var headers = BuildNameValueCollection(httpRequest.Headers);
            var body = httpRequest.Body != null && httpRequest.Body.Length > 0
                ? Encoding.UTF8.GetString(httpRequest.Body)
                : null;

            WebApiRequest webApiRequest;
            try
            {
                webApiRequest = WebApiRequest.Create(method, httpRequest.Url, headers, body);
                if (webApiRequest == null)
                {
                    trace.Add("Conversion skipped: request is not a Dataverse Web API call.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                trace.Add("Conversion failed: " + ex.Message);
                return false;
            }

            RequestConversionResult conversionResult;
            try
            {
                var converter = new RequestConverter(conversionContext);
                conversionResult = converter.Convert(webApiRequest);
            }
            catch (Exception ex)
            {
                trace.Add("Conversion failed: " + ex.Message);
                return false;
            }

            if (conversionResult?.DebugLog != null && conversionResult.DebugLog.Count > 0)
            {
                foreach (var line in conversionResult.DebugLog)
                {
                    trace.Add("Conversion detail: " + line);
                }
            }

            if (conversionResult == null || conversionResult.ConvertedRequest == null)
            {
                var failureMessage = conversionResult?.ConvertFailureMessage;
                if (!string.IsNullOrWhiteSpace(failureMessage))
                {
                    trace.Add("Conversion failed: " + failureMessage);
                }
                else
                {
                    trace.Add("Conversion failed: no request generated.");
                }
                return false;
            }

            var orgRequest = conversionResult.ConvertedRequest;
            if (!string.IsNullOrWhiteSpace(orgRequest.RequestName)
                && !string.IsNullOrWhiteSpace(context.MessageName)
                && !string.Equals(orgRequest.RequestName, context.MessageName, StringComparison.OrdinalIgnoreCase))
            {
                trace.Add($"Conversion message '{orgRequest.RequestName}' differs from step '{context.MessageName}'.");
            }

            foreach (var kvp in orgRequest.Parameters)
            {
                context.InputParameters[kvp.Key] = kvp.Value;
            }

            ApplyPrimaryEntityFromParameters(context);
            return true;
        }

        private static void ApplyPrimaryEntityFromParameters(StubPluginExecutionContext context)
        {
            var parameters = context.InputParameters;
            var target = GetParameter(parameters, "Target") ?? GetParameter(parameters, "EntityMoniker");
            if (target == null)
            {
                return;
            }

            if (target is Entity entity)
            {
                if (!string.IsNullOrWhiteSpace(entity.LogicalName))
                {
                    context.PrimaryEntityName = entity.LogicalName;
                }
                if (entity.Id != Guid.Empty)
                {
                    context.PrimaryEntityId = entity.Id;
                }
                return;
            }

            if (target is EntityReference reference)
            {
                if (!string.IsNullOrWhiteSpace(reference.LogicalName))
                {
                    context.PrimaryEntityName = reference.LogicalName;
                }
                if (reference.Id != Guid.Empty)
                {
                    context.PrimaryEntityId = reference.Id;
                }
            }
        }

        /// <summary>
        /// Extracts the MSCRMCallerID header for user impersonation and sets UserId accordingly.
        /// </summary>
        /// <param name="headers">The HTTP request headers dictionary.</param>
        /// <param name="context">The plugin execution context to update.</param>
        /// <param name="trace">Trace output list for logging.</param>
        private static void TryExtractImpersonationHeader(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? headers, StubPluginExecutionContext context, System.Collections.Generic.List<string> trace)
        {
            if (headers == null)
            {
                return;
            }

            // Look for MSCRMCallerID header (case-insensitive)
            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, "MSCRMCallerID", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kvp.Key, "CallerObjectId", StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value != null && kvp.Value.Count > 0)
                    {
                        var headerValue = kvp.Value[0];
                        if (Guid.TryParse(headerValue, out var userId) && userId != Guid.Empty)
                        {
                            // InitiatingUserId stays the same (actual caller)
                            // UserId changes to the impersonated user
                            var initiatingUserId = context.UserId;
                            context.UserId = userId;
                            trace.Add($"Impersonation: UserId set to {userId} via {kvp.Key} header (InitiatingUserId: {initiatingUserId})");
                            return;
                        }
                    }
                }
            }
        }

        internal static object? GetParameter(ParameterCollection parameters, string name)
        {
            if (parameters == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return parameters.Contains(name) ? parameters[name] : null;
        }

        internal static NameValueCollection BuildNameValueCollection(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? headers)
        {
            var collection = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            if (headers == null)
            {
                return collection;
            }

            foreach (var kvp in headers)
            {
                if (kvp.Value == null || kvp.Value.Count == 0)
                {
                    collection.Add(kvp.Key, string.Empty);
                    continue;
                }

                foreach (var value in kvp.Value)
                {
                    collection.Add(kvp.Key, value);
                }
            }

            return collection;
        }

        internal static IPlugin? CreatePluginInstance(Type pluginType, string? unsecureConfig, string? secureConfig, System.Collections.Generic.List<string> trace)
        {
            var ctor2 = pluginType.GetConstructor(new[] { typeof(string), typeof(string) });
            if (ctor2 != null)
            {
                trace.Add("Using plugin constructor (string unsecure, string secure).");
                return ctor2.Invoke(new object?[] { unsecureConfig, secureConfig }) as IPlugin;
            }

            var ctor1 = pluginType.GetConstructor(new[] { typeof(string) });
            if (ctor1 != null)
            {
                trace.Add("Using plugin constructor (string unsecure).");
                return ctor1.Invoke(new object?[] { unsecureConfig }) as IPlugin;
            }

            if (!string.IsNullOrWhiteSpace(unsecureConfig) || !string.IsNullOrWhiteSpace(secureConfig))
            {
                trace.Add("Plugin constructor does not accept configuration; values ignored.");
            }
            return Activator.CreateInstance(pluginType) as IPlugin;
        }

        // PR02: keep WebAPI entry wiring intact; delegate to the extracted engine.
        private static PluginInvokeResponse HandleExecutePlugin(string? payload)
        {
            return PluginInvocationEngine.Invoke(payload, out _);
        }

        private static async Task<ExecuteResponse> HandleExecuteAsync(string? payload, NamedPipeServerStream stream, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            ExecuteRequest? request = null;
            try
            {
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return new ExecuteResponse
                    {
                        RequestId = Guid.NewGuid().ToString("N"),
                        Response = new InterceptedHttpResponse
                        {
                            StatusCode = 400,
                            Body = System.Text.Encoding.UTF8.GetBytes("Empty execute payload")
                        },
                        Trace = new ExecutionTrace
                        {
                            Emulated = false,
                            TraceLines = new System.Collections.Generic.List<string> { "Empty execute payload" }
                        }
                    };
                }

                request = JsonSerializer.Deserialize<ExecuteRequest>(payload!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request == null)
                {
                    return new ExecuteResponse
                    {
                        RequestId = Guid.NewGuid().ToString("N"),
                        Response = new InterceptedHttpResponse
                        {
                            StatusCode = 400,
                            Body = System.Text.Encoding.UTF8.GetBytes("Invalid execute payload")
                        },
                        Trace = new ExecutionTrace
                        {
                            Emulated = false,
                            TraceLines = new System.Collections.Generic.List<string> { "Invalid execute payload" }
                        }
                    };
                }

                var response = await ProxyHttp(request, stream, cancellationToken).ConfigureAwait(false);

                return response;
            }
            catch (Exception ex)
            {
                return new ExecuteResponse
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    Response = new InterceptedHttpResponse
                    {
                        StatusCode = 500,
                        Body = System.Text.Encoding.UTF8.GetBytes($"Execute failed: {ex.Message}")
                    },
                    Trace = new ExecutionTrace
                    {
                        Emulated = false,
                        TraceLines = new System.Collections.Generic.List<string> { $"Execute failed: {ex.Message}" }
                    }
                };
            }
            finally
            {
                sw.Stop();
                LogSlowCommand("execute", sw.Elapsed, BuildRequestSummary(request?.Request, request?.RequestId));
            }
        }

        private static async System.Threading.Tasks.Task<ExecuteResponse> ProxyHttp(ExecuteRequest request, NamedPipeServerStream stream, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return new ExecuteResponse
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    Response = new InterceptedHttpResponse
                    {
                        StatusCode = 400,
                        Body = System.Text.Encoding.UTF8.GetBytes("No request provided")
                    },
                    Trace = new ExecutionTrace
                    {
                        TraceLines = new System.Collections.Generic.List<string> { "No request provided" }
                    }
                };
            }

            if (request.Request == null)
            {
                return new ExecuteResponse
                {
                    RequestId = request.RequestId ?? Guid.NewGuid().ToString("N"),
                    Response = new InterceptedHttpResponse
                    {
                        StatusCode = 400,
                        Body = System.Text.Encoding.UTF8.GetBytes("No HTTP request provided to proxy")
                    },
                    Trace = new ExecutionTrace
                    {
                        TraceLines = new System.Collections.Generic.List<string> { "No HTTP request provided to proxy" }
                    }
                };
            }

            var httpRequest = new System.Net.Http.HttpRequestMessage(new System.Net.Http.HttpMethod(request.Request.Method), request.Request.Url);
            if (request.Request.Body != null && request.Request.Body.Length > 0)
            {
                httpRequest.Content = new System.Net.Http.ByteArrayContent(request.Request.Body);
            }

            foreach (var kvp in request.Request.Headers)
            {
                foreach (var value in kvp.Value)
                {
                    if (!httpRequest.Headers.TryAddWithoutValidation(kvp.Key, value))
                    {
                        httpRequest.Content?.Headers.TryAddWithoutValidation(kvp.Key, value);
                    }
                }
            }

            var traceLines = new System.Collections.Generic.List<string>();
            var lastTraceSent = 0;
            var requestSummary = BuildRequestSummary(request.Request, request.RequestId);

            async Task SendTraceDeltaAsync()
            {
                if (traceLines.Count <= lastTraceSent)
                {
                    return;
                }

                var delta = traceLines.Skip(lastTraceSent).ToList();
                lastTraceSent = traceLines.Count;
                await SendTraceAsync(stream, request.RequestId, delta, cancellationToken).ConfigureAwait(false);
            }

            var clientRequestId = TryGetHeaderValue(request.Request.Headers, "x-ms-client-request-id");
            traceLines.Add($"Proxying {request.Request.Method} {request.Request.Url} (id={request.RequestId}, cid={clientRequestId ?? "none"})");
            await SendTraceDeltaAsync().ConfigureAwait(false);

            System.Net.Http.HttpResponseMessage? httpResponse = null;
            try
            {
                var sendSw = Stopwatch.StartNew();
                httpResponse = await HttpClient.SendAsync(httpRequest, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                sendSw.Stop();
                LogSlowCommand("proxy headers", sendSw.Elapsed, requestSummary);
            }
            catch (TaskCanceledException ex)
            {
                traceLines.Add($"Request timeout: {ex.Message}");
                await SendTraceDeltaAsync().ConfigureAwait(false);
                return new ExecuteResponse
                {
                    RequestId = request.RequestId,
                    Response = new InterceptedHttpResponse
                    {
                        StatusCode = 504,
                        Body = System.Text.Encoding.UTF8.GetBytes("Runner timeout while proxying request"),
                        Headers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(System.StringComparer.OrdinalIgnoreCase)
                    },
                    Trace = new ExecutionTrace { Emulated = false, TraceLines = traceLines }
                };
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                traceLines.Add($"Request error: {ex.Message}");
                await SendTraceDeltaAsync().ConfigureAwait(false);
                return new ExecuteResponse
                {
                    RequestId = request.RequestId,
                    Response = new InterceptedHttpResponse
                    {
                        StatusCode = 502,
                        Body = System.Text.Encoding.UTF8.GetBytes($"Runner HTTP error: {ex.Message}"),
                        Headers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(System.StringComparer.OrdinalIgnoreCase)
                    },
                    Trace = new ExecutionTrace { Emulated = false, TraceLines = traceLines }
                };
            }
            catch (Exception ex)
            {
                traceLines.Add($"Unexpected error: {ex.Message}");
                await SendTraceDeltaAsync().ConfigureAwait(false);
                return new ExecuteResponse
                {
                    RequestId = request.RequestId,
                    Response = new InterceptedHttpResponse
                    {
                        StatusCode = 500,
                        Body = System.Text.Encoding.UTF8.GetBytes($"Runner unexpected error: {ex.Message}"),
                        Headers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(System.StringComparer.OrdinalIgnoreCase)
                    },
                    Trace = new ExecutionTrace { Emulated = false, TraceLines = traceLines }
                };
            }

            var responseBody = Array.Empty<byte>();
            if (httpResponse.Content != null)
            {
                var readSw = Stopwatch.StartNew();
                responseBody = await httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                readSw.Stop();
                LogSlowCommand("proxy body", readSw.Elapsed, requestSummary);
            }

            if (httpResponse == null)
            {
                traceLines.Add("Runner did not receive a response object.");
                await SendTraceDeltaAsync().ConfigureAwait(false);
                return new ExecuteResponse
                {
                    RequestId = request.RequestId,
                    Response = new InterceptedHttpResponse
                    {
                        StatusCode = 500,
                        Body = System.Text.Encoding.UTF8.GetBytes("Runner did not receive a response"),
                        Headers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(System.StringComparer.OrdinalIgnoreCase)
                    },
                    Trace = new ExecutionTrace { Emulated = false, TraceLines = traceLines }
                };
            }

            var resp = new ExecuteResponse
            {
                RequestId = request.RequestId,
                Response = new InterceptedHttpResponse
                {
                    StatusCode = (int)httpResponse.StatusCode,
                    Body = responseBody,
                    Headers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(System.StringComparer.OrdinalIgnoreCase)
                },
                Trace = new ExecutionTrace
                {
                    Emulated = false,
                    TraceLines = traceLines
                }
            };

            traceLines.Add($"Response status: {(int)httpResponse.StatusCode} {(httpResponse.ReasonPhrase ?? string.Empty)} (id={request.RequestId})".Trim());
            if (httpResponse.Headers.TryGetValues("WWW-Authenticate", out var authHeaders))
            {
                foreach (var h in authHeaders)
                {
                    traceLines.Add($"WWW-Authenticate: {h}");
                }
            }
            await SendTraceDeltaAsync().ConfigureAwait(false);

            foreach (var header in httpResponse.Headers)
            {
                resp.Response.Headers[header.Key] = new System.Collections.Generic.List<string>(header.Value);
            }
            if (httpResponse.Content != null)
            {
                foreach (var header in httpResponse.Content.Headers)
                {
                    resp.Response.Headers[header.Key] = new System.Collections.Generic.List<string>(header.Value);
                }
            }

            return resp;
        }

        private static async Task SendTraceAsync(NamedPipeServerStream stream, string requestId, System.Collections.Generic.IEnumerable<string> lines, CancellationToken cancellationToken)
        {
            if (stream == null || lines == null)
            {
                return;
            }

            try
            {
                var payload = new ExecuteTrace
                {
                    RequestId = requestId,
                    TraceLines = new System.Collections.Generic.List<string>(lines)
                };
                await PipeProtocol.WriteAsync(stream, "executeTrace", payload, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // ignore streaming errors
            }
        }

        private static string? TryGetHeaderValue(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? headers, string name)
        {
            if (headers == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (headers.TryGetValue(name, out var list) && list != null && list.Count > 0)
            {
                return list[0];
            }

            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    var values = kvp.Value;
                    if (values != null && values.Count > 0)
                    {
                        return values[0];
                    }
                }
            }

            return null;
        }

        private static InitializeWorkspaceResponse ValidateWorkspace(InitializeWorkspaceRequest request)
        {
            var asmCount = request.Workspace?.Assemblies?.Count ?? 0;
            RunnerLogger.Log(RunnerLogCategory.WorkspaceInit, RunnerLogLevel.Debug,
                $"Validating workspace (assemblies={asmCount}).");
            var errors = new System.Text.StringBuilder();
            if (request.Environment == null || string.IsNullOrWhiteSpace(request.Environment.OrgUrl))
            {
                errors.AppendLine("OrgUrl is required.");
            }

            if (request.Workspace == null)
            {
                errors.AppendLine("Workspace is required.");
            }
            else
            {
                if (request.Workspace.Assemblies != null && request.Workspace.Assemblies.Count > 0)
                {
                    foreach (var asm in request.Workspace.Assemblies)
                    {
                        if (string.IsNullOrWhiteSpace(asm.Path))
                        {
                            errors.AppendLine("Assembly path is required.");
                            continue;
                        }
                        var resolved = asm.Path;
                        if (!System.IO.Path.IsPathRooted(resolved))
                        {
                            resolved = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, asm.Path));
                        }
                        if (!System.IO.File.Exists(resolved))
                        {
                            errors.AppendLine($"Assembly not found: {asm.Path}");
                        }
                    }
                }
            }

            if (errors.Length > 0)
            {
                RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                    $"Workspace validation failed: {errors.ToString().Trim()}");
                return new InitializeWorkspaceResponse
                {
                    Status = HealthStatus.Error,
                    Message = errors.ToString().Trim()
                };
            }

            var msg = asmCount == 0
                ? "Workspace validated (no assemblies provided)"
                : $"Workspace validated ({asmCount} assemblies)";

            return new InitializeWorkspaceResponse
            {
                Status = HealthStatus.Ready,
                Message = msg
            };
        }

        internal static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        }

        private static void ResetWorkspaceWatchers(PluginWorkspaceManifest manifest)
        {
            try
            {
                RunnerLogger.Log(RunnerLogCategory.WorkspaceInit, RunnerLogLevel.Debug,
                    "Resetting workspace file watchers.");
                foreach (var w in WorkspaceWatchers)
                {
                    try { w.EnableRaisingEvents = false; w.Dispose(); } catch { }
                }
                WorkspaceWatchers.Clear();

                if (manifest?.Assemblies == null)
                {
                    RunnerLogger.Log(RunnerLogCategory.WorkspaceInit, RunnerLogLevel.Debug,
                        "Workspace watchers not initialized (no assemblies).");
                    return;
                }

                foreach (var asm in manifest.Assemblies)
                {
                    if (string.IsNullOrWhiteSpace(asm.Path)) continue;
                    var resolved = asm.Path;
                    if (!System.IO.Path.IsPathRooted(resolved))
                    {
                        resolved = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, asm.Path));
                    }
                    var directory = System.IO.Path.GetDirectoryName(resolved);
                    var fileName = System.IO.Path.GetFileName(resolved);
                    if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !System.IO.Directory.Exists(directory))
                    {
                        continue;
                    }

                    var watcher = new System.IO.FileSystemWatcher(directory, fileName)
                    {
                        NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.Size
                    };
                    watcher.Changed += OnWorkspaceFileChanged;
                    watcher.Deleted += OnWorkspaceFileChanged;
                    watcher.Renamed += OnWorkspaceFileChanged;
                    watcher.EnableRaisingEvents = true;
                    WorkspaceWatchers.Add(watcher);
                }

                RunnerLogger.Log(RunnerLogCategory.WorkspaceInit, RunnerLogLevel.Info,
                    $"Workspace file watchers enabled ({WorkspaceWatchers.Count}).");
            }
            catch
            {
                RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                    "Workspace watcher initialization failed.");
                // best effort; if watcher fails, we won't auto-restart
            }
        }

        private static void OnWorkspaceFileChanged(object sender, System.IO.FileSystemEventArgs e)
        {
            try
            {
                RunnerLogger.Log(RunnerLogCategory.RunnerLifecycle, RunnerLogLevel.Info,
                    $"Workspace file change detected ({e.ChangeType}): {e.FullPath}");
                Environment.Exit(0); // signal host to restart runner
            }
            catch
            {
                // ignored
            }
        }

        private static void SafeDispose(IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        private sealed class PluginTypeCollection
        {
            public System.Collections.Generic.List<string> Types { get; set; } = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<StepInfo> Steps { get; set; } = new System.Collections.Generic.List<StepInfo>();
        }

        private static PluginTypeCollection CollectPluginTypes(PluginWorkspaceManifest manifest)
        {
            var collection = new PluginTypeCollection();
            var results = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var steps = new System.Collections.Generic.List<StepInfo>();
            if (manifest?.Assemblies == null || manifest.Assemblies.Count == 0)
            {
                RunnerLogger.Log(RunnerLogCategory.PluginCache, RunnerLogLevel.Debug,
                    "Plugin type collection skipped (no assemblies).");
                return collection;
            }

            var stopwatch = Stopwatch.StartNew();
            RunnerLogger.Log(RunnerLogCategory.PluginCache, RunnerLogLevel.Info,
                $"Collecting plugin types (assemblies={manifest.Assemblies.Count}).");
            EnsureSdkAssemblyResolver();

            foreach (var asmRef in manifest.Assemblies)
            {
                if (asmRef == null || !asmRef.Enabled)
                {
                    RunnerLogger.Log(RunnerLogCategory.PluginCache, RunnerLogLevel.Debug,
                        $"Assembly skipped (disabled): {asmRef?.Path ?? "(null)"}");
                    continue;
                }
                var resolvedAssemblyPath = ResolvePath(asmRef.Path);
                if (!File.Exists(resolvedAssemblyPath))
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                        $"Assembly not found: {resolvedAssemblyPath}");
                    continue;
                }
                var loadAssemblyPath = GetShadowCopyPath(resolvedAssemblyPath);
                RunnerLogger.Log(RunnerLogCategory.AssemblyLoad, RunnerLogLevel.Debug,
                    $"Loading assembly: {resolvedAssemblyPath}");

                var searchDirs = new System.Collections.Generic.List<string> { AppContext.BaseDirectory };
                var shadowDirs = new System.Collections.Generic.List<string>();
                var dir = Path.GetDirectoryName(resolvedAssemblyPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    searchDirs.Add(dir);
                    shadowDirs.Add(dir);
                }
                if (asmRef.DependencyFolders != null)
                {
                    foreach (var dep in asmRef.DependencyFolders)
                    {
                        var depPath = ResolvePath(dep);
                        if (Directory.Exists(depPath))
                        {
                            searchDirs.Add(depPath);
                            shadowDirs.Add(depPath);
                        }
                    }
                }

                ResolveEventHandler resolver = (s, e) =>
                {
                    var name = new AssemblyName(e.Name).Name + ".dll";
                    foreach (var d in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var candidate = Path.Combine(d, name);
                        if (File.Exists(candidate))
                        {
                            try
                            {
                                var candidateToLoad = ShouldShadowCopy(candidate, shadowDirs)
                                    ? GetShadowCopyPath(candidate)
                                    : candidate;
                                return Assembly.LoadFrom(candidateToLoad);
                            }
                            catch { }
                        }
                    }
                    return null;
                };

                AppDomain.CurrentDomain.AssemblyResolve += resolver;
                try
                {
                    var assembly = Assembly.LoadFrom(loadAssemblyPath);
                    RunnerLogger.Log(RunnerLogCategory.AssemblyLoad, RunnerLogLevel.Info,
                        $"Assembly loaded: {assembly.GetName().Name}");
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => t != null && typeof(IPlugin).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                        .ToList();
                    RunnerLogger.Log(RunnerLogCategory.PluginCache, RunnerLogLevel.Debug,
                        $"Assembly scan: {assembly.GetName().Name} plugin types={pluginTypes.Count}");

                    // If no explicit plugins found (edge cases), fall back to all classes so the user can pick.
                    var typesToAdd = pluginTypes.Any()
                        ? pluginTypes
                        : assembly.GetTypes().Where(t => t != null && t.IsClass);

                    foreach (var t in typesToAdd)
                    {
                        var name = t!.FullName ?? t.Name;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        results.Add(name);
                        steps.Add(new StepInfo
                        {
                            Assembly = Path.GetFileName(asmRef.Path ?? string.Empty) ?? string.Empty,
                            TypeName = name,
                            Stage = 40,
                            Mode = 0
                        });
                    }
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                        $"Assembly type load failed: {resolvedAssemblyPath} ({rtle.LoaderExceptions?.Length ?? 0} loader exceptions)");
                    if (rtle.Types != null)
                    {
                        var pluginTypes = rtle.Types
                            .Where(t => t != null && typeof(IPlugin).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                            .ToList();

                        var typesToAdd = pluginTypes.Any()
                            ? pluginTypes
                            : rtle.Types.Where(t => t != null && t.IsClass);

                        foreach (var t in typesToAdd)
                        {
                            var name = t!.FullName ?? t.Name;
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            results.Add(name);
                            steps.Add(new StepInfo
                            {
                                Assembly = Path.GetFileName(asmRef.Path ?? string.Empty) ?? string.Empty,
                                TypeName = name,
                                Stage = 40,
                                Mode = 0
                            });
                        }
                    }
                }
                catch
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                        $"Assembly scan failed: {resolvedAssemblyPath}");
                    // ignore errors for type collection
                }
                finally
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= resolver;
                }
            }

            collection.Types = results
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();
            collection.Steps = steps
                .Where(s => !string.IsNullOrWhiteSpace(s.TypeName))
                .OrderBy(s => s.TypeName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            stopwatch.Stop();
            RunnerLogger.Log(RunnerLogCategory.PluginCache, RunnerLogLevel.Info,
                $"Plugin type collection complete (types={collection.Types.Count}, steps={collection.Steps.Count}).");
            RunnerLogger.Log(RunnerLogCategory.Perf, RunnerLogLevel.Debug,
                $"CollectPluginTypes duration: {stopwatch.ElapsedMilliseconds} ms.");
            return collection;
        }

        internal static Entity? ParseEntityFromJson(
            string? json,
            string defaultLogicalName,
            Guid defaultId,
            AttributeMetadataResolver? attributeResolver,
            System.Collections.Generic.List<string>? conversionLog = null,
            string? logPrefix = null)
        {
            var jsonText = json ?? string.Empty;
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var root = doc.RootElement;
                var logicalName = defaultLogicalName;
                if (root.TryGetProperty("logicalName", out var lnProp) && lnProp.ValueKind == JsonValueKind.String)
                {
                    logicalName = lnProp.GetString() ?? logicalName;
                }
                if (string.IsNullOrWhiteSpace(logicalName))
                {
                    return null;
                }

                var entity = new Entity(logicalName);
                if (defaultId != Guid.Empty)
                {
                    entity.Id = defaultId;
                }

                var attributeMap = attributeResolver?.GetAttributeMap(logicalName);

                AddConversionLogLine(conversionLog, logPrefix, $"parsing entity '{logicalName}' (defaultId={defaultId})");

                foreach (var prop in root.EnumerateObject())
                {
                    var name = prop.Name;
                    if (string.Equals(name, "logicalName", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Guid.TryParse(prop.Value.GetString(), out var guid))
                        {
                            entity.Id = guid;
                        }
                        continue;
                    }

                    object? value = null;
                    switch (prop.Value.ValueKind)
                    {
                        case JsonValueKind.String:
                            var s = prop.Value.GetString();
                            if (Guid.TryParse(s, out var g))
                            {
                                value = g;
                            }
                            else
                            {
                                value = s;
                            }
                            break;
                        case JsonValueKind.Number:
                            if (prop.Value.TryGetInt64(out var l)) value = l;
                            else if (prop.Value.TryGetDouble(out var d)) value = d;
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            value = prop.Value.GetBoolean();
                            break;
                        case JsonValueKind.Object:
                            if (TryGetPropertyIgnoreCase(prop.Value, "id", out var idProp)
                                && Guid.TryParse(idProp.GetString(), out var refId))
                            {
                                string? refLogicalName = null;
                                if (TryGetPropertyIgnoreCase(prop.Value, "logicalName", out var lnPropRef)
                                    || TryGetPropertyIgnoreCase(prop.Value, "entityType", out lnPropRef))
                                {
                                    refLogicalName = lnPropRef.GetString();
                                }

                                if (!string.IsNullOrWhiteSpace(refLogicalName))
                                {
                                    value = new EntityReference(refLogicalName, refId);
                                }
                                else
                                {
                                    value = refId;
                                }
                            }
                            break;
                        default:
                            value = prop.Value.ToString();
                            break;
                    }

                    var normalized = NormalizeAttributeValue(name, value, attributeMap, conversionLog, logPrefix, logicalName);
                    if (normalized != null)
                    {
                        entity[name] = normalized;
                    }
                }

                AddConversionLogLine(conversionLog, logPrefix, $"entity '{logicalName}' parsed with {entity.Attributes.Count} attributes");
                return entity;
            }
            catch
            {
                return null;
            }
        }

        private static object? NormalizeAttributeValue(
            string attributeName,
            object? value,
            System.Collections.Generic.Dictionary<string, AttributeShape>? attributeMap,
            System.Collections.Generic.List<string>? conversionLog,
            string? logPrefix,
            string entityLogicalName)
        {
            if (value == null || attributeMap == null)
            {
                return value;
            }

            if (!attributeMap.TryGetValue(attributeName, out var metadata) || metadata == null)
            {
                LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"metadata missing; value remains {value?.GetType().Name ?? "null"}");
                return value;
            }

            if (value is EntityReference || value is Money || value is OptionSetValue || value is OptionSetValueCollection)
            {
                return value;
            }

            switch (metadata.AttributeType)
            {
                case AttributeTypeCode.Boolean:
                    if (TryConvertToBoolean(value, out var boolValue))
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"Boolean -> {boolValue}");
                        return boolValue;
                    }
                    break;
                case AttributeTypeCode.DateTime:
                    if (TryConvertToDateTime(value, out var dateValue))
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"DateTime -> {dateValue:o}");
                        return dateValue;
                    }
                    break;
                case AttributeTypeCode.Decimal:
                    if (TryConvertToDecimal(value, out var decimalValue))
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"Decimal -> {decimalValue}");
                        return decimalValue;
                    }
                    break;
                case AttributeTypeCode.Double:
                    if (TryConvertToDouble(value, out var doubleValue))
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"Double -> {doubleValue}");
                        return doubleValue;
                    }
                    break;
                case AttributeTypeCode.Integer:
                    if (TryConvertToInt(value, out var intValue))
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"Integer -> {intValue}");
                        return intValue;
                    }
                    break;
                case AttributeTypeCode.BigInt:
                    if (TryConvertToLong(value, out var longValue))
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"BigInt -> {longValue}");
                        return longValue;
                    }
                    break;
                case AttributeTypeCode.Uniqueidentifier:
                    if (TryConvertToGuid(value, out var guidValue))
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"Guid -> {guidValue}");
                        return guidValue;
                    }
                    break;
                case AttributeTypeCode.Money:
                    var money = ConvertToMoney(value);
                    if (money != null)
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"Money -> {money.Value}");
                        return money;
                    }
                    break;
                case AttributeTypeCode.Picklist:
                case AttributeTypeCode.State:
                case AttributeTypeCode.Status:
                    if (TryConvertToInt(value, out var optionValue))
                    {
                        LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"OptionSetValue -> {optionValue}");
                        return new OptionSetValue(optionValue);
                    }
                    break;
            }

            if (IsMultiSelectPicklist(metadata))
            {
                var collection = ConvertToOptionSetCollection(value);
                if (collection != null)
                {
                    var values = string.Join(",", collection.Select(v => v?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
                    LogAttributeConversion(conversionLog, logPrefix, attributeName, entityLogicalName, $"OptionSetValueCollection -> [{values}]");
                    return collection;
                }
            }

            return value;
        }

        private static string BuildConversionLogLabel(string? prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return "Conversion";
            }

            return prefix!;
        }

        private static void AddConversionLogLine(
            System.Collections.Generic.List<string>? log,
            string? prefix,
            string message)
        {
            var label = BuildConversionLogLabel(prefix);
            var line = $"{label}: {message}";
            if (log != null)
            {
                log.Add(line);
            }

            RunnerLogger.Log(RunnerLogCategory.Emulator, RunnerLogLevel.Info, line);
        }

        private static void LogAttributeConversion(
            System.Collections.Generic.List<string>? log,
            string? prefix,
            string attributeName,
            string entityLogicalName,
            string message)
        {
            AddConversionLogLine(log, prefix, $"{entityLogicalName}.{attributeName} {message}");
        }

        private static bool TryConvertToInt(object? value, out int result)
        {
            switch (value)
            {
                case int intValue:
                    result = intValue;
                    return true;
                case bool boolValue:
                    result = boolValue ? 1 : 0;
                    return true;
                case long longValue when longValue <= int.MaxValue && longValue >= int.MinValue:
                    result = (int)longValue;
                    return true;
                case short shortValue:
                    result = shortValue;
                    return true;
                case byte byteValue:
                    result = byteValue;
                    return true;
                case double doubleValue when doubleValue <= int.MaxValue && doubleValue >= int.MinValue:
                    result = (int)Math.Round(doubleValue);
                    return true;
                case float floatValue when floatValue <= int.MaxValue && floatValue >= int.MinValue:
                    result = (int)Math.Round(floatValue);
                    return true;
                case decimal decimalValue when decimalValue <= int.MaxValue && decimalValue >= int.MinValue:
                    result = (int)Math.Round(decimalValue);
                    return true;
                case string stringValue when int.TryParse(stringValue, out var parsed):
                    result = parsed;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        private static bool TryConvertToLong(object? value, out long result)
        {
            switch (value)
            {
                case long longValue:
                    result = longValue;
                    return true;
                case int intValue:
                    result = intValue;
                    return true;
                case short shortValue:
                    result = shortValue;
                    return true;
                case byte byteValue:
                    result = byteValue;
                    return true;
                case double doubleValue when doubleValue <= long.MaxValue && doubleValue >= long.MinValue:
                    result = (long)Math.Round(doubleValue);
                    return true;
                case float floatValue when floatValue <= long.MaxValue && floatValue >= long.MinValue:
                    result = (long)Math.Round(floatValue);
                    return true;
                case decimal decimalValue when decimalValue <= long.MaxValue && decimalValue >= long.MinValue:
                    result = (long)Math.Round(decimalValue);
                    return true;
                case string stringValue when long.TryParse(stringValue, out var parsed):
                    result = parsed;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        private static bool TryConvertToDouble(object? value, out double result)
        {
            switch (value)
            {
                case double doubleValue:
                    result = doubleValue;
                    return true;
                case float floatValue:
                    result = floatValue;
                    return true;
                case decimal decimalValue:
                    result = (double)decimalValue;
                    return true;
                case long longValue:
                    result = longValue;
                    return true;
                case int intValue:
                    result = intValue;
                    return true;
                case string stringValue when double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                    result = parsed;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        private static bool TryConvertToDecimal(object? value, out decimal result)
        {
            switch (value)
            {
                case decimal decimalValue:
                    result = decimalValue;
                    return true;
                case double doubleValue when doubleValue <= (double)decimal.MaxValue && doubleValue >= (double)decimal.MinValue:
                    result = (decimal)doubleValue;
                    return true;
                case float floatValue when floatValue <= (float)decimal.MaxValue && floatValue >= (float)decimal.MinValue:
                    result = (decimal)floatValue;
                    return true;
                case long longValue:
                    result = longValue;
                    return true;
                case int intValue:
                    result = intValue;
                    return true;
                case string stringValue when decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                    result = parsed;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        private static bool TryConvertToBoolean(object? value, out bool result)
        {
            switch (value)
            {
                case bool boolValue:
                    result = boolValue;
                    return true;
                case int intValue:
                    result = intValue != 0;
                    return true;
                case long longValue:
                    result = longValue != 0;
                    return true;
                case double doubleValue:
                    result = Math.Abs(doubleValue) > double.Epsilon;
                    return true;
                case float floatValue:
                    result = Math.Abs(floatValue) > float.Epsilon;
                    return true;
                case string stringValue:
                    if (bool.TryParse(stringValue, out var parsedBool))
                    {
                        result = parsedBool;
                        return true;
                    }
                    if (int.TryParse(stringValue, out var parsedInt))
                    {
                        result = parsedInt != 0;
                        return true;
                    }
                    break;
            }

            result = false;
            return false;
        }

        private static bool TryConvertToDateTime(object? value, out DateTime result)
        {
            switch (value)
            {
                case DateTime dateTime:
                    result = dateTime;
                    return true;
                case string stringValue:
                    if (DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                    {
                        result = parsed;
                        return true;
                    }
                    break;
            }

            result = default;
            return false;
        }

        private static bool TryConvertToGuid(object? value, out Guid result)
        {
            switch (value)
            {
                case Guid guid:
                    result = guid;
                    return true;
                case string stringValue when Guid.TryParse(stringValue, out var parsed):
                    result = parsed;
                    return true;
            }

            result = Guid.Empty;
            return false;
        }

        private static Money? ConvertToMoney(object? value)
        {
            if (value is Money existing)
            {
                return existing;
            }

            if (TryConvertToDecimal(value, out var amount))
            {
                return new Money(amount);
            }

            return null;
        }

        private static bool IsMultiSelectPicklist(AttributeShape? metadata)
        {
            if (metadata is not AttributeShape meta)
            {
                return false;
            }

                 var typeName = meta.AttributeTypeName;
                 if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

                 var nonEmptyTypeName = typeName!;
                 return meta.AttributeType == AttributeTypeCode.Virtual &&
                     nonEmptyTypeName.IndexOf("MultiSelectPicklist", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static OptionSetValueCollection? ConvertToOptionSetCollection(object? value)
        {
            if (value == null)
            {
                return null;
            }

            var collection = new OptionSetValueCollection();
            var added = false;

            if (value is string stringValue)
            {
                var parts = stringValue
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (int.TryParse(part.Trim(), out var parsed))
                    {
                        collection.Add(new OptionSetValue(parsed));
                        added = true;
                    }
                }
                return added ? collection : null;
            }

            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                foreach (var item in enumerable)
                {
                    if (TryConvertToInt(item, out var parsed))
                    {
                        collection.Add(new OptionSetValue(parsed));
                        added = true;
                    }
                }
                return added ? collection : null;
            }

            if (TryConvertToInt(value, out var single))
            {
                collection.Add(new OptionSetValue(single));
                return collection;
            }

            return null;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        internal static Guid TryParseGuid(string? input)
        {
            if (Guid.TryParse(input, out var guid))
            {
                return guid;
            }
            return Guid.Empty;
        }

        internal static void EnsureSdkAssemblyResolver()
        {
            if (_sdkResolverRegistered) return;
            _sdkResolverRegistered = true;

            RunnerLogger.Log(RunnerLogCategory.AssemblyLoad, RunnerLogLevel.Info,
                "SDK assembly resolver registered.");
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var requestedName = new AssemblyName(args.Name).Name ?? string.Empty;
                if (requestedName.Equals("Microsoft.Xrm.Sdk", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadSdkAssembly("Microsoft.Xrm.Sdk.dll");
                }
                if (requestedName.Equals("Microsoft.Crm.Sdk.Proxy", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadSdkAssembly("Microsoft.Crm.Sdk.Proxy.dll");
                }
                return null;
            };
        }

        private static Assembly? LoadSdkAssembly(string fileName)
        {
            var existing = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name + ".dll", fileName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                RunnerLogger.Log(RunnerLogCategory.AssemblyLoad, RunnerLogLevel.Debug,
                    $"SDK assembly already loaded: {fileName}");
                return existing;
            }

            var candidate = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(candidate))
            {
                try
                {
                    var loaded = Assembly.LoadFrom(candidate);
                    RunnerLogger.Log(RunnerLogCategory.AssemblyLoad, RunnerLogLevel.Info,
                        $"SDK assembly loaded: {candidate}");
                    return loaded;
                }
                catch
                {
                    // ignore load failures; caller will continue probing
                }
            }

            RunnerLogger.Log(RunnerLogCategory.AssemblyLoad, RunnerLogLevel.Debug,
                $"SDK assembly not found: {fileName}");
            return null;
        }

        private sealed class ShadowCopyInfo
        {
            public string ShadowPath { get; set; } = string.Empty;
            public DateTime LastWriteUtc { get; set; }
            public long Length { get; set; }
        }

        internal static string GetShadowCopyPath(string originalPath)
        {
            if (string.IsNullOrWhiteSpace(originalPath))
            {
                return originalPath;
            }

            try
            {
                if (IsUnderDirectory(originalPath, ShadowRoot))
                {
                    return originalPath;
                }

                var info = new FileInfo(originalPath);
                if (!info.Exists)
                {
                    return originalPath;
                }

                lock (ShadowCopyLock)
                {
                    if (ShadowCopies.TryGetValue(originalPath, out var cached) &&
                        cached.LastWriteUtc == info.LastWriteTimeUtc &&
                        cached.Length == info.Length &&
                        File.Exists(cached.ShadowPath))
                    {
                        return cached.ShadowPath;
                    }

                    Directory.CreateDirectory(ShadowRoot);
                    var name = Path.GetFileNameWithoutExtension(originalPath);
                    var ext = Path.GetExtension(originalPath);
                    var hashFolder = ComputeAssemblyHash(info);
                    var shadowDir = Path.Combine(ShadowRoot, hashFolder);
                    Directory.CreateDirectory(shadowDir);
                    var shadowPath = Path.Combine(shadowDir, $"{name}{ext}");

                    if (!TryCopyFileWithRetry(originalPath, shadowPath, attempts: 8, delayMs: 150))
                    {
                        RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info,
                            $"Shadow copy failed: {originalPath}");
                        return originalPath;
                    }

                    var pdb = Path.ChangeExtension(originalPath, ".pdb");
                    if (File.Exists(pdb))
                    {
                        var pdbShadow = Path.Combine(shadowDir, $"{name}.pdb");
                        TryCopyFileWithRetry(pdb, pdbShadow, attempts: 8, delayMs: 150);
                    }

                    CopyRelatedFiles(originalPath, shadowDir);

                    ShadowCopies[originalPath] = new ShadowCopyInfo
                    {
                        ShadowPath = shadowPath,
                        LastWriteUtc = info.LastWriteTimeUtc,
                        Length = info.Length
                    };

                    RunnerLogger.Log(RunnerLogCategory.AssemblyLoad, RunnerLogLevel.Info,
                        $"Shadow copy created: {originalPath} -> {shadowPath}");
                    return shadowPath;
                }
            }
            catch
            {
                return originalPath;
            }
        }

        private static readonly string[] CopiedExtensions = new[]
        {
            ".dll",
            ".pdb",
            ".json",
            ".config",
            ".xml"
        };

        private static void CopyRelatedFiles(string originalAssemblyPath, string shadowDir)
        {
            try
            {
                var directory = Path.GetDirectoryName(originalAssemblyPath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                foreach (var candidate in Directory.EnumerateFiles(directory))
                {
                    if (!ShouldCopyRelatedFile(originalAssemblyPath, candidate))
                    {
                        continue;
                    }

                    var target = Path.Combine(shadowDir, Path.GetFileName(candidate));
                    TryCopyFileWithRetry(candidate, target, attempts: 4, delayMs: 100);
                }
            }
            catch
            {
                // best effort; missing dependencies will still be copied on-demand
            }
        }

        private static string ComputeAssemblyHash(FileInfo info)
        {
            try
            {
                using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(stream);
                if (hash.Length == 0)
                {
                   return BuildShadowFallback(info);
                }

                var builder = new StringBuilder(8);
                var bytesToUse = Math.Min(4, hash.Length);
                for (var i = 0; i < bytesToUse; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.Length > 0 ? builder.ToString() : BuildShadowFallback(info);
            }
            catch
            {
                return BuildShadowFallback(info);
            }
        }

        private static string BuildShadowFallback(FileInfo info)
        {
            var fallback = info.LastWriteTimeUtc.Ticks.ToString("x", CultureInfo.InvariantCulture);
            if (fallback.Length <= 16)
            {
                return fallback;
            }

            var start = fallback.Length - 16;
            return fallback.Substring(start, 16);
        }

        private static bool ShouldCopyRelatedFile(string originalAssemblyPath, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            if (string.Equals(originalAssemblyPath, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var ext = Path.GetExtension(candidate);
            if (string.IsNullOrEmpty(ext))
            {
                return false;
            }

            return CopiedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private static bool TryCopyFileWithRetry(string sourcePath, string destinationPath, int attempts, int delayMs)
        {
            for (var i = 0; i < attempts; i++)
            {
                try
                {
                    CopyFileAllowReadWrite(sourcePath, destinationPath);
                    return true;
                }
                catch
                {
                    if (i == attempts - 1)
                    {
                        break;
                    }
                    Thread.Sleep(delayMs);
                }
            }

            return false;
        }

        private static void CopyFileAllowReadWrite(string sourcePath, string destinationPath)
        {
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var dest = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(dest);
        }

        internal static bool ShouldShadowCopy(string candidatePath, System.Collections.Generic.IEnumerable<string> shadowDirs)
        {
            foreach (var dir in shadowDirs)
            {
                if (IsUnderDirectory(candidatePath, dir))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsUnderDirectory(string path, string directory)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                var fullDir = Path.GetFullPath(directory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                return fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
