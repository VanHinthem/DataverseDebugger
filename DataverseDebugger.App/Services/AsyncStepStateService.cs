using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DataverseDebugger.App.Models;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Result of an async step state modification operation.
    /// </summary>
    public sealed class AsyncStepStateResult
    {
        /// <summary>Gets the step IDs that were successfully modified.</summary>
        public List<Guid> Succeeded { get; } = new List<Guid>();
        /// <summary>Gets the step IDs that failed to modify.</summary>
        public List<Guid> Failed { get; } = new List<Guid>();
    }

    /// <summary>
    /// Service for disabling/enabling async plugin steps in Dataverse.
    /// </summary>
    /// <remarks>
    /// Async steps can be temporarily disabled to allow local debugging.
    /// Disabled step IDs are tracked in a lock file for automatic restoration.
    /// </remarks>
    public static class AsyncStepStateService
    {
        private static readonly HttpClient Http = new HttpClient();

        /// <summary>
        /// Gets the lock file path for tracking disabled steps.
        /// </summary>
        public static string GetLockFilePath(EnvironmentProfile profile)
        {
            var root = EnvironmentPathService.GetEnvironmentCacheRoot(profile);
            return Path.Combine(root, "disabled_async_steps.lock");
        }

        public static List<Guid>? TryReadLockFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var stepIds = new List<Guid>();
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (Guid.TryParse(line.Trim(), out var id))
                    {
                        stepIds.Add(id);
                    }
                }
                return stepIds;
            }
            catch
            {
                return null;
            }
        }

        public static void WriteLockFile(string path, IEnumerable<Guid> stepIds)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var list = stepIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList() ?? new List<Guid>();
            if (list.Count == 0)
            {
                return;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var lines = new List<string>
            {
                "# DataverseDebugger disabled async steps lock file",
                $"# Created: {DateTime.UtcNow:O}",
                $"# Count: {list.Count}"
            };
            lines.AddRange(list.Select(id => id.ToString()));
            File.WriteAllLines(path, lines);
        }

        public static void DeleteLockFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignore cleanup failures
            }
        }

        public static List<Guid> ResolveAsyncStepIds(PluginCatalog catalog, IEnumerable<string>? assemblyPaths)
        {
            if (catalog == null || assemblyPaths == null)
            {
                return new List<Guid>();
            }

            var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in assemblyPaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var file = Path.GetFileName(path);
                if (!string.IsNullOrWhiteSpace(file))
                {
                    selectedNames.Add(file);
                }

                var fileNoExt = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrWhiteSpace(fileNoExt))
                {
                    selectedNames.Add(fileNoExt);
                }
            }

            if (selectedNames.Count == 0)
            {
                return new List<Guid>();
            }

            var assemblyIds = catalog.Assemblies
                .Where(a => MatchesName(a.Name, selectedNames))
                .Select(a => a.Id)
                .ToHashSet();

            var typeIds = catalog.Types
                .Where(t => (t.AssemblyId != Guid.Empty && assemblyIds.Contains(t.AssemblyId)) ||
                            MatchesName(t.AssemblyName, selectedNames))
                .Select(t => t.Id)
                .ToHashSet();

            return catalog.Steps
                .Where(s => s.Mode == 1 &&
                            ((s.AssemblyId != Guid.Empty && assemblyIds.Contains(s.AssemblyId)) ||
                             typeIds.Contains(s.PluginTypeId)))
                .Select(s => s.Id)
                .Distinct()
                .ToList();
        }

        public static async Task<AsyncStepStateResult> SetAsyncStepsEnabledAsync(EnvironmentProfile profile, string accessToken, IEnumerable<Guid> stepIds, bool enabled)
        {
            var result = new AsyncStepStateResult();
            if (string.IsNullOrWhiteSpace(profile.OrgUrl) || string.IsNullOrWhiteSpace(accessToken))
            {
                return result;
            }

            foreach (var stepId in stepIds ?? Enumerable.Empty<Guid>())
            {
                if (stepId == Guid.Empty)
                {
                    continue;
                }

                var success = await SetStepEnabledAsync(profile, accessToken, stepId, enabled).ConfigureAwait(false);
                if (success)
                {
                    result.Succeeded.Add(stepId);
                }
                else
                {
                    result.Failed.Add(stepId);
                }
            }

            return result;
        }

        private static async Task<bool> SetStepEnabledAsync(EnvironmentProfile profile, string accessToken, Guid stepId, bool enabled)
        {
            var url = $"{profile.OrgUrl.TrimEnd('/')}/api/data/v9.0/sdkmessageprocessingsteps({stepId})";
            var payload = new Dictionary<string, object?>
            {
                ["statecode"] = enabled ? 0 : 1,
                ["statuscode"] = enabled ? 1 : 2
            };

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await Http.SendAsync(request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                var detail = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LogService.Append($"Async step update failed ({stepId}): {(int)response.StatusCode} {response.ReasonPhrase} {detail}");
                return false;
            }
            catch (Exception ex)
            {
                LogService.Append($"Async step update failed ({stepId}): {ex.Message}");
                return false;
            }
        }

        private static bool MatchesName(string? name, HashSet<string> selectedNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (selectedNames.Contains(name))
            {
                return true;
            }

            var noExt = Path.GetFileNameWithoutExtension(name);
            return !string.IsNullOrWhiteSpace(noExt) && selectedNames.Contains(noExt);
        }
    }
}
