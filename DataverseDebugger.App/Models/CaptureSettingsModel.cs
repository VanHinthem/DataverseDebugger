using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Settings for request capture behavior in the browser.
    /// </summary>
    /// <remarks>
    /// Includes settings for capture filtering, auto-proxy, debugging, and
    /// user impersonation for Web API requests.
    /// </remarks>
    public class CaptureSettingsModel : INotifyPropertyChanged
    {
        private string? _navigateUrl;
        private bool _apiOnly = true;
        private bool _captureWebResources;
        private bool _autoProxy = true;
        private bool _autoDebugMatched;
        private bool _captureEnabled = true;
        private Guid? _impersonatedUserId;
        private string? _impersonatedUserName;

        /// <summary>
        /// Gets or sets the URL to navigate to when activating the environment.
        /// </summary>
        public string? NavigateUrl
        {
            get => _navigateUrl;
            set { if (_navigateUrl != value) { _navigateUrl = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether to capture Web API requests.
        /// </summary>
        public bool ApiOnly
        {
            get => _apiOnly;
            set { if (_apiOnly != value) { _apiOnly = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether to capture WebResource requests.
        /// </summary>
        public bool CaptureWebResources
        {
            get => _captureWebResources;
            set { if (_captureWebResources != value) { _captureWebResources = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether to automatically proxy captured requests through the runner.
        /// </summary>
        public bool AutoProxy
        {
            get => _autoProxy;
            set { if (_autoProxy != value) { _autoProxy = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether to automatically debug requests that match plugin steps.
        /// </summary>
        public bool AutoDebugMatched
        {
            get => _autoDebugMatched;
            set { if (_autoDebugMatched != value) { _autoDebugMatched = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether request capture is currently enabled.
        /// </summary>
        public bool CaptureEnabled
        {
            get => _captureEnabled;
            set { if (_captureEnabled != value) { _captureEnabled = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets the ID of the user to impersonate for Web API requests.
        /// When set, the MSCRMCallerID header will be added to proxied requests.
        /// </summary>
        public Guid? ImpersonatedUserId
        {
            get => _impersonatedUserId;
            set { if (_impersonatedUserId != value) { _impersonatedUserId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets the display name of the impersonated user (for UI display).
        /// </summary>
        public string? ImpersonatedUserName
        {
            get => _impersonatedUserName;
            set { if (_impersonatedUserName != value) { _impersonatedUserName = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets whether impersonation is currently active.
        /// </summary>
        public bool IsImpersonating => _impersonatedUserId.HasValue && _impersonatedUserId.Value != Guid.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
