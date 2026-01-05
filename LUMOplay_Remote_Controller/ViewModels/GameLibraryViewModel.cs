using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace LUMOplay_Remote_Controller.ViewModels
{
    public partial class GameLibraryViewModel : ObservableObject
    {
        private readonly DeviceManager _deviceManager;

        private readonly LumoPlayApiClient _apiClient;
        public ObservableCollection<LumoplayGame> Games { get; }
        public ObservableCollection<LumoplayDevice> Devices => _deviceManager.Devices;

        [ObservableProperty]
        private LumoplayDevice selectedDevice;

        [ObservableProperty]
        private bool isBusy;

        // Shows an error message if something fails
        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private bool hasError;

        public IRelayCommand<LumoplayGame> LaunchGameCommand { get; }

        public GameLibraryViewModel(DeviceManager deviceManager, LumoPlayApiClient apiClient)
        {
            _deviceManager = deviceManager;
            _apiClient = apiClient;
            Games = new ObservableCollection<LumoplayGame>();
            LoadGamesCommand.Execute(null);
            SelectedDevice = Devices.FirstOrDefault();
            LaunchGameCommand = new RelayCommand<LumoplayGame>(OnLaunchGame, CanLaunchGame);
        }

        private bool CanLaunchGame(LumoplayGame game)
        {
            return SelectedDevice != null && game != null;
        }

        private async void OnLaunchGame(LumoplayGame game)
        {
            if (SelectedDevice != null && game != null)
            {
                await _deviceManager.PlayGameAsync(SelectedDevice.IpAddress, game);
            }
        }

        public async Task LaunchGameAsync(LumoplayGame game, LumoplayDevice device)
        {
            if (device != null && game != null)
            {
                await _deviceManager.PlayGameAsync(device.IpAddress, game);
            }
        }

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

        // Optional: Command to refresh the list (e.g., Pull-to-Refresh)
        [RelayCommand]
        public async Task RefreshGames()
        {
            await LoadGames();
        }
    }
}