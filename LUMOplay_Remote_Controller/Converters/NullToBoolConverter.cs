using System.Globalization;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Converters
{
    /**
     * Reports whether a bound value is null, so views can show placeholders and
     * empty states without a dedicated flag on the view model. One-way only.
     */
    public class NullToBoolConverter : IValueConverter
    {
        /**
         * Tests the bound value for null.
         *
         * <param name="value">the value to test</param>
         * <param name="targetType">binding target type; unused</param>
         * <param name="parameter">unused</param>
         * <param name="culture">binding culture; unused</param>
         * <returns>true when the value is null</returns>
         */
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null;
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
