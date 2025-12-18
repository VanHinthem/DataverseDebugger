using System;
using System.Globalization;
using System.Windows.Data;

namespace DataverseDebugger.App.Converters
{
    /// <summary>
    /// Splits a log line into timestamp and message components.
    /// </summary>
    public sealed class LogLinePartConverter : IValueConverter
    {
        public LogLinePart Part { get; set; } = LogLinePart.Message;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var line = value as string ?? string.Empty;
            var (timestamp, message) = Split(line);
            return Part == LogLinePart.Timestamp ? timestamp : message;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static (string Timestamp, string Message) Split(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return (string.Empty, string.Empty);
            }

            var firstSpace = line.IndexOf(' ');
            if (firstSpace > 0 && firstSpace < line.Length - 1)
            {
                var timestamp = line.Substring(0, firstSpace);
                var message = line.Substring(firstSpace + 1);
                return (timestamp, message);
            }

            return (string.Empty, line);
        }
    }

    public enum LogLinePart
    {
        Timestamp,
        Message
    }
}
