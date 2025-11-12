using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace LUMOplay_Remote_Controller.Model
{
    public partial class Playlist : ObservableObject
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private List<LumoplayGame> games;
    }
}