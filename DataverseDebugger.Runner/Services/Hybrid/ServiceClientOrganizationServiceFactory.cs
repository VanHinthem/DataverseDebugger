using System;
using DataverseDebugger.Runner.Abstractions;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Services.Hybrid
{
    internal sealed class ServiceClientOrganizationServiceFactory : ILiveOrganizationServiceFactory
    {
        public IOrganizationService? CreateLiveService(string? orgUrl, string? accessToken, out IDisposable? disposable)
        {
            disposable = null;
            if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            try
            {
                var connectionString = $"AuthType=OAuth;Url={orgUrl};AccessToken={accessToken};";
                var client = new ServiceClient(connectionString);
                disposable = client;
                return client;
            }
            catch
            {
                disposable = null;
                return null;
            }
        }
    }
}
