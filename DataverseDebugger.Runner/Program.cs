using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.Runner
{
    /// <summary>
    /// Entry point for the Dataverse Debugger Runner process.
    /// The runner executes in a separate .NET Framework 4.8 process to host plugin assemblies
    /// and handle IPC commands from the main application.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.Title = "Dataverse Debugger Runner";
            Console.WriteLine($"Dataverse Debugger Runner starting (protocol v{ProtocolVersion.Current}).");
            RunnerLogger.Log(RunnerLogCategory.RunnerLifecycle, RunnerLogLevel.Info,
                $"Runner starting (protocol v{ProtocolVersion.Current}).");

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                var message = ex != null
                    ? $"Unhandled exception: {ex.GetType().Name}: {ex.Message}"
                    : $"Unhandled exception: {e.ExceptionObject}";
                RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, message);
                var stack = ex?.StackTrace;
                if (stack != null && stack.Length > 0)
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, stack);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                var baseEx = e.Exception?.GetBaseException();
                var message = baseEx != null
                    ? $"Unobserved task exception: {baseEx.GetType().Name}: {baseEx.Message}"
                    : "Unobserved task exception.";
                RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, message);
                if (e.Exception != null)
                {
                    RunnerLogger.Log(RunnerLogCategory.Errors, RunnerLogLevel.Info, e.Exception.ToString());
                }
                e.SetObserved();
            };

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var hostPid = TryGetHostPid();
            if (hostPid.HasValue)
            {
                _ = MonitorHostProcessAsync(hostPid.Value, cts);
            }

            var server = new RunnerPipeServer();
            await server.RunAsync(cts.Token).ConfigureAwait(false);

            Console.WriteLine("Runner shutting down.");
            RunnerLogger.Log(RunnerLogCategory.RunnerLifecycle, RunnerLogLevel.Info, "Runner shutting down.");
            return 0;
        }

        /// <summary>
        /// Attempts to read the host process ID from the environment variable.
        /// </summary>
        private static int? TryGetHostPid()
        {
            try
            {
                var raw = Environment.GetEnvironmentVariable("DATAVERSE_DEBUGGER_HOST_PID");
                if (int.TryParse(raw, out var pid) && pid > 0)
                {
                    return pid;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>
        /// Monitors the host process and triggers shutdown when it exits.
        /// This ensures the runner doesn't become orphaned if the host crashes.
        /// </summary>
        private static async Task MonitorHostProcessAsync(int hostPid, CancellationTokenSource cts)
        {
            while (!cts.IsCancellationRequested)
            {
                if (!IsProcessAlive(hostPid))
                {
                    RunnerLogger.Log(RunnerLogCategory.RunnerLifecycle, RunnerLogLevel.Info,
                        $"Host process {hostPid} not running. Exiting runner.");
                    cts.Cancel();
                    return;
                }

                try
                {
                    await Task.Delay(2000, cts.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Checks if a process with the given PID is still running.
        /// </summary>
        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                return !proc.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}
