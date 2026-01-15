using System;
using System.Threading.Tasks;
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

            Uri instanceUri;
            if (!Uri.TryCreate(orgUrl, UriKind.Absolute, out instanceUri))
            {
                return null;
            }

            try
            {
                // ServiceClient will call this delegate whenever it needs an access token.
                // No MSAL flows are invoked, so there is no interactive prompt.
                Func<string, Task<string>> tokenProvider = (string instance) =>
                {
                    return Task.FromResult(accessToken ?? string.Empty);
                };

                ServiceClient client = new ServiceClient(instanceUri, tokenProvider, useUniqueInstance: true);

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
