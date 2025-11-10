using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace LUMOplay_Remote_Controller.ViewModels
{
    public partial class PlaylistViewModel : ObservableObject
    {
        private readonly PlaylistManager _playlistManager;
        private readonly DeviceManager _deviceManager;

        public ObservableCollection<Playlist> Playlists => _playlistManager.Playlists;

        public PlaylistViewModel(PlaylistManager playlistManager, DeviceManager deviceManager)
        {
            _playlistManager = playlistManager;
            _deviceManager = deviceManager;
        }

        [RelayCommand]
        private async Task LaunchPlaylist(Playlist playlist)
        {
            if (playlist == null || !playlist.Games.Any())
                return;

            var deviceNames = _deviceManager.Devices.Select(d => d.Name).ToArray();
            if (!deviceNames.Any())
            {
                await Application.Current.MainPage.DisplayAlert("No Devices", "No devices are available to play the game.", "OK");
                return;
            }

            var selectedDeviceName = await Application.Current.MainPage.DisplayActionSheet("Select a Device", "Cancel", null, deviceNames);

            if (selectedDeviceName == null || selectedDeviceName == "Cancel")
                return;

            var selectedDevice = _deviceManager.Devices.FirstOrDefault(d => d.Name == selectedDeviceName);
            if (selectedDevice != null)
            {
                await _deviceManager.PlayPlaylistAsync(selectedDevice, playlist);
            }
        }
    }
}