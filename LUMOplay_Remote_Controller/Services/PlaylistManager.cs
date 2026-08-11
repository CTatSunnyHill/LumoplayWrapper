using CommunityToolkit.Mvvm.ComponentModel;
using LUMOplay_Remote_Controller.Model;
using System.Collections.ObjectModel;

namespace LUMOplay_Remote_Controller.Services
{
    /**
     * Holds the app's playlists as an observable collection for the UI to bind
     * to. Seeded from the built-in configuration; it does not yet read playlists
     * from the backend.
     */
    public partial class PlaylistManager : ObservableObject
    {
        /** The playlists shown in the UI. */
        [ObservableProperty]
        private ObservableCollection<Playlist> playlists;

        /** Seeds the collection from <see cref="LumoplayConfig.Playlists"/>. */
        public PlaylistManager()
        {
            // Initialize with dummy data for now
            playlists = new ObservableCollection<Playlist>(LumoplayConfig.Playlists);
        }
    }
}
