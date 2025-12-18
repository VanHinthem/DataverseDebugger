using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Data;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Centralized logging service for the application.
    /// </summary>
    /// <remarks>
    /// Maintains an observable log entry collection for UI binding and writes to a file.
    /// Thread-safe with dispatcher marshaling for UI updates.
    /// </remarks>
    public static class LogService
    {
        /// <summary>Gets the observable collection of log entries.</summary>
        public static ObservableCollection<string> Entries { get; } = new ObservableCollection<string>();
        private const int MaxEntries = 300;
        private static readonly object _sync = new object();
        private static Dispatcher? _uiDispatcher;
        private static bool _syncEnabled;
        private static string? _logFilePath;

        /// <summary>
        /// Initializes the log service with the UI dispatcher.
        /// </summary>
        /// <param name="dispatcher">The UI dispatcher for thread marshaling.</param>
        public static void Initialize(Dispatcher dispatcher)
        {
            try
            {
                _uiDispatcher = dispatcher;
                BindingOperations.EnableCollectionSynchronization(Entries, _sync);
                _syncEnabled = true;
                var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                System.IO.Directory.CreateDirectory(logDir);
                _logFilePath = System.IO.Path.Combine(logDir, "app.log");
            }
            catch
            {
                // best effort; will fall back to locks
            }
        }

        public static void ClearLogFile()
        {
            var path = _logFilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                System.IO.File.WriteAllText(path, string.Empty);
            }
            catch
            {
                // ignore issues so app startup continues
            }
        }

        public static void Append(string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss} {message}";
            var dispatcher = _uiDispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                try
                {
                    dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => AddLine(line)));
                }
                catch
                {
                    AddLine(line);
                }
            }
            else
            {
                AddLine(line);
            }
        }

        private static void AddLine(string line)
        {
            if (!_syncEnabled)
            {
                try
                {
                    BindingOperations.EnableCollectionSynchronization(Entries, _sync);
                    _syncEnabled = true;
                }
                catch
                {
                    // ignore; we'll still lock
                }
            }
            lock (_sync)
            {
                Entries.Add(line);
                while (Entries.Count > MaxEntries)
                {
                    Entries.RemoveAt(0);
                }
                TryWriteToFile(line);
            }
        }

        private static void TryWriteToFile(string line)
        {
            if (string.IsNullOrWhiteSpace(_logFilePath)) return;
            try
            {
                System.IO.File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch
            {
                // ignore file write issues
            }
        }

        public static void AppendException(Exception ex, string source)
        {
            var msg = $"{source}: {ex.GetType().Name}: {ex.Message}";
            Append(msg);
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                Append(ex.StackTrace);
            }
            if (ex.InnerException != null)
            {
                Append($"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                if (!string.IsNullOrWhiteSpace(ex.InnerException.StackTrace))
                {
                    Append(ex.InnerException.StackTrace);
                }
            }
        }
    }
}
