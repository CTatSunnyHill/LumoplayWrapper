using CommunityToolkit.Mvvm.ComponentModel;
using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.Services;
using System.Collections.ObjectModel;

namespace LUMOplay_Remote_Controller.ViewModels
{
    public partial class PlaylistViewModel : ObservableObject
    {
        private readonly PlaylistManager _playlistManager;

        public ObservableCollection<Playlist> Playlists => _playlistManager.Playlists;

        public PlaylistViewModel(PlaylistManager playlistManager)
        {
            _playlistManager = playlistManager;
        }
    }
}