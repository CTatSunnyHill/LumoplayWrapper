using CommunityToolkit.Mvvm.ComponentModel;
    using System;
using System.Collections.Generic;
using System.Text;

namespace LUMOplay_Remote_Controller.Model
{
    /**
     * A LUMOplay device with its connection details and current state.
     * Observable so the dashboard re-renders as polling updates the live fields;
     * the MVVM Toolkit generates the public properties from these fields.
     */
    public partial class LumoplayDevice : ObservableObject
    {
        /** Human-readable device name shown in the UI. */
        [ObservableProperty]
        private string name;

        /** Host or IP address of the LUMOplay service on the local network. */
        [ObservableProperty]
        private string ipAddress;

        /** Shared secret required by the LUMOplay API on this unit. */
        [ObservableProperty]
        private string securityKey;

        /** Path to the vendor scripting tool on the machine driving the device. */
        [ObservableProperty]
        private string exePath;

        /** Whether the device answered the last poll. */
        [ObservableProperty]
        private bool isConnected;

        /** Game on screen, or null when nothing is playing. */
        [ObservableProperty]
        private LumoplayGame? currentGame;

        /** Playlist currently running on this device, or null when none is active. */
        [ObservableProperty]
        private Playlist? playlist;

        /** Whether playback is running rather than stopped. */
        [ObservableProperty]
        private bool isPlaying;
    }
}
