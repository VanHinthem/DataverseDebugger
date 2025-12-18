using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DataverseDebugger.App.Converters
{
    /// <summary>
    /// Converts HTTP status codes to display strings.
    /// </summary>
    /// <remarks>
    /// Returns "-" for zero/null, otherwise the numeric code.
    /// </remarks>
    public sealed class StatusConverter : IValueConverter
    {
        /// <summary>
        /// Converts status code integer to display string.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int code)
            {
                return code == 0 ? "-" : code.ToString();
            }
            return "-";
        }

        /// <summary>Not implemented - one-way converter only.</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts HTTP status codes to colored brushes.
    /// </summary>
    /// <remarks>
    /// Green for 2xx success, amber for 3xx redirect, red for 4xx/5xx errors.
    /// </remarks>
    public sealed class StatusBrushConverter : IValueConverter
    {
        private static readonly Brush Success = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        private static readonly Brush Redirect = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00));
        private static readonly Brush Error = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
        private static readonly Brush Neutral = new SolidColorBrush(Color.FromRgb(0x45, 0x5A, 0x64));

        /// <summary>
        /// Converts status code to corresponding color brush.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int code && code > 0)
            {
                if (code >= 200 && code < 300) return Success;
                if (code >= 300 && code < 400) return Redirect;
                return Error;
            }
            return Neutral;
        }

        /// <summary>Not implemented - one-way converter only.</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
