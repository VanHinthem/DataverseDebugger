using System;
using System.Globalization;
using System.Windows.Data;

namespace DataverseDebugger.App.Converters
{
    /// <summary>
    /// Converts boolean values to "ON" or "OFF" strings for display.
    /// </summary>
    public class BooleanToOnOffConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to "ON" (true) or "OFF" (false).
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? "ON" : "OFF";
            }
            return "OFF";
        }

        /// <summary>
        /// Not implemented - one-way converter only.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
