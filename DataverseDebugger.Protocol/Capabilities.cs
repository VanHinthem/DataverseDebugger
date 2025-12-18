using System;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Flags indicating the capabilities supported by the runner.
    /// </summary>
    [Flags]
    public enum CapabilityFlags
    {
        /// <summary>No capabilities.</summary>
        None = 0,

        /// <summary>Supports streaming trace output during execution.</summary>
        TraceStreaming = 1 << 0,

        /// <summary>Supports returning a catalog of registered plugin steps.</summary>
        StepCatalog = 1 << 1,

        /// <summary>Supports batch request processing.</summary>
        BatchSupport = 1 << 2
    }
}
