using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Services.Hybrid
{
    internal static class ServiceClientOrganizationServiceFactory
    {
        public static IOrganizationService? TryCreate(string? orgUrl, string? accessToken, out ServiceClient? serviceClient)
        {
            serviceClient = null;
            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            try
            {
                var connectionString = $"AuthType=OAuth;Url={orgUrl};AccessToken={accessToken};";
                serviceClient = new ServiceClient(connectionString);
                return serviceClient;
            }
            catch
            {
                serviceClient = null;
                return null;
            }
        }
    }
}
