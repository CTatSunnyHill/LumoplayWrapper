using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace LUMOplay_Remote_Controller.ViewModels
{
    /**
     * Backs the game library page: loads the catalogue from the backend and
     * launches a chosen game on a chosen device.
     *
     * There are two launch paths. <see cref="LaunchGameCommand"/> uses this view
     * model's own <see cref="SelectedDevice"/>, while
     * <see cref="LaunchGameAsync"/> takes the device as an argument, for the
     * page's modal picker flow.
     */
    public partial class GameLibraryViewModel : ObservableObject
    {
        private readonly DeviceManager _deviceManager;

        private readonly LumoPlayApiClient _apiClient;
        /** The catalogue shown on the page, refilled by <see cref="LoadGames"/>. */
        public ObservableCollection<LumoplayGame> Games { get; }
        /** The devices available to launch on, owned by the device manager. */
        public ObservableCollection<LumoplayDevice> Devices => _deviceManager.Devices;

        /** Device the in-page launch command targets. */
        [ObservableProperty]
        private LumoplayDevice selectedDevice;

        /** True while a load is in flight; also guards against overlapping loads. */
        [ObservableProperty]
        private bool isBusy;

        // Shows an error message if something fails
        /** Message to show when <see cref="HasError"/> is set. */
        [ObservableProperty]
        private string errorMessage;

        /** True when the last load failed or returned nothing. */
        [ObservableProperty]
        private bool hasError;

        /** Launches the given game on <see cref="SelectedDevice"/>. */
        public IRelayCommand<LumoplayGame> LaunchGameCommand { get; }

        /**
         * Starts the first catalogue load and pre-selects the first device, so
         * the page is usable as soon as it appears.
         *
         * <param name="deviceManager">shared device state and playback control</param>
         * <param name="apiClient">client for the controller backend</param>
         */
        public GameLibraryViewModel(DeviceManager deviceManager, LumoPlayApiClient apiClient)
        {
            _deviceManager = deviceManager;
            _apiClient = apiClient;
            Games = new ObservableCollection<LumoplayGame>();
            LoadGamesCommand.Execute(null);
            SelectedDevice = Devices.FirstOrDefault();
            LaunchGameCommand = new RelayCommand<LumoplayGame>(OnLaunchGame, CanLaunchGame);
        }

        /**
         * Gates <see cref="LaunchGameCommand"/> on having both a target device
         * and a game.
         *
         * <param name="game">the game the command was invoked with</param>
         * <returns>true when the launch can proceed</returns>
         */
        private bool CanLaunchGame(LumoplayGame game)
        {
            return SelectedDevice != null && game != null;
        }

        /**
         * Handler for <see cref="LaunchGameCommand"/>; launches on the currently
         * selected device.
         *
         * <param name="game">the game to launch</param>
         */
        private async void OnLaunchGame(LumoplayGame game)
        {
            if (SelectedDevice != null && game != null)
            {
                await _deviceManager.PlayGameAsync(SelectedDevice.IpAddress, game);
            }
        }

        /**
         * Launches a game on an explicitly given device, for the page's modal
         * device picker. Does nothing if either argument is null.
         *
         * <param name="game">the game to launch</param>
         * <param name="device">the device to launch it on</param>
         */
        public async Task LaunchGameAsync(LumoplayGame game, LumoplayDevice device)
        {
            if (device != null && game != null)
            {
                await _deviceManager.PlayGameAsync(device.IpAddress, game);
            }
        }

        /**
         * Reloads the catalogue from the backend, replacing the current contents.
         * An empty result is surfaced through <see cref="HasError"/> just like a
         * failure, so the page shows a message rather than a blank list.
         */
        [RelayCommand]
        public async Task LoadGames()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                HasError = false;
                ErrorMessage = string.Empty;

                // 1. Call the Backend API
                var gameList = await _apiClient.GetAllGamesAsync();

                // 2. Clear and Reload the List
                // We clear first to ensure we don't duplicate items if reload is clicked
                Games.Clear();

                if (gameList.Count > 0)
                {
                    foreach (var game in gameList)
                    {
                        // Optional: Decode Base64 image if you need to do processing here,
                        // otherwise XAML Image Source can often handle Base64 streams with a converter.
                        Games.Add(game);
                    }
                }
                else
                {
                    // Optional: Handle empty state logic here
                    ErrorMessage = "No games found in the library.";
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load games: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /** Reloads the catalogue; bound to pull-to-refresh. */
        [RelayCommand]
        public async Task RefreshGames()
        {
            await LoadGames();
        }
    }
}
