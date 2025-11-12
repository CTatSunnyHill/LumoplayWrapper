using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace LUMOplay_Remote_Controller.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DeviceManager _deviceManager;
        public ObservableCollection<LumoplayDevice> Devices => _deviceManager.Devices;

        public DashboardViewModel(DeviceManager deviceManager)
        {
            _deviceManager = deviceManager;
        }

        [RelayCommand]
        private async Task PreviousGameAsync(LumoplayDevice device)
        {
            if (device == null) return;
            await _deviceManager.PreviousGameAsync(device.IpAddress);
        }

        [RelayCommand]
        private async Task TogglePlayPauseAsync(LumoplayDevice device)
        {
            if (device == null) return;

            if (device.IsPlaying)
            {
                await _deviceManager.PauseGameAsync(device.IpAddress);
            }
            else
            {
                // This assumes you want to resume the current game.
                // If there's no current game, this might need different logic,
                // like starting the first game in the playlist.
                if (device.CurrentGame != null)
                {
                    await _deviceManager.PlayGameAsync(device.IpAddress, device.CurrentGame, device.Playlist);
                }
            }
        }

        [RelayCommand]
        private async Task NextGameAsync(LumoplayDevice device)
        {
            if (device == null) return;
            await _deviceManager.NextGameAsync(device.IpAddress);
        }
    }
}
