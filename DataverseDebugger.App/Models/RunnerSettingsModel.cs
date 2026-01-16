using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Settings for runner execution behavior.
    /// </summary>
    public sealed class RunnerSettingsModel : INotifyPropertyChanged
    {
        private string _executionMode = "Hybrid";
        private bool _allowLiveWrites;

        /// <summary>Raised when a property value changes.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the execution mode (Offline, Hybrid, or Online).
        /// </summary>
        public string ExecutionMode
        {
            get => _executionMode;
            set
            {
                var normalized = NormalizeExecutionMode(value);
                if (string.Equals(_executionMode, normalized, System.StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _executionMode = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WriteMode));
            }
        }

        /// <summary>
        /// Gets or sets whether live writes are explicitly allowed.
        /// </summary>
        public bool AllowLiveWrites
        {
            get => _allowLiveWrites;
            set
            {
                if (_allowLiveWrites == value)
                {
                    return;
                }

                _allowLiveWrites = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WriteMode));
            }
        }

        /// <summary>
        /// Gets or sets the write mode (FakeWrites or LiveWrites).
        /// </summary>
        /// <remarks>
        /// FakeWrites prevents actual data changes in Dataverse during debugging.
        /// LiveWrites allows actual Create/Update/Delete operations.
        /// </remarks>
        public string WriteMode
        {
            get => IsLiveWrites(ExecutionMode, AllowLiveWrites) ? "LiveWrites" : "FakeWrites";
            set
            {
                var wantsLiveWrites =
                    string.Equals(value, "LiveWrites", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "Live", System.StringComparison.OrdinalIgnoreCase);

                if (wantsLiveWrites)
                {
                    if (!string.Equals(ExecutionMode, "Online", System.StringComparison.OrdinalIgnoreCase))
                    {
                        ExecutionMode = "Online";
                    }

                    AllowLiveWrites = true;
                }
                else
                {
                    AllowLiveWrites = false;
                }
            }
        }

        private static bool IsLiveWrites(string executionMode, bool allowLiveWrites)
        {
            return string.Equals(executionMode, "Online", System.StringComparison.OrdinalIgnoreCase) && allowLiveWrites;
        }

        private static string NormalizeExecutionMode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Hybrid";
            }

            if (string.Equals(value, "Offline", System.StringComparison.OrdinalIgnoreCase))
            {
                return "Offline";
            }

            if (string.Equals(value, "Online", System.StringComparison.OrdinalIgnoreCase))
            {
                return "Online";
            }

            if (string.Equals(value, "Hybrid", System.StringComparison.OrdinalIgnoreCase))
            {
                return "Hybrid";
            }

            return value;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
