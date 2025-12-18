namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Configuration for a Dataverse environment used by the runner.
    /// </summary>
    public sealed class EnvConfig
    {
        /// <summary>Display name of the environment.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Base URL of the Dataverse organization (e.g., https://org.crm.dynamics.com).</summary>
        public string OrgUrl { get; set; } = string.Empty;

        /// <summary>Optional path to a cached metadata XML file.</summary>
        public string? MetadataPath { get; set; }

        /// <summary>Root folder for shadow-copied runner assemblies.</summary>
        public string? RunnerShadowRoot { get; set; }

        /// <summary>Optional root directory for cached entity metadata (used by the runner).</summary>
        public string? EntityMetadataCacheRoot { get; set; }

        /// <summary>Whether plugin emulation is enabled for this environment.</summary>
        public bool EmulationEnabled { get; set; } = true;

        /// <summary>Whether web resource overrides are enabled.</summary>
        public bool WebResourceOverridesEnabled { get; set; }
    }
}
