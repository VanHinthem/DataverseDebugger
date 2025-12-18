using System;
using System.Collections.Generic;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Indicates which Dataverse surface defined an operation parameter.
    /// </summary>
    public enum OperationParameterSource
    {
        /// <summary>Parameter originates from a Custom API definition.</summary>
        CustomApi = 0,

        /// <summary>Parameter originates from a classic custom action (SDK message).</summary>
        CustomAction = 1
    }

    /// <summary>
    /// Snapshot of operation (custom API/action) parameter metadata cached on disk.
    /// </summary>
    public sealed class OperationParameterSnapshot
    {
        /// <summary>Gets or sets when the snapshot was generated (UTC).</summary>
        public DateTimeOffset GeneratedOnUtc { get; set; }

        /// <summary>Gets or sets the flattened parameter list.</summary>
        public List<OperationParameterSnapshotItem> Parameters { get; set; } = new List<OperationParameterSnapshotItem>();

        /// <summary>Gets or sets the operation source hints (Custom API vs Action).</summary>
        public List<OperationSourceSnapshotItem> OperationSources { get; set; } = new List<OperationSourceSnapshotItem>();
    }

    /// <summary>
    /// Describes a single operation parameter definition.
    /// </summary>
    public sealed class OperationParameterSnapshotItem
    {
        /// <summary>Gets or sets the source surface (Custom API or Action).</summary>
        public OperationParameterSource Source { get; set; }

        /// <summary>Gets or sets the operation unique name (Custom API or SDK message name).</summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>Gets or sets the primary parameter name (typically the unique/request name).</summary>
        public string PrimaryParameterName { get; set; } = string.Empty;

        /// <summary>Gets or sets an optional alternate parameter name (display/public name).</summary>
        public string? AlternateParameterName { get; set; }

        /// <summary>Gets or sets the type option value.</summary>
        public int? Type { get; set; }

        /// <summary>Gets or sets the logical entity name when applicable.</summary>
        public string? EntityLogicalName { get; set; }

        /// <summary>Gets or sets the logical entity name derived from metadata (alias).</summary>
        public string? LogicalEntityName { get; set; }

        /// <summary>Gets or sets whether the parameter is optional.</summary>
        public bool? IsOptional { get; set; }

        /// <summary>Gets or sets the parameter order/position.</summary>
        public int? Position { get; set; }

        /// <summary>Gets or sets binding metadata for the parameter (e.g., bound target hints).</summary>
        public string? BindingInformation { get; set; }

        /// <summary>Gets or sets the parser type name emitted by Dataverse (classic actions only).</summary>
        public string? Parser { get; set; }

        /// <summary>Gets or sets the formatter type name emitted by Dataverse.</summary>
        public string? Formatter { get; set; }
    }

    /// <summary>
    /// Describes the origin surface for a Dataverse operation (Custom API vs Action).
    /// </summary>
    public sealed class OperationSourceSnapshotItem
    {
        /// <summary>Gets or sets the operation unique name.</summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>Gets or sets the operation source.</summary>
        public OperationParameterSource Source { get; set; }
    }
}
