using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace LUMOplay_Remote_Controller.Model
{
    /// <summary>
    /// Static configuration class that maintains the collection of LUMOplay devices and games.
    /// </summary>
    public static class LumoplayConfig
    {
        /// <summary>
        /// Gets the collection of available LUMOplay devices.
        /// </summary>
        public static ReadOnlyCollection<LumoplayDevice> Devices { get; }

        /// <summary>
        /// Gets the collection of available LUMOplay games.
        /// </summary>
        public static ReadOnlyCollection<LumoplayGame> Games { get; }

        /// <summary>
        /// Static constructor to initialize the device and game collections.
        /// </summary>
        static LumoplayConfig()
        {
            // Initialize devices
            var devices = new List<LumoplayDevice>
            {
                 new LumoplayDevice
                {
                    Name = "TML",
                    IpAddress = "10.5.43.186",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,

                },
                new LumoplayDevice
                {
                    Name = "GYM Wall Right",
                    IpAddress = "10.5.43.118",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                    
                },
                new LumoplayDevice
                {
                    Name = "GYM Wall Left",
                    IpAddress = "10.5.43.106",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                    
                },
                new LumoplayDevice
                {
                    Name = "GYM Floor Left",
                    IpAddress = "10.5.43.109",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                    
                },
                new LumoplayDevice
                {
                    Name = "GYM Floor Right",
                    IpAddress = "10.5.43.121",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                  
                },
                new LumoplayDevice
                {
                    Name = "GYM Floor Garage",
                    IpAddress = "10.5.43.120",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                    
                },
                new LumoplayDevice
                {
                    Name = "Bioness Left 1",
                    IpAddress = "10.5.43.80",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                   
                },
                new LumoplayDevice
                {
                    Name = "Bioness Left 2",
                    IpAddress = "10.5.43.99",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                  
                },
                new LumoplayDevice
                {
                    Name = "Bioness Right 1",
                    IpAddress = "10.5.43.111",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                   
                },
                new LumoplayDevice
                {
                    Name = "Bioness Right 2",
                    IpAddress = "10.5.43.81",
                    SecurityKey = "idoneusdigital",
                    ExePath = @"C:\Program Files\LUMOplay\MotionPlayer.Scripting.exe",
                    IsConnected = false,
                    CurrentGame = null,
                    Playlist = null,
                    IsPlaying = false,
                   
                },
                // Add more devices as needed
            };

            // Initialize games
            var games = new List<LumoplayGame>
            {
                new LumoplayGame
                {
                    GameId = "11718",
                    Name = "Bunny Hero",
                    ImageUrl = "sample_game1.png",
                    Description = "Protect your colony of bunnies from the hungry foxes chasing them away on this interactive floor game."
                },
                new LumoplayGame
                {
                    GameId = "10780",
                    Name = "Ball Pit",
                    ImageUrl = "sample_game2.png",
                    Description = "Put a ball pit in any room and don't worry about cleaning up the mess. Kick these interactive balls around and they fall back."
                }
                // Add more games as needed
            };

            Devices = new ReadOnlyCollection<LumoplayDevice>(devices);
            Games = new ReadOnlyCollection<LumoplayGame>(games);
        }

        /// <summary>
        /// Gets a device by its name.
        /// </summary>
        /// <param name="name">The name of the device to find.</param>
        /// <returns>The matching device or null if not found.</returns>
        public static LumoplayDevice GetDeviceByName(string name)
        {
            return Devices.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets a game by its ID.
        /// </summary>
        /// <param name="gameId">The ID of the game to find.</param>
        /// <returns>The matching game or null if not found.</returns>
        public static LumoplayGame GetGameById(string gameId)
        {
            return Games.FirstOrDefault(g => g.GameId == gameId);
        }

        public static LumoplayGame GetGameById(int gameId)
        {
            return Games.FirstOrDefault(g => Convert.ToInt64(g.GameId) == gameId);
        }
    }
}
