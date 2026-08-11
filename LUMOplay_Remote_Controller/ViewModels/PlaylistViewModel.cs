using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace LUMOplay_Remote_Controller.ViewModels
{
    /**
     * Backs the playlist page: lists the available playlists and starts one on a
     * device the user picks.
     */
    public partial class PlaylistViewModel : ObservableObject
    {
        private readonly PlaylistManager _playlistManager;
        private readonly DeviceManager _deviceManager;

        /** The playlists shown on the page, owned by the playlist manager. */
        public ObservableCollection<Playlist> Playlists => _playlistManager.Playlists;

        /**
         * <param name="playlistManager">shared playlist collection</param>
         * <param name="deviceManager">shared device state and playback control</param>
         */
        public PlaylistViewModel(PlaylistManager playlistManager, DeviceManager deviceManager)
        {
            _playlistManager = playlistManager;
            _deviceManager = deviceManager;
        }

        /**
         * Asks which device to play on, then starts the playlist there. Unlike
         * the game library's modal popup, this uses the platform action sheet.
         * Does nothing for an empty playlist or if the user cancels.
         *
         * <param name="playlist">the playlist to start; null or empty is ignored</param>
         */
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

            // Devices are matched by name, so two devices sharing a name would be
            // indistinguishable here.
            var selectedDevice = _deviceManager.Devices.FirstOrDefault(d => d.Name == selectedDeviceName);
            if (selectedDevice != null)
            {
                await _deviceManager.PlayPlaylistAsync(selectedDevice, playlist);
            }
        }
    }
}
