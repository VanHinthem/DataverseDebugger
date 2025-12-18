using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Represents a running Visual Studio instance.
    /// </summary>
    public sealed class VisualStudioInstance
    {
        /// <summary>Gets the display name for the instance.</summary>
        public string DisplayName { get; init; } = string.Empty;
        /// <summary>Gets the COM ProgId.</summary>
        public string ProgId { get; init; } = string.Empty;
        /// <summary>Gets the process ID.</summary>
        public int? ProcessId { get; init; }
        /// <summary>Gets the DTE automation object.</summary>
        public object Dte { get; init; } = new object();
    }

    /// <summary>
    /// Service for attaching Visual Studio debugger to the Runner process.
    /// </summary>
    /// <remarks>
    /// Uses COM automation to enumerate running VS instances and attach/detach
    /// the debugger. Also manages symbol path configuration for shadow-copied assemblies.
    /// </remarks>
    public static class VisualStudioAttachService
    {
        private const string RunnerProcessName = "DataverseDebugger.Runner";

        /// <summary>
        /// Gets all running Visual Studio instances.
        /// </summary>
        public static IReadOnlyList<VisualStudioInstance> GetRunningInstances()
        {
            var list = new List<VisualStudioInstance>();
            foreach (var entry in EnumerateRunningObjects())
            {
                var displayName = entry.DisplayName ?? string.Empty;
                if (!displayName.StartsWith("!VisualStudio.DTE", StringComparison.OrdinalIgnoreCase) &&
                    !displayName.StartsWith("VisualStudio.DTE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var progId = displayName.TrimStart('!');
                var pid = (int?)null;
                var colon = progId.IndexOf(':');
                if (colon > 0)
                {
                    var pidText = progId.Substring(colon + 1);
                    progId = progId.Substring(0, colon);
                    if (int.TryParse(pidText, out var parsed))
                    {
                        pid = parsed;
                    }
                }

                var dte = entry.Object;
                var caption = string.Empty;
                var solution = string.Empty;
                try
                {
                    dynamic dyn = dte;
                    caption = dyn.MainWindow?.Caption ?? string.Empty;
                    solution = dyn.Solution?.FullName ?? string.Empty;
                }
                catch
                {
                    // ignore missing properties
                }

                var display = caption;
                if (!string.IsNullOrWhiteSpace(solution))
                {
                    var file = Path.GetFileName(solution);
                    display = string.IsNullOrWhiteSpace(display) ? file : $"{display} ({file})";
                }
                if (string.IsNullOrWhiteSpace(display))
                {
                    display = displayName;
                }

                list.Add(new VisualStudioInstance
                {
                    DisplayName = display,
                    ProgId = progId,
                    ProcessId = pid,
                    Dte = dte
                });
            }

            return list
                .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool AttachToRunner(VisualStudioInstance instance, out string error)
            => AttachToProcess(instance, RunnerProcessName, out error);

        public static bool DetachRunner(VisualStudioInstance instance, out string error)
            => DetachFromProcess(instance, RunnerProcessName, out error);

        public static bool IsRunnerAttached(VisualStudioInstance instance)
            => IsProcessAttached(instance, RunnerProcessName);

        private static bool AttachToProcess(VisualStudioInstance instance, string processName, out string error)
        {
            error = string.Empty;
            if (instance == null || instance.Dte == null)
            {
                error = "Visual Studio instance not available.";
                return false;
            }

            var runnerPids = Process.GetProcessesByName(processName).Select(p => p.Id).ToHashSet();
            if (runnerPids.Count == 0)
            {
                error = $"{processName} is not running.";
                return false;
            }

            try
            {
                dynamic dte = instance.Dte;
                EnsureShadowSymbolPath(dte, runnerPids);
                var debugged = new HashSet<int>();
                foreach (dynamic proc in dte.Debugger.DebuggedProcesses)
                {
                    try { debugged.Add(Convert.ToInt32(proc.ProcessID)); } catch { }
                }

                var attached = 0;
                foreach (dynamic proc in dte.Debugger.LocalProcesses)
                {
                    var pid = 0;
                    try { pid = Convert.ToInt32(proc.ProcessID); } catch { continue; }
                    if (!runnerPids.Contains(pid)) continue;
                    if (debugged.Contains(pid)) continue;
                    try
                    {
                        proc.Attach();
                        attached++;
                    }
                    catch { }
                }

                if (attached == 0 && debugged.Overlaps(runnerPids))
                {
                    return true;
                }

                if (attached == 0)
                {
                    error = "Runner process not found in Visual Studio local processes.";
                }
                return attached > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsProcessAttached(VisualStudioInstance instance, string processName)
        {
            if (instance == null || instance.Dte == null)
            {
                return false;
            }

            try
            {
                var runnerPids = Process.GetProcessesByName(processName).Select(p => p.Id).ToHashSet();
                if (runnerPids.Count == 0)
                {
                    return false;
                }

                dynamic dte = instance.Dte;
                foreach (dynamic proc in dte.Debugger.DebuggedProcesses)
                {
                    var pid = 0;
                    try { pid = Convert.ToInt32(proc.ProcessID); } catch { continue; }
                    if (runnerPids.Contains(pid))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool DetachFromProcess(VisualStudioInstance instance, string processName, out string error)
        {
            error = string.Empty;
            if (instance == null || instance.Dte == null)
            {
                error = "Visual Studio instance not available.";
                return false;
            }

            try
            {
                var runnerPids = Process.GetProcessesByName(processName).Select(p => p.Id).ToHashSet();
                dynamic dte = instance.Dte;
                var detached = 0;
                foreach (dynamic proc in dte.Debugger.DebuggedProcesses)
                {
                    var pid = 0;
                    try { pid = Convert.ToInt32(proc.ProcessID); } catch { continue; }
                    if (!runnerPids.Contains(pid)) continue;
                    try
                    {
                        proc.Detach(false);
                        detached++;
                    }
                    catch
                    {
                        try
                        {
                            proc.Detach();
                            detached++;
                        }
                        catch { }
                    }
                }
                return detached > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void EnsureShadowSymbolPath(dynamic dte, HashSet<int> runnerPids)
        {
            try
            {
                var shadowRoots = EnvironmentPathService.EnumerateRunnerShadowRoots().ToList();
                if (shadowRoots.Count == 0)
                {
                    return;
                }

                var candidates = runnerPids
                    .SelectMany(pid => shadowRoots.Select(root => Path.Combine(root, pid.ToString())))
                    .Where(Directory.Exists)
                    .ToList();

                if (candidates.Count == 0)
                {
                    // Fall back to any existing shadow folders.
                    candidates = shadowRoots
                        .SelectMany(root => Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>())
                        .ToList();
                }

                if (candidates.Count == 0)
                {
                    return;
                }

                var expanded = new List<string>();
                foreach (var candidate in candidates)
                {
                    if (!Directory.Exists(candidate)) continue;
                    expanded.Add(candidate);
                    try
                    {
                        expanded.AddRange(Directory.GetDirectories(candidate));
                    }
                    catch
                    {
                        // ignore directory probe failures
                    }
                }
                candidates = expanded
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var existing = string.Empty;
                try { existing = dte.Debugger.SymbolPath ?? string.Empty; } catch { }

                var parts = existing.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                foreach (var path in candidates)
                {
                    if (!parts.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        parts.Add(path);
                    }
                }

                var updated = string.Join(";", parts);
                if (!string.Equals(updated, existing, StringComparison.OrdinalIgnoreCase))
                {
                    dte.Debugger.SymbolPath = updated;
                }
            }
            catch
            {
                // ignore symbol path failures
            }
        }

        private static IEnumerable<(string DisplayName, object Object)> EnumerateRunningObjects()
        {
            var results = new List<(string DisplayName, object Object)>();
            if (GetRunningObjectTable(0, out var rot) != 0 || rot == null)
            {
                return results;
            }
            if (CreateBindCtx(0, out var ctx) != 0 || ctx == null)
            {
                return results;
            }

            rot.EnumRunning(out var enumMoniker);
            if (enumMoniker == null)
            {
                return results;
            }

            var monikers = new IMoniker[1];
            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                var moniker = monikers[0];
                if (moniker == null) continue;
                try
                {
                    moniker.GetDisplayName(ctx, null, out var name);
                    rot.GetObject(moniker, out var obj);
                    if (!string.IsNullOrWhiteSpace(name) && obj != null)
                    {
                        results.Add((name, obj));
                    }
                }
                catch
                {
                    // ignore ROT errors
                }
            }
            return results;
        }

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);
    }
}
