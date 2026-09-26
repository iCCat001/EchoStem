using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dopamine.Converters
{
    public class LyricsContentMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length >= 4 && values[0] is bool && values[1] is bool && values[2] is double && values[3] is double)
            {
                if ((bool)values[0] && (bool)values[1])
                {
                    return new Thickness((double)values[2], 0, (double)values[3], 0);
                }
            }

            return new Thickness(0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
