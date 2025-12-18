using System.ComponentModel;
using System.Runtime.CompilerServices;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.App.Runner
{
    /// <summary>
    /// View model for displaying runner process health status and execution trace.
    /// </summary>
    /// <remarks>
    /// Provides bindable properties for the runner status display in the UI,
    /// including status text, capabilities, and trace output.
    /// </remarks>
    public sealed class RunnerHealthViewModel : INotifyPropertyChanged
    {
        private string _statusText = "Checking runner...";
        private string _capabilitiesText = string.Empty;
        private string _initStatusText = string.Empty;
        private string _executeStatusText = string.Empty;
        private HealthStatus _status = HealthStatus.Unknown;
        private System.Collections.ObjectModel.ObservableCollection<string> _traceLines = new System.Collections.ObjectModel.ObservableCollection<string>();

        /// <summary>Gets or sets the main status text.</summary>
        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        /// <summary>Gets or sets the runner capabilities text.</summary>
        public string CapabilitiesText
        {
            get => _capabilitiesText;
            set => SetField(ref _capabilitiesText, value);
        }

        /// <summary>Gets or sets the workspace initialization status text.</summary>
        public string InitStatusText
        {
            get => _initStatusText;
            set => SetField(ref _initStatusText, value);
        }

        /// <summary>Gets or sets the execution status text.</summary>
        public string ExecuteStatusText
        {
            get => _executeStatusText;
            set => SetField(ref _executeStatusText, value);
        }

        /// <summary>Gets or sets the current health status.</summary>
        public HealthStatus Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        /// <summary>Gets or sets the execution trace lines collection.</summary>
        public System.Collections.ObjectModel.ObservableCollection<string> TraceLines
        {
            get => _traceLines;
            set => SetField(ref _traceLines, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value))
            {
                return;
            }
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
