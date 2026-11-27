using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using EchoStem.Core.Api.Lyrics;

namespace EchoStem.Converters
{
    public class LyricSourceVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Hidden;
            }

            return (SourceTypeEnum) value == (SourceTypeEnum) parameter ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}