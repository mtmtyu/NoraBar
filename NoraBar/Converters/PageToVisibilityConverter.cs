using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NoraBar.Converters
{
    public class PageToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentPage && parameter is string targetPage)
            {
                return currentPage == targetPage ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
