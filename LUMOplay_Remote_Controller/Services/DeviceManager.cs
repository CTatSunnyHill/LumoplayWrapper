using CommunityToolkit.Mvvm.ComponentModel;
using LUMOplay_Remote_Controller.Model;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LUMOplay_Remote_Controller.Services
{
    public partial class DeviceManager : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<LumoplayDevice> devices;

        public DeviceManager()
        {
            devices = new ObservableCollection<LumoplayDevice>(LumoplayConfig.Devices);
        }

        /// <summary>
        /// Initializes device connections and synchronizes their current state.
        /// </summary>
        public async Task InitializeDeviceConnectionsAsync()
        {
            Debug.WriteLine("Starting device handshake and state synchronization...");

            var syncTasks = Devices.Select(async device =>
            {
                try
                {
                    var service = new LumoplayService(device);
                    // Fetch the detailed status from the device.
                    var response = await service.CurrentGamePlaylistAsync();

                    if (response != null)
                    {
                        // Connection successful, update the state.
                        device.IsConnected = true;

                        if (response.NowPlayingIndex != null) { 
                            device.IsPlaying = true;
                            string gameIndex = Convert.ToString(response.NowPlayingIndex.Value);
                            device.CurrentGame = LumoplayConfig.GetGameById(gameIndex);
                        
                        }
                      

                        // Find the full LumoplayGame object from the config using the ID.

                        // Reconstruct the playlist with full LumoplayGame objects.
                        if (status.Playlist != null)
                        {
                            device.Playlist = status.Playlist
                                .Select(gameId => LumoplayConfig.GetGameById(gameId))
                                .Where(game => game != null)
                                .ToList()!;
                        }
                        else
                        {
                            device.Playlist = new List<LumoplayGame>();
                        }

                        Debug.WriteLine($"SUCCESS: Synchronized state for device '{device.Name}'.");
                    }
                    else
                    {
                        // Connection failed, set default offline state.
                        device.IsConnected = false;
                        device.IsPlaying = false;
                        device.CurrentGame = null;
                        device.Playlist = null;
                        Debug.WriteLine($"FAILURE: Could not connect to device '{device.Name}'.");
                    }
                }
                catch (Exception ex)
                {
                    // Safeguard for any unexpected errors during the process.
                    device.IsConnected = false;
                    Debug.WriteLine($"ERROR: An exception occurred while synchronizing '{device.Name}': {ex.Message}");
                }
            }).ToList();

            await Task.WhenAll(syncTasks);

            Debug.WriteLine("Device synchronization process completed.");
        }

        public LumoplayDevice? GetDevice(string deviceName)
        {
            return Devices.FirstOrDefault(d => d.Name == deviceName);
        }

        public async Task PlayGameAsync(string deviceName, LumoplayGame game)
        {
            var device = GetDevice(deviceName);
            if (device == null) return;

            var service = new LumoplayService(device);
            bool success = await service.PlayGameAsync(game);
            if (success)
            {
                device.CurrentGame = game;
                device.IsPlaying = true;
                device.IsActive = true;
            }
            else
            {
                device.IsPlaying = false;
                device.IsActive = false;
            }
        }

        public async Task PauseGameAsync(string deviceName)
        {
            var device = GetDevice(deviceName);
            if (device == null) return;

            var service = new LumoplayService(device);
            bool success = await service.PauseContentAsync();
            if (success)
            {
                device.IsPlaying = false;
            }
        }

        public async Task StopGameAsync(string deviceName)
        {
            var device = GetDevice(deviceName);
            if (device == null) return;

            var service = new LumoplayService(device);
            bool success = await service.StopContentAsync();
            if (success)
            {
                device.IsPlaying = false;
                device.CurrentGame = null;
            }
        }

        public async Task NextGameAsync(string deviceName)
        {
            var device = GetDevice(deviceName);
            if (device == null) return;

            var service = new LumoplayService(device);
            bool success = await service.NextContentAsync();
            if (success)
            {
                // Optionally update CurrentGame from playlist
                if (device.Playlist != null && device.CurrentGame != null)
                {
                    int idx = device.Playlist.IndexOf(device.CurrentGame);
                    if (idx >= 0 && idx < device.Playlist.Count - 1)
                        device.CurrentGame = device.Playlist[idx + 1];
                }
            }
        }

        public async Task PreviousGameAsync(string deviceName)
        {
            var device = GetDevice(deviceName);
            if (device == null) return;

            var service = new LumoplayService(device);
            bool success = await service.PreviousContentAsync();
            if (success)
            {
                // Optionally update CurrentGame from playlist
                if (device.Playlist != null && device.CurrentGame != null)
                {
                    int idx = device.Playlist.IndexOf(device.CurrentGame);
                    if (idx > 0)
                        device.CurrentGame = device.Playlist[idx - 1];
                }
            }
        }
    }
}