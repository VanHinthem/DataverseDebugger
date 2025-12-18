using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataverseDebugger.App.Models;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Provides path management for per-environment cache directories.
    /// </summary>
    /// <remarks>
    /// Each environment has an isolated cache directory under envcache/{envNumber}/
    /// for metadata, plugin catalogs, and runner shadow copies.
    /// </remarks>
    public static class EnvironmentPathService
    {
        /// <summary>
        /// Gets the root cache directory for an environment profile, migrating legacy paths if necessary.
        /// </summary>
        public static string GetEnvironmentCacheRoot(EnvironmentProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var folderName = GetEnvironmentFolderName(profile);
            var baseRoot = GetBaseCacheRoot();
            var target = Path.Combine(baseRoot, folderName);
            TryMigrateLegacyCache(profile, baseRoot, folderName, target);
            return target;
        }

        /// <summary>
        /// Gets the root cache directory for a legacy name-based environment.
        /// </summary>
        public static string GetEnvironmentCacheRoot(string envName)
        {
            var safeName = MakeSafeName(envName);
            return Path.Combine(GetBaseCacheRoot(), safeName);
        }

        public static string GetRunnerShadowRoot(EnvironmentProfile profile)
        {
            return Path.Combine(GetEnvironmentCacheRoot(profile), "runner-shadow");
        }

        public static string GetRunnerShadowRoot(string envName)
        {
            return Path.Combine(GetEnvironmentCacheRoot(envName), "runner-shadow");
        }

        public static IEnumerable<string> EnumerateRunnerShadowRoots()
        {
            var baseRoot = GetBaseCacheRoot();
            if (!Directory.Exists(baseRoot))
            {
                return Enumerable.Empty<string>();
            }

            var roots = new List<string>();
            foreach (var envDir in Directory.GetDirectories(baseRoot))
            {
                var shadowDir = Path.Combine(envDir, "runner-shadow");
                if (Directory.Exists(shadowDir))
                {
                    roots.Add(shadowDir);
                }
            }

            return roots;
        }

        public static void CleanupAllRunnerShadowRoots()
        {
            foreach (var root in EnumerateRunnerShadowRoots())
            {
                TryDeleteDirectory(root);
            }
        }

        public static void DeleteEnvironmentCache(EnvironmentProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            var cacheRoot = GetEnvironmentCacheRoot(profile);
            TryDeleteDirectory(cacheRoot);
        }

        public static void DeleteEnvironmentCache(string envName)
        {
            var cacheRoot = GetEnvironmentCacheRoot(envName);
            TryDeleteDirectory(cacheRoot);
        }

        /// <summary>
        /// Ensures a subfolder exists within an environment cache root and returns the full path.
        /// </summary>
        /// <param name="envName">Environment display name.</param>
        /// <param name="subfolder">Subfolder name (e.g., token-cache).</param>
        /// <returns>Full path to the ensured subfolder.</returns>
        public static string EnsureEnvironmentSubfolder(EnvironmentProfile profile, string subfolder)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var root = EnsureEnvironmentCacheRoot(profile);
            var path = Path.Combine(root, subfolder);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string EnsureEnvironmentSubfolder(string envName, string subfolder)
        {
            var root = GetEnvironmentCacheRoot(envName);
            var path = Path.Combine(root, subfolder);
            Directory.CreateDirectory(path);
            return path;
        }

        private static string EnsureEnvironmentCacheRoot(EnvironmentProfile profile)
        {
            var root = GetEnvironmentCacheRoot(profile);
            Directory.CreateDirectory(root);
            return root;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // ignore cleanup failures
            }
        }

        private static string GetBaseCacheRoot()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "envcache");
        }

        private static string GetEnvironmentFolderName(EnvironmentProfile profile)
        {
            var normalized = NormalizeEnvironmentNumber(profile.EnvironmentNumber);
            return string.IsNullOrWhiteSpace(normalized)
                ? MakeSafeName(profile.Name)
                : normalized;
        }

        private static string NormalizeEnvironmentNumber(string? number)
        {
            return int.TryParse(number, out var parsed) && parsed > 0
                ? parsed.ToString("D3")
                : string.Empty;
        }

        private static void TryMigrateLegacyCache(EnvironmentProfile profile, string baseRoot, string targetFolderName, string targetPath)
        {
            var legacyFolder = MakeSafeName(profile.Name);
            if (string.IsNullOrWhiteSpace(legacyFolder) ||
                string.Equals(legacyFolder, targetFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var legacyPath = Path.Combine(baseRoot, legacyFolder);
            if (!Directory.Exists(legacyPath) || Directory.Exists(targetPath))
            {
                return;
            }

            try
            {
                Directory.Move(legacyPath, targetPath);
            }
            catch
            {
                // ignore migration failures
            }
        }

        private static string MakeSafeName(string name)
        {
            var safeName = string.Join("_", (name ?? string.Empty).Split(Path.GetInvalidFileNameChars())).Trim();
            return string.IsNullOrWhiteSpace(safeName) ? "env" : safeName;
        }
    }
}
