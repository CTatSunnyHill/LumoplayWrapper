using CommunityToolkit.Mvvm.ComponentModel;
using LUMOplay_Remote_Controller.Model;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LUMOplay_Remote_Controller.Services
{
    /**
     * The app's single source of truth for device state, and the layer the UI
     * drives playback through. A singleton, so every page sees the same devices.
     *
     * State is only updated after the device confirms a command, which keeps the
     * UI honest about what is actually on screen — at the cost of a visible
     * delay while each command round-trips.
     */
    public partial class DeviceManager : ObservableObject
    {
        /** The devices shown in the UI, seeded from the built-in configuration. */
        [ObservableProperty]
        private ObservableCollection<LumoplayDevice> devices;

        /** Seeds the collection from <see cref="LumoplayConfig.Devices"/>. */
        public DeviceManager()
        {
            devices = new ObservableCollection<LumoplayDevice>(LumoplayConfig.Devices);
        }

        /**
         * Initializes device connections and synchronizes their current state.
         * Every device is contacted in parallel and failures are contained per
         * device — one unreachable unit is marked offline without affecting the
         * others or failing the call.
         */
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

                        if (response.NowPlayingIndex.HasValue)
                        {
                            int nowPlayingIndex = response.NowPlayingIndex.Value;
                            Debug.WriteLine($"SUCCESS: Now Playing Index: '{nowPlayingIndex}'");
                            int gameId = response.Scenes[nowPlayingIndex].Scene.ID;
                            Debug.WriteLine($"SUCCESS: Now Playing Scene: '{gameId}'");

                            device.IsPlaying = true;
                            // Null when the device is running something absent
                            // from the local catalogue.
                            device.CurrentGame = LumoplayConfig.GetGameById(gameId);
                        }

                        Debug.WriteLine($"SUCCESS: Synchronized state for device '{device.Name}'. Current game '{device.CurrentGame}'");
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

        /**
         * Looks up a tracked device by address.
         *
         * <param name="deviceIPAddress">address of the device</param>
         * <returns>the device, or null when it is not in the collection</returns>
         */
        public LumoplayDevice? GetDevice(string deviceIPAddress)
        {
            return Devices.FirstOrDefault(d => d.IpAddress == deviceIPAddress);
        }

        /**
         * Starts a playlist by launching its first game and binding the playlist
         * to the device, so next/previous can walk it. Does nothing for a null
         * or empty playlist.
         *
         * <param name="device">the device to play on</param>
         * <param name="playlist">the playlist to start</param>
         */
        public async Task PlayPlaylistAsync(LumoplayDevice device, Playlist playlist)
        {
            if (playlist == null || !playlist.Games.Any())
                return;

            var firstGame = playlist.Games.First();
            await PlayGameAsync(device.IpAddress, firstGame, playlist);
        }

        /**
         * Launches a game on a device and records the result. Every other
         * playback method routes through here. An unknown device is ignored, and
         * a refused command leaves the device marked as not playing.
         *
         * <param name="deviceIPAddress">address of the target device</param>
         * <param name="game">the game to launch</param>
         * <param name="playlist">the playlist this launch is part of, or null
         * for a standalone launch</param>
         */
        public async Task PlayGameAsync(string deviceIPAddress, LumoplayGame game, Playlist playlist = null)
        {
            var device = GetDevice(deviceIPAddress);
            if (device == null) return;

            var service = new LumoplayService(device);
            bool success = await service.PlayGameAsync(game);
            if (success)
            {
                device.CurrentGame = game;
                device.IsPlaying = true;
                device.Playlist = playlist;
            }
            else
            {
                device.IsPlaying = false;
            }
        }

        /**
         * Pauses playback, leaving the current game and playlist bound so it can
         * be resumed.
         *
         * <param name="deviceIPAddress">address of the target device</param>
         */
        public async Task PauseGameAsync(string deviceIPAddress)
        {
            var device = GetDevice(deviceIPAddress);
            if (device == null) return;

            var service = new LumoplayService(device);
            bool success = await service.PauseContentAsync();
            if (success)
            {
                device.IsPlaying = false;
            }
        }

        /**
         * Stops playback and clears the device's game and playlist — unlike
         * <see cref="PauseGameAsync"/>, there is nothing left to resume.
         *
         * <param name="deviceIPAddress">address of the target device</param>
         */
        public async Task StopGameAsync(string deviceIPAddress)
        {
            var device = GetDevice(deviceIPAddress);
            if (device == null) return;

            var service = new LumoplayService(device);
            bool success = await service.StopContentAsync();
            if (success)
            {
                device.IsPlaying = false;
                device.CurrentGame = null;
                device.Playlist = null;
            }
        }

        /**
         * Advances to the next game in the device's playlist. Stops at the end
         * rather than wrapping, and does nothing when no playlist is bound or the
         * current game is not part of it.
         *
         * <param name="deviceIPAddress">address of the target device</param>
         */
        public async Task NextGameAsync(string deviceIPAddress)
        {
            var device = GetDevice(deviceIPAddress);
            if (device?.Playlist == null || device.CurrentGame == null) return;

            int currentIndex = device.Playlist.Games.IndexOf(device.CurrentGame);
            if (currentIndex >= 0 && currentIndex < device.Playlist.Games.Count - 1)
            {
                var nextGame = device.Playlist.Games[currentIndex + 1];
                await PlayGameAsync(deviceIPAddress, nextGame, device.Playlist);
            }
        }

        /**
         * Steps back to the previous game in the device's playlist. Stops at the
         * start rather than wrapping.
         *
         * <param name="deviceIPAddress">address of the target device</param>
         */
        public async Task PreviousGameAsync(string deviceIPAddress)
        {
            var device = GetDevice(deviceIPAddress);
            if (device?.Playlist == null || device.CurrentGame == null) return;

            int currentIndex = device.Playlist.Games.IndexOf(device.CurrentGame);
            if (currentIndex > 0)
            {
                var previousGame = device.Playlist.Games[currentIndex - 1];
                await PlayGameAsync(deviceIPAddress, previousGame, device.Playlist);
            }
        }
    }
}
