using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Services;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace DataverseDebugger.App.Auth
{
    /// <summary>
    /// Contains authentication result information including access token and user details.
    /// </summary>
    public class AuthResultInfo
    {
        /// <summary>Gets or sets the OAuth access token.</summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>Gets or sets the authenticated user's username.</summary>
        public string? User { get; set; }

        /// <summary>Gets or sets when the access token expires.</summary>
        public DateTimeOffset ExpiresOn { get; set; }
        
        /// <summary>Gets or sets the tenant identifier returned by Azure AD.</summary>
        public string? TenantId { get; set; }
    }

    /// <summary>
    /// Provides authentication services for Dataverse environments using MSAL.
    /// </summary>
    /// <remarks>
    /// Handles interactive and silent token acquisition with per-environment token caching.
    /// Uses the default Dataverse public client ID if not specified in the environment profile.
    /// </remarks>
    public static class AuthService
    {
        private const string DefaultClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
        private const string DefaultTenant = "organizations";
        private const string DiscoveryProfileName = "__globaldisco";

        /// <summary>
        /// Acquires an access token for the provided environment, attempting silent auth before interactive.
        /// </summary>
        public static Task<AuthResultInfo?> AcquireTokenInteractiveAsync(EnvironmentProfile profile)
            => AcquireTokenAsync(profile, silentOnly: false);

        private static async Task<AuthenticationResult> AcquireInteractiveAsync(IPublicClientApplication app, string[] scopes)
        {
            try
            {
                return await app.AcquireTokenInteractive(scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync()
                    .ConfigureAwait(false);
            }
            catch (MsalClientException mce) when (mce.ErrorCode == "authentication_canceled")
            {
                throw new OperationCanceledException("User canceled sign-in.");
            }
        }

        private static string EnsureTokenCache(EnvironmentProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.TokenCachePath))
            {
                profile.TokenCachePath = EnvironmentPathService.EnsureEnvironmentSubfolder(profile, "token-cache");
            }

            Directory.CreateDirectory(profile.TokenCachePath);
            return profile.TokenCachePath;
        }

        /// <summary>
        /// Acquires a token for the global Dataverse discovery endpoint to list environments.
        /// </summary>
        public static Task<AuthResultInfo?> AcquireDiscoveryTokenAsync(bool silentOnly = false)
        {
            var discoveryProfile = new EnvironmentProfile
            {
                Name = DiscoveryProfileName,
                OrgUrl = "https://globaldisco.crm.dynamics.com",
                CaptureApiOnly = true,
                CaptureAutoProxy = true,
                TenantId = DefaultTenant,
                ClientId = DefaultClientId,
                TokenCachePath = EnvironmentPathService.EnsureEnvironmentSubfolder(DiscoveryProfileName, "token-cache")
            };

            return AcquireTokenAsync(discoveryProfile, silentOnly);
        }

        private static async Task<AuthResultInfo?> AcquireTokenAsync(EnvironmentProfile profile, bool silentOnly)
        {
            if (string.IsNullOrWhiteSpace(profile.OrgUrl))
            {
                throw new InvalidOperationException("Org URL is required for authentication.");
            }

            var authority = string.IsNullOrWhiteSpace(profile.TenantId)
                ? $"https://login.microsoftonline.com/{DefaultTenant}"
                : $"https://login.microsoftonline.com/{profile.TenantId}";

            var clientId = string.IsNullOrWhiteSpace(profile.ClientId) ? DefaultClientId : profile.ClientId;
            var scopes = new[] { $"{profile.OrgUrl.TrimEnd('/')}/.default" };

            var app = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(authority)
                .WithRedirectUri("http://localhost")
                .Build();

            var cacheDir = EnsureTokenCache(profile);
            var storageProps = new StorageCreationPropertiesBuilder("msalcache.dat", cacheDir).Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProps).ConfigureAwait(false);
            cacheHelper.RegisterCache(app.UserTokenCache);

            AuthenticationResult result;
            try
            {
                var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync().ConfigureAwait(false);
            }
            catch
            {
                if (silentOnly)
                {
                    return null;
                }

                result = await AcquireInteractiveAsync(app, scopes).ConfigureAwait(false);
            }

            return new AuthResultInfo
            {
                AccessToken = result.AccessToken,
                User = result.Account?.Username,
                ExpiresOn = result.ExpiresOn,
                TenantId = result.TenantId
            };
        }

        /// <summary>
        /// Copies the MSAL token cache from one environment folder to another so silent auth can reuse the same account.
        /// </summary>
        public static void CloneTokenCache(string? sourceDirectory, string? destinationDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(destinationDirectory))
            {
                return;
            }

            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(destinationDirectory);
                foreach (var file in Directory.GetFiles(sourceDirectory))
                {
                    var targetPath = Path.Combine(destinationDirectory, Path.GetFileName(file));
                    File.Copy(file, targetPath, overwrite: true);
                }
            }
            catch
            {
                // ignore cache copy issues; user can always sign in again
            }
        }

        /// <summary>
        /// Signs out all cached accounts for the specified environment.
        /// </summary>
        public static async Task SignOutAsync(EnvironmentProfile profile)
        {
            var authority = string.IsNullOrWhiteSpace(profile.TenantId)
                ? $"https://login.microsoftonline.com/{DefaultTenant}"
                : $"https://login.microsoftonline.com/{profile.TenantId}";

            var clientId = string.IsNullOrWhiteSpace(profile.ClientId) ? DefaultClientId : profile.ClientId;

            var app = PublicClientApplicationBuilder
                .Create(clientId)
                .WithAuthority(authority)
                .WithRedirectUri("http://localhost")
                .Build();

            var cacheDir = EnsureTokenCache(profile);
            var storageProps = new StorageCreationPropertiesBuilder("msalcache.dat", cacheDir).Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProps).ConfigureAwait(false);
            cacheHelper.RegisterCache(app.UserTokenCache);

            var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
            foreach (var account in accounts)
            {
                await app.RemoveAsync(account).ConfigureAwait(false);
            }
        }
    }
}
