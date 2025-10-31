using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Converters
{
    public class PlayPauseIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? "?" : "?";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
