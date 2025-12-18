using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DataverseDebugger.App
{
    /// <summary>
    /// Manages the lifecycle of the DataverseDebugger.Runner process.
    /// </summary>
    /// <remarks>
    /// This class is responsible for starting, stopping, and monitoring the Runner process
    /// which hosts plugin execution in a .NET Framework 4.8 environment.
    /// </remarks>
    internal sealed class RunnerProcessManager : IDisposable
    {
        private Process? _process;
        private int? _expectedExitPid;

        /// <summary>
        /// Raised when the runner process exits unexpectedly.
        /// </summary>
        public event EventHandler<int>? RunnerExited;

        /// <summary>
        /// Starts the runner process if not already running.
        /// </summary>
        /// <returns>True if the process started successfully; false otherwise.</returns>
        public async Task<bool> StartAsync()
        {
            if (_process != null && !_process.HasExited)
            {
                return true;
            }

            var runnerExe = ResolveRunnerPath();
            if (!File.Exists(runnerExe))
            {
                throw new FileNotFoundException("Runner executable not found", runnerExe);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = runnerExe,
                WorkingDirectory = Path.GetDirectoryName(runnerExe) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.Environment["DATAVERSE_DEBUGGER_HOST_PID"] = Environment.ProcessId.ToString();

            _process = Process.Start(startInfo);
            if (_process == null || _process.HasExited)
            {
                _process = null;
                return false;
            }
            _process.EnableRaisingEvents = true;
            _process.Exited += OnProcessExited;

            // Give the pipe server a brief moment to bind.
            await Task.Delay(200).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Ensures the runner process is running, starting it if necessary.
        /// </summary>
        /// <returns>True if the process is running; false otherwise.</returns>
        public async Task<bool> EnsureRunningAsync()
        {
            if (_process != null && !_process.HasExited)
            {
                return true;
            }
            return await StartAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Stops the current runner process and starts a new one.
        /// </summary>
        /// <returns>True if the restart succeeded; false otherwise.</returns>
        public async Task<bool> RestartAsync()
        {
            Stop();
            return await StartAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Stops the runner process if running.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_process == null)
                {
                    return;
                }

                _expectedExitPid = _process.Id;
                _process.Exited -= OnProcessExited;
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(2000);
                }
            }
            catch
            {
                // best effort
            }
            finally
            {
                _process?.Dispose();
                _process = null;
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            try
            {
                if (sender is not Process proc)
                {
                    return;
                }

                var pid = 0;
                try { pid = proc.Id; } catch { }
                if (_expectedExitPid.HasValue && _expectedExitPid.Value == pid)
                {
                    _expectedExitPid = null;
                    return;
                }

                RunnerExited?.Invoke(this, pid);
            }
            catch
            {
                // ignore
            }
        }

        private static string ResolveRunnerPath()
        {
            var bundled = ResolveBundledRunnerPath();
            if (bundled != null)
            {
                return bundled;
            }

            // Local dev fallback: assume runner lives in sibling project output.
            var baseDir = AppContext.BaseDirectory;
            var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            var debugPath = Path.Combine(solutionRoot, "DataverseDebugger.Runner", "bin", "Debug", "net48", "DataverseDebugger.Runner.exe");
            var releasePath = Path.Combine(solutionRoot, "DataverseDebugger.Runner", "bin", "Release", "net48", "DataverseDebugger.Runner.exe");

            if (File.Exists(debugPath))
            {
                return debugPath;
            }

            if (File.Exists(releasePath))
            {
                return releasePath;
            }

            return debugPath; // default fallback
        }

        private static string? ResolveBundledRunnerPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.Combine(baseDir, "runner", "DataverseDebugger.Runner.exe");
            return File.Exists(candidate) ? candidate : null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
