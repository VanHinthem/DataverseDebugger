using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.App
{
    /// <summary>
    /// Client for communicating with the DataverseDebugger.Runner process via named pipes.
    /// </summary>
    /// <remarks>
    /// This class provides methods for sending commands to the runner process and receiving responses,
    /// including health checks, workspace initialization, request execution, and plugin invocation.
    /// </remarks>
    public sealed class RunnerClient
    {
        private int _activeRunnerRequests;

        /// <summary>
        /// Gets whether there are active requests being processed by the runner.
        /// </summary>
        public bool HasActiveRequests => Volatile.Read(ref _activeRunnerRequests) > 0;

        /// <summary>
        /// Updates the runner's logging configuration.
        /// </summary>
        /// <param name="request">The log configuration request.</param>
        /// <param name="timeout">The timeout for the operation.</param>
        /// <returns>The response indicating whether the configuration was applied.</returns>
        public async Task<RunnerLogConfigResponse> UpdateRunnerLogConfigAsync(RunnerLogConfigRequest request, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeNames.RunnerPipe,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                await PipeProtocol.WriteAsync(pipe, "runnerLogConfig", request, cts.Token).ConfigureAwait(false);
                var message = await PipeProtocol.ReadAsync(pipe, cts.Token).ConfigureAwait(false);

                if (message == null || string.IsNullOrEmpty(message.Payload))
                {
                    return new RunnerLogConfigResponse
                    {
                        Applied = false,
                        Message = "Empty response from runner"
                    };
                }

                var response = System.Text.Json.JsonSerializer.Deserialize<RunnerLogConfigResponse>(message.Payload);
                return response ?? new RunnerLogConfigResponse
                {
                    Applied = false,
                    Message = "Unable to parse runner log config response"
                };
            }
            catch (OperationCanceledException)
            {
                return new RunnerLogConfigResponse
                {
                    Applied = false,
                    Message = "Timeout contacting runner"
                };
            }
            catch (Exception ex)
            {
                return new RunnerLogConfigResponse
                {
                    Applied = false,
                    Message = $"Runner log config failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Fetches log entries from the runner process.
        /// </summary>
        /// <param name="request">The log fetch request with filtering options.</param>
        /// <param name="timeout">The timeout for the operation.</param>
        /// <returns>The log entries, or null on failure.</returns>
        public async Task<RunnerLogFetchResponse?> FetchRunnerLogsAsync(RunnerLogFetchRequest request, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeNames.RunnerPipe,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                await PipeProtocol.WriteAsync(pipe, "runnerLogFetch", request, cts.Token).ConfigureAwait(false);
                var message = await PipeProtocol.ReadAsync(pipe, cts.Token).ConfigureAwait(false);

                if (message == null || string.IsNullOrEmpty(message.Payload))
                {
                    return null;
                }

                return System.Text.Json.JsonSerializer.Deserialize<RunnerLogFetchResponse>(message.Payload);
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// Checks the health status of the runner process.
        /// </summary>
        /// <param name="timeout">The timeout for the operation.</param>
        /// <returns>The health check response with status and any error messages.</returns>
        public async Task<HealthCheckResponse> CheckHealthAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeNames.RunnerPipe,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                await PipeProtocol.WriteAsync(pipe, "health", new HealthCheckRequest(), cts.Token).ConfigureAwait(false);
                var message = await PipeProtocol.ReadAsync(pipe, cts.Token).ConfigureAwait(false);

                if (message == null || string.IsNullOrEmpty(message.Payload))
                {
                    return new HealthCheckResponse
                    {
                        Status = HealthStatus.Error,
                        Message = "Empty response from runner"
                    };
                }

                if (!string.Equals(message.Command, "healthResponse", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(message.Command, "error", StringComparison.OrdinalIgnoreCase))
                {
                    return new HealthCheckResponse
                    {
                        Status = HealthStatus.Error,
                        Message = $"Unexpected command: {message.Command}"
                    };
                }

                var response = System.Text.Json.JsonSerializer.Deserialize<HealthCheckResponse>(message.Payload);
                return response ?? new HealthCheckResponse
                {
                    Status = HealthStatus.Error,
                    Message = "Unable to parse runner response"
                };
            }
            catch (OperationCanceledException)
            {
                return new HealthCheckResponse
                {
                    Status = HealthStatus.Error,
                    Message = "Timeout contacting runner"
                };
            }
            catch (Exception ex)
            {
                return new HealthCheckResponse
                {
                    Status = HealthStatus.Error,
                    Message = $"Runner health check failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Initializes the runner workspace with environment and plugin configuration.
        /// </summary>
        /// <param name="request">The workspace initialization request.</param>
        /// <param name="timeout">The timeout for the operation.</param>
        /// <returns>The initialization response with status.</returns>
        public async Task<InitializeWorkspaceResponse> InitializeWorkspaceAsync(InitializeWorkspaceRequest request, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeNames.RunnerPipe,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                await PipeProtocol.WriteAsync(pipe, "initWorkspace", request, cts.Token).ConfigureAwait(false);
                var message = await PipeProtocol.ReadAsync(pipe, cts.Token).ConfigureAwait(false);

                if (message == null || string.IsNullOrEmpty(message.Payload))
                {
                    return new InitializeWorkspaceResponse
                    {
                        Status = HealthStatus.Error,
                        Message = "Empty response from runner"
                    };
                }

                var response = System.Text.Json.JsonSerializer.Deserialize<InitializeWorkspaceResponse>(message.Payload);
                return response ?? new InitializeWorkspaceResponse
                {
                    Status = HealthStatus.Error,
                    Message = "Unable to parse init response"
                };
            }
            catch (OperationCanceledException)
            {
                return new InitializeWorkspaceResponse
                {
                    Status = HealthStatus.Error,
                    Message = "Timeout contacting runner"
                };
            }
            catch (Exception ex)
            {
                return new InitializeWorkspaceResponse
                {
                    Status = HealthStatus.Error,
                    Message = $"Init failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Executes a Web API request through the runner's plugin pipeline.
        /// </summary>
        /// <param name="request">The execute request containing the HTTP request to process.</param>
        /// <param name="timeout">The timeout for the operation.</param>
        /// <param name="onTrace">Optional callback for receiving trace messages as they arrive.</param>
        /// <returns>The execute response with the HTTP response and execution trace.</returns>
        public async Task<ExecuteResponse> ExecuteAsync(ExecuteRequest request, TimeSpan timeout, Action<string>? onTrace = null)
        {
            try
            {
                Interlocked.Increment(ref _activeRunnerRequests);
                using var cts = new CancellationTokenSource(timeout);
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeNames.RunnerPipe,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                await PipeProtocol.WriteAsync(pipe, "execute", request, cts.Token).ConfigureAwait(false);

                var traceBuffer = new System.Collections.Generic.List<string>();
                ExecuteResponse? final = null;

                while (true)
                {
                    var message = await PipeProtocol.ReadAsync(pipe, cts.Token).ConfigureAwait(false);
                    if (message == null)
                    {
                        break;
                    }

                    if (string.Equals(message.Command, "executeTrace", StringComparison.OrdinalIgnoreCase))
                    {
                        var trace = System.Text.Json.JsonSerializer.Deserialize<ExecuteTrace>(message.Payload ?? string.Empty);
                        if (trace?.TraceLines != null)
                        {
                            foreach (var line in trace.TraceLines)
                            {
                                traceBuffer.Add(line);
                                onTrace?.Invoke(line);
                            }
                        }
                        continue;
                    }

                    if (string.Equals(message.Command, "executeResponse", StringComparison.OrdinalIgnoreCase))
                    {
                        final = System.Text.Json.JsonSerializer.Deserialize<ExecuteResponse>(message.Payload ?? string.Empty);
                        break;
                    }
                }

                if (final == null)
                {
                    return new ExecuteResponse
                    {
                        RequestId = request.RequestId,
                        Response = new InterceptedHttpResponse { StatusCode = 500 },
                        Trace = new ExecutionTrace { TraceLines = traceBuffer.Count > 0 ? traceBuffer : new System.Collections.Generic.List<string> { "Empty response from runner" } }
                    };
                }

                if (final.Trace == null)
                {
                    final.Trace = new ExecutionTrace { TraceLines = new System.Collections.Generic.List<string>() };
                }

                if (final.Trace.TraceLines == null)
                {
                    final.Trace.TraceLines = new System.Collections.Generic.List<string>();
                }

                if (traceBuffer.Count > 0)
                {
                    // Trace messages are streamed as deltas; use the collected buffer as final output.
                    final.Trace.TraceLines = traceBuffer;
                }

                return final;
            }
            catch (OperationCanceledException)
            {
                return new ExecuteResponse
                {
                    RequestId = request.RequestId,
                    Response = new InterceptedHttpResponse { StatusCode = 504 },
                    Trace = new ExecutionTrace { TraceLines = new System.Collections.Generic.List<string> { "Timeout contacting runner (client)" } }
                };
            }
            catch (Exception ex)
            {
                return new ExecuteResponse
                {
                    RequestId = request.RequestId,
                    Response = new InterceptedHttpResponse { StatusCode = 500 },
                    Trace = new ExecutionTrace { TraceLines = new System.Collections.Generic.List<string> { $"Execute failed: {ex.Message}" } }
                };
            }
            finally
            {
                Interlocked.Decrement(ref _activeRunnerRequests);
            }
        }

        /// <summary>
        /// Invokes a specific plugin step directly without going through the full request pipeline.
        /// </summary>
        /// <param name="request">The plugin invoke request with step configuration and context.</param>
        /// <param name="timeout">The timeout for the operation.</param>
        /// <returns>The plugin invoke response with status and any error messages.</returns>
        public async Task<PluginInvokeResponse> ExecutePluginAsync(PluginInvokeRequest request, TimeSpan timeout)
        {
            try
            {
                Interlocked.Increment(ref _activeRunnerRequests);
                using var cts = new CancellationTokenSource(timeout);
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeNames.RunnerPipe,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                await PipeProtocol.WriteAsync(pipe, "executePlugin", request, cts.Token).ConfigureAwait(false);
                var message = await PipeProtocol.ReadAsync(pipe, cts.Token).ConfigureAwait(false);

                if (message == null || string.IsNullOrEmpty(message.Payload))
                {
                    return new PluginInvokeResponse
                    {
                        Status = HealthStatus.Error,
                        Message = "Empty response from runner"
                    };
                }

                var response = System.Text.Json.JsonSerializer.Deserialize<PluginInvokeResponse>(message.Payload);
                return response ?? new PluginInvokeResponse
                {
                    Status = HealthStatus.Error,
                    Message = "Unable to parse plugin response"
                };
            }
            catch (OperationCanceledException)
            {
                return new PluginInvokeResponse
                {
                    Status = HealthStatus.Error,
                    Message = "Timeout contacting runner"
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
                Interlocked.Decrement(ref _activeRunnerRequests);
            }
        }
    }
}
