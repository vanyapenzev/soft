using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VagImmoEditor.Pro.UI.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                var invert = parameter?.ToString()?.ToLower() == "invert";
                return boolValue ^ invert ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    public class CrcStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToUpper() switch
                {
                    "VALID" => System.Windows.Media.Brushes.Green,
                    "INVALID" => System.Windows.Media.Brushes.Red,
                    "UNKNOWN" => System.Windows.Media.Brushes.Gray,
                    _ => System.Windows.Media.Brushes.Black
                };
            }
            return System.Windows.Media.Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
