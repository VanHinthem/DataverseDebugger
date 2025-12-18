using System.Collections.Generic;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Reference to a plugin assembly to be loaded by the runner.
    /// </summary>
    public sealed class PluginAssemblyRef
    {
        /// <summary>Full path to the assembly DLL file.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Optional path to the PDB file for debugging symbols.</summary>
        public string? PdbPath { get; set; }

        /// <summary>Whether this assembly should be loaded and executed.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Additional folders to probe for assembly dependencies.</summary>
        public List<string> DependencyFolders { get; set; } = new List<string>();
    }

    /// <summary>
    /// Manifest describing the plugin workspace configuration.
    /// </summary>
    public sealed class PluginWorkspaceManifest
    {
        /// <summary>List of plugin assemblies to load.</summary>
        public List<PluginAssemblyRef> Assemblies { get; set; } = new List<PluginAssemblyRef>();

        /// <summary>Whether to disable async steps registered on the server.</summary>
        public bool DisableAsyncStepsOnServer { get; set; }

        /// <summary>Enable verbose trace output from plugins.</summary>
        public bool TraceVerbose { get; set; }
    }
}
