using System;
using System.Windows;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Manages application theme switching between light and dark modes.
    /// </summary>
    /// <remarks>
    /// Provides centralized theme management with event notifications for theme changes.
    /// Theme resources are loaded from App.xaml resource dictionaries.
    /// </remarks>
    public static class ThemeService
    {
        private static bool _isDarkMode = true;

        /// <summary>
        /// Occurs when the theme is changed.
        /// </summary>
        public static event EventHandler? ThemeChanged;

        /// <summary>
        /// Gets or sets whether dark mode is enabled.
        /// </summary>
        public static bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    ApplyTheme();
                    ThemeChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Initializes the theme service with the specified mode.
        /// </summary>
        /// <param name="isDarkMode">Whether to use dark mode.</param>
        public static void Initialize(bool isDarkMode)
        {
            _isDarkMode = isDarkMode;
            ApplyTheme();
        }

        /// <summary>
        /// Applies the current theme to the application.
        /// </summary>
        private static void ApplyTheme()
        {
            var app = Application.Current;
            if (app == null) return;

            // Find and update the theme dictionary
            var themeUri = _isDarkMode
                ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

            // Remove existing theme dictionary if present
            ResourceDictionary? existingTheme = null;
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Source != null && 
                    (dict.Source.OriginalString.Contains("DarkTheme") || 
                     dict.Source.OriginalString.Contains("LightTheme")))
                {
                    existingTheme = dict;
                    break;
                }
            }

            if (existingTheme != null)
            {
                app.Resources.MergedDictionaries.Remove(existingTheme);
            }

            // Add new theme dictionary
            var newTheme = new ResourceDictionary { Source = themeUri };
            app.Resources.MergedDictionaries.Add(newTheme);
        }
    }
}
