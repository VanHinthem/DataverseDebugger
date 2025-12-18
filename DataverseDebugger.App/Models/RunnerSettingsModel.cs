using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Settings for runner execution behavior.
    /// </summary>
    public sealed class RunnerSettingsModel : INotifyPropertyChanged
    {
        private string _writeMode = "FakeWrites";

        /// <summary>Raised when a property value changes.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the write mode (FakeWrites or RealWrites).
        /// </summary>
        /// <remarks>
        /// FakeWrites prevents actual data changes in Dataverse during debugging.
        /// RealWrites allows actual Create/Update/Delete operations.
        /// </remarks>
        public string WriteMode
        {
            get => _writeMode;
            set
            {
                if (string.Equals(_writeMode, value, System.StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _writeMode = string.IsNullOrWhiteSpace(value) ? "FakeWrites" : value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
