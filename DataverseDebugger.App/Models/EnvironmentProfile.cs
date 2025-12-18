using System.Collections.Generic;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Represents a Dataverse environment configuration profile.
    /// </summary>
    /// <remarks>
    /// Contains all settings needed to connect to and debug plugins in a Dataverse environment,
    /// including authentication, plugin assemblies, capture settings, and cache locations.
    /// </remarks>
    public class EnvironmentProfile
    {
        /// <summary>Gets or sets the profile display name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the three-digit environment number used for cache isolation.</summary>
        public string EnvironmentNumber { get; set; } = string.Empty;

        /// <summary>Gets or sets the Dataverse organization URL.</summary>
        public string OrgUrl { get; set; } = string.Empty;

        /// <summary>Gets or sets optional notes about this environment.</summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>Gets or sets the list of local plugin assembly paths to debug.</summary>
        public List<string> PluginAssemblies { get; set; } = new List<string>();

        // Runner defaults
        public bool TraceVerbose { get; set; }

        // Capture defaults
        public bool CaptureApiOnly { get; set; } = true;
        public bool CaptureAutoProxy { get; set; } = true;
        public string? CaptureNavigateUrl { get; set; }

        // Auth/cache paths (per-environment)
        public string? TokenCachePath { get; set; }
        public string? WebViewCachePath { get; set; }

        // Auth
        public string? ClientId { get; set; } = "51f81489-12ee-4a9e-aaae-a2591f45987d"; // default public client
        public string? TenantId { get; set; } = "organizations";
        public string? SignedInUser { get; set; }
        public DateTimeOffset? AccessTokenExpiresOn { get; set; }
        public string? LastAccessToken { get; set; }

        // UI state
        public bool IsActive { get; set; }

        // Metadata cache
        public DateTimeOffset? MetadataFetchedOn { get; set; }

        // Plugin catalog cache
        public DateTimeOffset? PluginCatalogFetchedOn { get; set; }
    }
}
