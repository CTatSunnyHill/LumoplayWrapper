using LUMOplay_Remote_Controller.Model;
using LUMOplay_Remote_Controller.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace LUMOplay_Remote_Controller.ViewModels
{
    public partial class GameLibraryViewModel : ObservableObject
    {
        private readonly DeviceManager _deviceManager;
        public ObservableCollection<LumoplayGame> Games { get; }
        public ObservableCollection<LumoplayDevice> Devices => _deviceManager.Devices;

        [ObservableProperty]
        private LumoplayDevice selectedDevice;

        public IRelayCommand<LumoplayGame> LaunchGameCommand { get; }

        public GameLibraryViewModel(DeviceManager deviceManager)
        {
            _deviceManager = deviceManager;
            Games = new ObservableCollection<LumoplayGame>(LumoplayConfig.Games);
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
    }
}