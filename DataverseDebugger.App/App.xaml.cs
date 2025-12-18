using System.Configuration;
using System.Data;
using System.Windows;

namespace DataverseDebugger.App;

/// <summary>
/// Main application entry point for the Dataverse Debugger WPF application.
/// </summary>
/// <remarks>
/// This application provides a debugging and testing environment for Dataverse plugins,
/// allowing developers to intercept Web API requests, execute plugin logic locally,
/// and debug plugins without deploying to the server.
/// </remarks>
public partial class App : Application
{
    private bool _isShuttingDown;

    /// <summary>
    /// Called when the application starts; initializes logging and exception handlers.
    /// </summary>
    /// <param name="e">Startup event arguments.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        HookGlobalExceptionHandlers();
        DataverseDebugger.App.Services.LogService.Initialize(this.Dispatcher);
        DataverseDebugger.App.Services.LogService.ClearLogFile();
        base.OnStartup(e);
    }

    /// <summary>
    /// Registers global exception handlers for UI, task, and domain exceptions.
    /// </summary>
    private void HookGlobalExceptionHandlers()
    {
        // UI thread exceptions
        this.DispatcherUnhandledException += (sender, args) =>
        {
            DataverseDebugger.App.Services.LogService.AppendException(args.Exception, "DispatcherUnhandledException");
            args.Handled = true;
            ShutdownApplication();
        };

        // Task exceptions
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            DataverseDebugger.App.Services.LogService.AppendException(args.Exception, "UnobservedTaskException");
            args.SetObserved();
            ShutdownApplication();
        };

        // Non-UI exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                DataverseDebugger.App.Services.LogService.AppendException(ex, "UnhandledException");
            }
            ShutdownApplication();
        };
    }

    private void ShutdownApplication()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;

        void ShutdownAction()
        {
            try
            {
                if (MainWindow != null)
                {
                    MainWindow.Close();
                }
            }
            catch
            {
                // ignored; fallback to app shutdown
            }
            finally
            {
                Shutdown();
            }
        }

        if (Dispatcher.CheckAccess())
        {
            ShutdownAction();
        }
        else
        {
            Dispatcher.Invoke(ShutdownAction);
        }
    }
}

