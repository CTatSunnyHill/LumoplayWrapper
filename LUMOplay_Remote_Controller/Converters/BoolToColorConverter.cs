using System.Globalization;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Converters
{
    /**
     * Maps a bound boolean to a colour, with the converter parameter choosing
     * which palette to use. One-way only: colours cannot be turned back into
     * booleans.
     */
    public class BoolToColorConverter : IValueConverter
    {
        /**
         * Picks a colour for the given flag.
         *
         * <param name="value">the bound flag; anything else yields grey</param>
         * <param name="targetType">binding target type; unused</param>
         * <param name="parameter">"PlayPause" for the accent/grey pair,
         * "MediaControl" for the black/light-grey pair, anything else for
         * green/red status</param>
         * <param name="culture">binding culture; unused</param>
         * <returns>the colour to apply</returns>
         */
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

        /**
         * Not supported — this conversion is one-way.
         *
         * <param name="value">unused</param>
         * <param name="targetType">unused</param>
         * <param name="parameter">unused</param>
         * <param name="culture">unused</param>
         * <returns>never returns</returns>
         */
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
