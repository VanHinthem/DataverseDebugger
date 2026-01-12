using System;
using System.Collections.Generic;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Request to initialize the runner workspace with environment and plugin configuration.
    /// Command: "initWorkspace"
    /// </summary>
    public sealed class InitializeWorkspaceRequest
    {
        /// <summary>Protocol version.</summary>
        public int Version { get; set; } = ProtocolVersion.Current;

        /// <summary>Environment configuration.</summary>
        public EnvConfig Environment { get; set; } = new EnvConfig();

        /// <summary>Plugin workspace manifest.</summary>
        public PluginWorkspaceManifest Workspace { get; set; } = new PluginWorkspaceManifest();
    }

    /// <summary>
    /// Response after workspace initialization.
    /// </summary>
    public sealed class InitializeWorkspaceResponse
    {
        /// <summary>Protocol version.</summary>
        public int Version { get; set; } = ProtocolVersion.Current;

        /// <summary>Status of the runner after initialization.</summary>
        public HealthStatus Status { get; set; } = HealthStatus.Unknown;

        /// <summary>Optional status message or error details.</summary>
        public string? Message { get; set; }

        /// <summary>List of discovered plugin type names.</summary>
        public List<string> PluginTypes { get; set; } = new List<string>();

        /// <summary>List of registered plugin steps.</summary>
        public List<StepInfo> Steps { get; set; } = new List<StepInfo>();
    }

    /// <summary>
    /// Request to execute an intercepted HTTP request.
    /// Command: "execute"
    /// </summary>
    public sealed class ExecuteRequest
    {
        /// <summary>Unique identifier for this request.</summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>The intercepted HTTP request to process.</summary>
        public InterceptedHttpRequest Request { get; set; } = new InterceptedHttpRequest();

        /// <summary>Force proxying to the server instead of local emulation.</summary>
        public bool ForceProxy { get; set; }

        /// <summary>Skip authentication preflight checks.</summary>
        public bool BypassAuthPreflight { get; set; }
    }

    /// <summary>
    /// Response from executing an intercepted HTTP request.
    /// </summary>
    public sealed class ExecuteResponse
    {
        /// <summary>Request ID matching the original request.</summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>The HTTP response to return to the client.</summary>
        public InterceptedHttpResponse Response { get; set; } = new InterceptedHttpResponse();

        /// <summary>Execution trace information.</summary>
        public ExecutionTrace Trace { get; set; } = new ExecutionTrace();
    }

    /// <summary>
    /// Streaming trace output during request execution.
    /// </summary>
    public sealed class ExecuteTrace
    {
        /// <summary>Request ID this trace belongs to.</summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>Trace log lines.</summary>
        public List<string> TraceLines { get; set; } = new List<string>();
    }

    /// <summary>
    /// Request to directly invoke a plugin.
    /// Command: "pluginInvoke"
    /// </summary>
    public sealed class PluginInvokeRequest
    {
        /// <summary>Unique identifier for this request.</summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Assembly name containing the plugin.</summary>
        public string Assembly { get; set; } = string.Empty;

        /// <summary>Fully qualified type name of the plugin class.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Message name (e.g., Create, Update, Delete).</summary>
        public string MessageName { get; set; } = string.Empty;

        /// <summary>Primary entity logical name.</summary>
        public string PrimaryEntityName { get; set; } = string.Empty;

        /// <summary>Primary entity ID (GUID as string).</summary>
        public string PrimaryEntityId { get; set; } = string.Empty;

        /// <summary>Pipeline stage (10=PreValidation, 20=PreOperation, 40=PostOperation).</summary>
        public int Stage { get; set; } = 40;

        /// <summary>Execution mode (0=Synchronous, 1=Asynchronous).</summary>
        public int Mode { get; set; } = 0;

        /// <summary>Organization URL for service calls.</summary>
        public string? OrgUrl { get; set; }

        /// <summary>Access token for authentication.</summary>
        public string? AccessToken { get; set; }

        /// <summary>Write mode for the organization service.</summary>
        public string? WriteMode { get; set; }

        /// <summary>Execution mode for the organization service.</summary>
        public string? ExecutionMode { get; set; }

        /// <summary>Original HTTP request that triggered this plugin.</summary>
        public InterceptedHttpRequest? HttpRequest { get; set; }

        /// <summary>JSON-serialized Target entity.</summary>
        public string? TargetJson { get; set; }

        /// <summary>JSON-serialized PreImage entity.</summary>
        public string? PreImageJson { get; set; }

        /// <summary>JSON-serialized PostImage entity.</summary>
        public string? PostImageJson { get; set; }

        /// <summary>Additional entity images.</summary>
        public List<PluginImagePayload> Images { get; set; } = new List<PluginImagePayload>();

        /// <summary>Unsecure configuration string.</summary>
        public string? UnsecureConfiguration { get; set; }

        /// <summary>Secure configuration string.</summary>
        public string? SecureConfiguration { get; set; }
    }

    /// <summary>
    /// Entity image data for plugin execution context.
    /// </summary>
    public sealed class PluginImagePayload
    {
        /// <summary>Image type: "PreImage" or "PostImage".</summary>
        public string ImageType { get; set; } = string.Empty;

        /// <summary>Entity alias for the image.</summary>
        public string EntityAlias { get; set; } = string.Empty;

        /// <summary>JSON-serialized entity data.</summary>
        public string EntityJson { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from a direct plugin invocation.
    /// </summary>
    public sealed class PluginInvokeResponse
    {
        /// <summary>Request ID matching the original request.</summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>Execution status.</summary>
        public HealthStatus Status { get; set; } = HealthStatus.Unknown;

        /// <summary>Status message or error details.</summary>
        public string? Message { get; set; }

        /// <summary>Trace log lines from plugin execution.</summary>
        public List<string> TraceLines { get; set; } = new List<string>();
    }

    /// <summary>
    /// Information about a registered plugin step.
    /// </summary>
    public sealed class StepInfo
    {
        /// <summary>Assembly name containing the plugin.</summary>
        public string Assembly { get; set; } = string.Empty;

        /// <summary>Fully qualified type name of the plugin class.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Message name (e.g., Create, Update, Delete).</summary>
        public string MessageName { get; set; } = string.Empty;

        /// <summary>Primary entity logical name.</summary>
        public string PrimaryEntity { get; set; } = string.Empty;

        /// <summary>Pipeline stage.</summary>
        public int Stage { get; set; }

        /// <summary>Execution mode.</summary>
        public int Mode { get; set; }

        /// <summary>Execution rank/order.</summary>
        public int Rank { get; set; }

        /// <summary>Attributes that trigger this step (for filtered steps).</summary>
        public List<string> FilteringAttributes { get; set; } = new List<string>();

        /// <summary>Unsecure configuration string.</summary>
        public string? UnsecureConfiguration { get; set; }

        /// <summary>Secure configuration string.</summary>
        public string? SecureConfiguration { get; set; }
    }
}
