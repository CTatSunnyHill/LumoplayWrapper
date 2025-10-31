using CommunityToolkit.Mvvm.ComponentModel;
using LUMOplay_Remote_Controller.Model;
using System.Collections.ObjectModel;
using System.Linq;

namespace LUMOplay_Remote_Controller.Services
{
    public partial class DeviceManager : ObservableObject
    {
        private static DeviceManager? _instance;
        public static DeviceManager Instance => _instance ??= new DeviceManager();

        [ObservableProperty]
        private ObservableCollection<DeviceState> devices = new();

        private DeviceManager()
        {
            foreach (var device in LumoplayConfig.Devices)
            {
                Devices.Add(new DeviceState { Device = device });
            }
        }

        public DeviceState? GetDeviceState(string deviceName)
        {
            return Devices.FirstOrDefault(d => d.Device.Name == deviceName);
        }

        public async Task PlayGameAsync(string deviceName, LumoplayGame game)
        {
            var deviceState = GetDeviceState(deviceName);
            if (deviceState == null) return;

            var service = new LumoplayService(deviceState.Device);
            bool success = await service.PlayGameAsync(game);
            if (success)
            {
                deviceState.CurrentGame = game;
                deviceState.IsPlaying = true;
                deviceState.IsActive = true;
            }
            else
            {
                deviceState.IsPlaying = false;
                deviceState.IsActive = false;
            }
        }

        public async Task PauseGameAsync(string deviceName)
        {
            var deviceState = GetDeviceState(deviceName);
            if (deviceState == null) return;

            var service = new LumoplayService(deviceState.Device);
            bool success = await service.PauseContentAsync();
            if (success)
            {
                deviceState.IsPlaying = false;
            }
        }

        public async Task StopGameAsync(string deviceName)
        {
            var deviceState = GetDeviceState(deviceName);
            if (deviceState == null) return;

            var service = new LumoplayService(deviceState.Device);
            bool success = await service.StopContentAsync();
            if (success)
            {
                deviceState.IsPlaying = false;
                deviceState.CurrentGame = null;
            }
        }

        public async Task NextGameAsync(string deviceName)
        {
            var deviceState = GetDeviceState(deviceName);
            if (deviceState == null) return;

            var service = new LumoplayService(deviceState.Device);
            bool success = await service.NextContentAsync();
            if (success)
            {
                // Optionally update CurrentGame from playlist
                if (deviceState.Playlist != null && deviceState.CurrentGame != null)
                {
                    int idx = deviceState.Playlist.IndexOf(deviceState.CurrentGame);
                    if (idx >= 0 && idx < deviceState.Playlist.Count - 1)
                        deviceState.CurrentGame = deviceState.Playlist[idx + 1];
                }
            }
        }

        public async Task PreviousGameAsync(string deviceName)
        {
            var deviceState = GetDeviceState(deviceName);
            if (deviceState == null) return;

            var service = new LumoplayService(deviceState.Device);
            bool success = await service.PreviousContentAsync();
            if (success)
            {
                // Optionally update CurrentGame from playlist
                if (deviceState.Playlist != null && deviceState.CurrentGame != null)
                {
                    int idx = deviceState.Playlist.IndexOf(deviceState.CurrentGame);
                    if (idx > 0)
                        deviceState.CurrentGame = deviceState.Playlist[idx - 1];
                }
            }
        }

       
    }
}
