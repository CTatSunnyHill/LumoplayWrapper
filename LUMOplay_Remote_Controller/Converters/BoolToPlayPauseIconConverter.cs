using System.Globalization;
using Microsoft.Maui.Controls;

namespace LUMOplay_Remote_Controller.Converters
{
    /**
     * Maps a playing flag to the Material Icons glyph for the button that acts
     * on it — pause while playing, play while stopped. One-way only.
     */
    public class BoolToPlayPauseIconConverter : IValueConverter
    {
        /**
         * Picks the glyph for the given playback state.
         *
         * <param name="value">true while playing; anything else falls back to play</param>
         * <param name="targetType">binding target type; unused</param>
         * <param name="parameter">unused</param>
         * <param name="culture">binding culture; unused</param>
         * <returns>a Material Icons character, to be rendered in that font</returns>
         */
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying)
            {
                // Material Icon characters for 'pause' and 'play_arrow'
                return isPlaying ? "\uE034" : "\uE037";
            }
            return "\uE037"; // Default to play icon
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
