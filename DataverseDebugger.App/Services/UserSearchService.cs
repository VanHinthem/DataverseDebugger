using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DataverseDebugger.App.Models;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Represents a Dataverse system user for impersonation.
    /// </summary>
    public sealed class DataverseUser
    {
        /// <summary>Gets or sets the user's system user ID.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the user's full name.</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Gets or sets the user's domain name (UPN).</summary>
        public string DomainName { get; set; } = string.Empty;

        /// <summary>Gets or sets the user's primary email address.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Gets or sets the user's business unit ID.</summary>
        public Guid BusinessUnitId { get; set; }

        /// <summary>Gets or sets whether the user is disabled.</summary>
        public bool IsDisabled { get; set; }

        /// <summary>Gets the display text for the user.</summary>
        public string DisplayText => string.IsNullOrWhiteSpace(FullName) ? DomainName : $"{FullName} ({DomainName})";
    }

    /// <summary>
    /// Service for fetching Dataverse users for impersonation.
    /// </summary>
    /// <remarks>
    /// Uses the Web API to search for system users. Impersonation requires
    /// the calling user to have the "Act on Behalf of Another User" privilege.
    /// </remarks>
    public static class UserSearchService
    {
        private static readonly HttpClient Http = new HttpClient();

        /// <summary>
        /// Searches for system users matching the specified query.
        /// </summary>
        /// <param name="profile">The environment profile with org URL.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        /// <param name="searchText">Text to search for in user name or email.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <returns>List of matching users.</returns>
        public static async Task<List<DataverseUser>> SearchUsersAsync(
            EnvironmentProfile profile,
            string accessToken,
            string searchText,
            int maxResults = 20)
        {
            var users = new List<DataverseUser>();

            if (string.IsNullOrWhiteSpace(profile.OrgUrl) || string.IsNullOrWhiteSpace(accessToken))
            {
                return users;
            }

            try
            {
                // Build OData filter
                var filter = "isdisabled eq false";
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    // Escape single quotes for OData string literals
                    var escaped = searchText.Replace("'", "''");
                    filter += $" and (contains(fullname,'{escaped}') or contains(domainname,'{escaped}') or contains(internalemailaddress,'{escaped}'))";
                }

                // URL-encode the filter parameter
                var encodedFilter = Uri.EscapeDataString(filter);
                var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/systemusers" +
                          $"?$select=systemuserid,fullname,domainname,internalemailaddress,businessunitid,isdisabled" +
                          $"&$filter={encodedFilter}" +
                          $"&$orderby=fullname" +
                          $"&$top={maxResults}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.ParseAdd("application/json");

                LogService.Append($"User search URL: {url}");

                var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    LogService.Append($"User search failed: {response.StatusCode} - {errorBody}");
                    return users;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("value", out var valueArray))
                {
                    foreach (var userElement in valueArray.EnumerateArray())
                    {
                        var user = new DataverseUser();

                        if (userElement.TryGetProperty("systemuserid", out var idProp) &&
                            Guid.TryParse(idProp.GetString(), out var id))
                        {
                            user.Id = id;
                        }

                        if (userElement.TryGetProperty("fullname", out var nameProp))
                        {
                            user.FullName = nameProp.GetString() ?? string.Empty;
                        }

                        if (userElement.TryGetProperty("domainname", out var domainProp))
                        {
                            user.DomainName = domainProp.GetString() ?? string.Empty;
                        }

                        if (userElement.TryGetProperty("internalemailaddress", out var emailProp))
                        {
                            user.Email = emailProp.GetString() ?? string.Empty;
                        }

                        if (userElement.TryGetProperty("_businessunitid_value", out var buProp) &&
                            Guid.TryParse(buProp.GetString(), out var buId))
                        {
                            user.BusinessUnitId = buId;
                        }

                        if (userElement.TryGetProperty("isdisabled", out var disabledProp))
                        {
                            user.IsDisabled = disabledProp.GetBoolean();
                        }

                        if (user.Id != Guid.Empty)
                        {
                            users.Add(user);
                        }
                    }
                }

                LogService.Append($"User search returned {users.Count} results for '{searchText}'");
            }
            catch (Exception ex)
            {
                LogService.Append($"User search error: {ex.Message}");
            }

            return users;
        }

        /// <summary>
        /// Gets a specific user by ID.
        /// </summary>
        /// <param name="profile">The environment profile with org URL.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        /// <param name="userId">The system user ID.</param>
        /// <returns>The user if found, null otherwise.</returns>
        public static async Task<DataverseUser?> GetUserByIdAsync(
            EnvironmentProfile profile,
            string accessToken,
            Guid userId)
        {
            if (string.IsNullOrWhiteSpace(profile.OrgUrl) || string.IsNullOrWhiteSpace(accessToken) || userId == Guid.Empty)
            {
                return null;
            }

            try
            {
                var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/systemusers({userId})" +
                          $"?$select=systemuserid,fullname,domainname,internalemailaddress,businessunitid,isdisabled";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.ParseAdd("application/json");

                var response = await Http.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var userElement = doc.RootElement;

                var user = new DataverseUser();

                if (userElement.TryGetProperty("systemuserid", out var idProp) &&
                    Guid.TryParse(idProp.GetString(), out var id))
                {
                    user.Id = id;
                }

                if (userElement.TryGetProperty("fullname", out var nameProp))
                {
                    user.FullName = nameProp.GetString() ?? string.Empty;
                }

                if (userElement.TryGetProperty("domainname", out var domainProp))
                {
                    user.DomainName = domainProp.GetString() ?? string.Empty;
                }

                if (userElement.TryGetProperty("internalemailaddress", out var emailProp))
                {
                    user.Email = emailProp.GetString() ?? string.Empty;
                }

                if (userElement.TryGetProperty("_businessunitid_value", out var buProp) &&
                    Guid.TryParse(buProp.GetString(), out var buId))
                {
                    user.BusinessUnitId = buId;
                }

                if (userElement.TryGetProperty("isdisabled", out var disabledProp))
                {
                    user.IsDisabled = disabledProp.GetBoolean();
                }

                return user.Id != Guid.Empty ? user : null;
            }
            catch (Exception ex)
            {
                LogService.Append($"Get user by ID error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the current user (WhoAmI).
        /// </summary>
        /// <param name="profile">The environment profile with org URL.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        /// <returns>The current user's ID.</returns>
        public static async Task<Guid> GetCurrentUserIdAsync(
            EnvironmentProfile profile,
            string accessToken)
        {
            if (string.IsNullOrWhiteSpace(profile.OrgUrl) || string.IsNullOrWhiteSpace(accessToken))
            {
                return Guid.Empty;
            }

            try
            {
                var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/WhoAmI";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.ParseAdd("application/json");

                var response = await Http.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return Guid.Empty;
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("UserId", out var userIdProp) &&
                    Guid.TryParse(userIdProp.GetString(), out var userId))
                {
                    return userId;
                }
            }
            catch (Exception ex)
            {
                LogService.Append($"WhoAmI error: {ex.Message}");
            }

            return Guid.Empty;
        }
    }
}
