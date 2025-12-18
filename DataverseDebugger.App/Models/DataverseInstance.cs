using System;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Represents a Dataverse environment returned from the global discovery service.
    /// </summary>
    public class DataverseInstance
    {
        public string FriendlyName { get; set; } = string.Empty;
        public string UniqueName { get; set; } = string.Empty;
        public string WebUrl { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public string Geo { get; set; } = string.Empty;
        public string EnvironmentId { get; set; } = string.Empty;
        public Guid? OrganizationId { get; set; }
    }
}
