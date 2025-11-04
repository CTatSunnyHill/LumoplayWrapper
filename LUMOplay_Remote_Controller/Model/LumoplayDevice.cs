using CommunityToolkit.Mvvm.ComponentModel;
    using System;
using System.Collections.Generic;
using System.Text;

namespace LUMOplay_Remote_Controller.Model
{
    /// <summary>
    /// Represents a LUMOplay device with its connection details and current state.
    /// </summary>
    public partial class LumoplayDevice : ObservableObject
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string ipAddress;

        [ObservableProperty]
        private string securityKey;

        [ObservableProperty]
        private string exePath;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private LumoplayGame? currentGame;

        [ObservableProperty]
        private List<LumoplayGame>? playlist;

        [ObservableProperty]
        private bool isPlaying;
    }
}
