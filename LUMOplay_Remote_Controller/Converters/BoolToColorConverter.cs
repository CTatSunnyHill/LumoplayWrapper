using System.Globalization;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool isConnected)
                return Colors.Gray;

            string param = parameter as string;

            if (param == "PlayPause")
            {
                return isConnected ? Color.FromArgb("#6200EE") : Colors.Gray;
            }
            
            if (param == "MediaControl")
            {
                return isConnected ? Colors.Black : Colors.LightGray;
            }

            return isConnected ? Colors.Green : Colors.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}