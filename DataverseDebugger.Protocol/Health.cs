namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Health status of the runner process.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>Status is not yet known.</summary>
        Unknown = 0,

        /// <summary>Runner is starting up.</summary>
        Starting = 1,

        /// <summary>Runner is ready to accept requests.</summary>
        Ready = 2,

        /// <summary>Runner is operational but experiencing issues.</summary>
        Degraded = 3,

        /// <summary>Runner has encountered a fatal error.</summary>
        Error = 4
    }

    /// <summary>
    /// Request to check runner health and capabilities.
    /// </summary>
    public sealed class HealthCheckRequest
    {
        /// <summary>Protocol version of the requesting client.</summary>
        public int Version { get; set; } = ProtocolVersion.Current;
    }

    /// <summary>
    /// Response containing runner health status and capabilities.
    /// </summary>
    public sealed class HealthCheckResponse
    {
        /// <summary>Protocol version of the runner.</summary>
        public int Version { get; set; } = ProtocolVersion.Current;

        /// <summary>Current health status of the runner.</summary>
        public HealthStatus Status { get; set; } = HealthStatus.Unknown;

        /// <summary>Capability flags indicating supported features.</summary>
        public CapabilityFlags Capabilities { get; set; } = CapabilityFlags.None;

        /// <summary>Optional status message or error details.</summary>
        public string? Message { get; set; }
    }
}
