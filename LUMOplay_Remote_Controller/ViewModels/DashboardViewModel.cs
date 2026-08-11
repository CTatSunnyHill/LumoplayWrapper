using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace LUMOplay_Remote_Controller.ViewModels
{
    /**
     * Backs the dashboard's per-device transport controls. Holds no state of its
     * own: the device collection is the manager's, and the manager updates each
     * device as commands succeed.
     */
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DeviceManager _deviceManager;
        /** The devices shown on the dashboard, owned by the device manager. */
        public ObservableCollection<LumoplayDevice> Devices => _deviceManager.Devices;

        /**
         * <param name="deviceManager">shared device state and playback control</param>
         */
        public DashboardViewModel(DeviceManager deviceManager)
        {
            _deviceManager = deviceManager;
        }

        /**
         * Steps a device back to the previous game in its playlist.
         *
         * <param name="device">the device to act on; null is ignored</param>
         */
        [RelayCommand]
        private async Task PreviousGameAsync(LumoplayDevice device)
        {
            if (device == null) return;
            await _deviceManager.PreviousGameAsync(device.IpAddress);
        }

        /**
         * Pauses a playing device, or resumes a paused one by relaunching its
         * current game. A paused device with no current game does nothing, since
         * there is nothing to resume.
         *
         * <param name="device">the device to act on; null is ignored</param>
         */
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

        /**
         * Advances a device to the next game in its playlist.
         *
         * <param name="device">the device to act on; null is ignored</param>
         */
        [RelayCommand]
        private async Task NextGameAsync(LumoplayDevice device)
        {
            if (device == null) return;
            await _deviceManager.NextGameAsync(device.IpAddress);
        }
    }
}
