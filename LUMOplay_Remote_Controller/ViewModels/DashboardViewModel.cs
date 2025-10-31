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
        public ObservableCollection<DeviceState> Devices => DeviceManager.Instance.Devices;

        public DashboardViewModel()
        {
            // Optionally, refresh or load devices here
        }

        [RelayCommand]
        private async Task PreviousGameAsync(DeviceState deviceState)
        {
            if (deviceState == null) return;
            await DeviceManager.Instance.PreviousGameAsync(deviceState.Device.Name);
        }

        [RelayCommand]
        private async Task TogglePlayPauseAsync(DeviceState deviceState)
        {
            if (deviceState == null) return;

            if (deviceState.IsPlaying)
            {
                await DeviceManager.Instance.PauseGameAsync(deviceState.Device.Name);
            }
            else
            {
                // This assumes you want to resume the current game.
                // If there's no current game, this might need different logic,
                // like starting the first game in the playlist.
                if (deviceState.CurrentGame != null)
                {
                    await DeviceManager.Instance.PlayGameAsync(deviceState.Device.Name, deviceState.CurrentGame);
                }
            }
        }

        [RelayCommand]
        private async Task NextGameAsync(DeviceState deviceState)
        {
            if (deviceState == null) return;
            await DeviceManager.Instance.NextGameAsync(deviceState.Device.Name);
        }
    }
}
