using CommunityToolkit.Mvvm.ComponentModel;
using LUMOplay_Remote_Controller.Model;
using System.Collections.ObjectModel;

namespace LUMOplay_Remote_Controller.Services
{
    public partial class PlaylistManager : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Playlist> playlists;

        public PlaylistManager()
        {
            // Initialize with dummy data for now
            playlists = new ObservableCollection<Playlist>(LumoplayConfig.Playlists);
        }
    }
}