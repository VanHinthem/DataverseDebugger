using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Model for application appearance settings.
    /// </summary>
    /// <remarks>
    /// Contains settings for UI theming and visual preferences.
    /// </remarks>
    public sealed class AppearanceSettingsModel : INotifyPropertyChanged
    {
        private bool _isDarkMode = false;

        /// <summary>
        /// Gets or sets whether dark mode is enabled.
        /// </summary>
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
