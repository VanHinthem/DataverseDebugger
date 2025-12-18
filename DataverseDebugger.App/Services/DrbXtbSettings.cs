using System;
using System.Runtime.InteropServices;
using DataverseDebugger.App.Models;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// COM-visible settings object exposed to the embedded DataverseRESTBuilder.
    /// </summary>
    /// <remarks>
    /// Provides the access token, org URL, and version info to the JavaScript
    /// code running in the WebView2 browser control.
    /// </remarks>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class DrbXtbSettings
    {
        private readonly Func<EnvironmentProfile?> _profileProvider;

        /// <summary>
        /// Initializes a new instance with a profile provider function.
        /// </summary>
        /// <param name="profileProvider">Function that returns the current environment profile.</param>
        public DrbXtbSettings(Func<EnvironmentProfile?> profileProvider)
        {
            _profileProvider = profileProvider ?? (() => null);
        }

        public string Token
        {
            get
            {
                try
                {
                    return _profileProvider()?.LastAccessToken ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string Url
        {
            get
            {
                try
                {
                    var raw = _profileProvider()?.OrgUrl;
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        return string.Empty;
                    }

                    var trimmed = raw.Trim();
                    if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) &&
                        (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
                    {
                        return absolute.GetLeftPart(UriPartial.Authority);
                    }

                    var withScheme = "https://" + trimmed;
                    if (Uri.TryCreate(withScheme, UriKind.Absolute, out var normalized))
                    {
                        return normalized.GetLeftPart(UriPartial.Authority);
                    }

                    return trimmed.TrimEnd('/');
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string Version
        {
            get
            {
                return "9.2.0.0";
            }
        }

        public bool IsDarkMode
        {
            get
            {
                try
                {
                    return ThemeService.IsDarkMode;
                }
                catch
                {
                    return true;
                }
            }
        }
    }
}
