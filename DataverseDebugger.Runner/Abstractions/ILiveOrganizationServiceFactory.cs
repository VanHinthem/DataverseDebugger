using System;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Abstractions
{
    internal interface ILiveOrganizationServiceFactory
    {
        IOrganizationService? CreateLiveService(string? orgUrl, string? accessToken, out IDisposable? disposable);
    }
}
