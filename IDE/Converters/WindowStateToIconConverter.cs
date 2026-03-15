using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace IDE.Converters
{
    public class WindowStateToIconConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (value is WindowState state)
            {
                string iconPath = state == WindowState.Maximized
                    ? "pack://application:,,,/Resources/Icons/restore.png"
                    : "pack://application:,,,/Resources/Icons/maxsize.png";

                return new BitmapImage(new Uri(iconPath));
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}