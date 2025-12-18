using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Settings for the embedded WebView2 browser behavior.
    /// </summary>
    public class BrowserSettingsModel : INotifyPropertyChanged
    {
        private bool _disableCaching = true;
        private bool _bypassServiceWorker = true;
        private bool _openDevToolsOnActivate;

        /// <summary>
        /// Gets or sets whether browser caching is disabled.
        /// </summary>
        public bool DisableCaching
        {
            get => _disableCaching;
            set { if (_disableCaching != value) { _disableCaching = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether to bypass the service worker for requests.
        /// </summary>
        public bool BypassServiceWorker
        {
            get => _bypassServiceWorker;
            set { if (_bypassServiceWorker != value) { _bypassServiceWorker = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether to automatically open DevTools when activating the browser.
        /// </summary>
        public bool OpenDevToolsOnActivate
        {
            get => _openDevToolsOnActivate;
            set { if (_openDevToolsOnActivate != value) { _openDevToolsOnActivate = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
