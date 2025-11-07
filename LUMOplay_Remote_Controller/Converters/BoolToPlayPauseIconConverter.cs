using System.Globalization;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Converters
{
    public class BoolToPlayPauseIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying)
            {
                // Material Icon characters for 'pause' and 'play_arrow'
                return isPlaying ? "\uE034" : "\uE037";
            }
            return "\uE037"; // Default to play icon
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}