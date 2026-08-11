using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace LUMOplay_Remote_Controller.Model
{
    /**
     * An ordered run of games a device can play through. Observable so the UI
     * re-renders when a playlist is renamed or its contents change.
     */
    public partial class Playlist : ObservableObject
    {
        /** Display name of the playlist. */
        [ObservableProperty]
        private string name;

        /** The games to play, in playback order. */
        [ObservableProperty]
        private List<LumoplayGame> games;
    }
}
