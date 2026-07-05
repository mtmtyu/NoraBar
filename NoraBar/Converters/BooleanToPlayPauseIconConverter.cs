using System;
using System.Globalization;
using System.Windows.Data;

namespace NoraBar.Converters
{
    public class BooleanToPlayPauseIconConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPlaying)
            {
                // Fluent Icon for Pause is E769, Play is E768
                return isPlaying ? "\xE769" : "\xE768";
            }
            return "\xE768";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
