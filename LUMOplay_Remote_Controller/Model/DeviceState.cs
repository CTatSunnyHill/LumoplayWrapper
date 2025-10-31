using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace LUMOplay_Remote_Controller.Model
{
    public partial class DeviceState : ObservableObject
    {
        [ObservableProperty]
        private LumoplayDevice device;

        [ObservableProperty]
        private LumoplayGame? currentGame;

        [ObservableProperty]
        private List<LumoplayGame>? playlist;

        [ObservableProperty]
        private bool isPlaying;

        [ObservableProperty]
        private bool isActive;
    }
}   
