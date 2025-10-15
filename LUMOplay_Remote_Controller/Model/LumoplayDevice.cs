using System;
using System.Collections.Generic;
using System.Text;

namespace LUMOplay_Remote_Controller.Model
{
    /// <summary>
    /// Represents a LUMOplay device with its connection details.
    /// </summary>
    public class LumoplayDevice
    {
        /// <summary>
        /// Gets or sets the friendly name of the device.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the IP address of the device.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// Gets or sets the security key for authentication.
        /// </summary>
        public string SecurityKey { get; set; }

        /// <summary>
        /// Gets or sets the path to the LUMOplay executable on this device.
        /// </summary>
        public string ExePath { get; set; }

        /// <summary>
        /// Gets or sets whether the device is currently connected.
        /// </summary>
        public bool IsConnected { get; set; }
    }
}
