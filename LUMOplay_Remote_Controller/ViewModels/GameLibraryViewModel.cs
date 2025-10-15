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
        public ObservableCollection<LumoplayGame> Games { get; }
        public ObservableCollection<LumoplayDevice> Devices { get; }

        [ObservableProperty]
        private LumoplayDevice selectedDevice;

        public IRelayCommand<LumoplayGame> LaunchGameCommand { get; }

        public GameLibraryViewModel()
        {
            Games = new ObservableCollection<LumoplayGame>(LumoplayConfig.Games);
            Devices = new ObservableCollection<LumoplayDevice>(LumoplayConfig.Devices);
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
                System.Diagnostics.Debug.WriteLine($"Trying to Launch LumoPlay service using ViewModel on Device {nameof(SelectedDevice)}");
                var service = new LumoplayService(SelectedDevice);
                await service.PlayGameAsync(game);
            }
        }
    }
}