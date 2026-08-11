using System.Globalization;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Converters
{
    /**
     * Maps a connection flag to the status word shown beside a device.
     * One-way only.
     */
    public class BoolToStatusConverter : IValueConverter
    {
        /**
         * Picks the status word for the given flag.
         *
         * <param name="value">the connection flag</param>
         * <param name="targetType">binding target type; unused</param>
         * <param name="parameter">unused</param>
         * <param name="culture">binding culture; unused</param>
         * <returns>"Active", "Inactive", or "Unknown" when the value is not a boolean</returns>
         */
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isConnected)
            {
                return isConnected ? "Active" : "Inactive";
            }
            return "Unknown";
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
