using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace LUMOplay_Remote_Controller.Model
{
    /**
     * Static configuration class that maintains the collection of LUMOplay devices and games.
     *
     * This is the app's built-in fallback catalogue, hard-coded against one
     * specific site: the device list below is baked into the source, so adding
     * or moving a unit means a rebuild. The controller backend is the
     * authoritative source of devices and games; this exists for running the app
     * standalone against known hardware.
     */
    public static class LumoplayConfig
    {
        /** The hard-coded LUMOplay devices, in the order they are listed below. */
        public static ReadOnlyCollection<LumoplayDevice> Devices { get; }

        /** Games loaded from the bundled games.json; empty if that file could not be read. */
        public static ReadOnlyCollection<LumoplayGame> Games { get; }

        /** The built-in playlists, currently a single one holding every game. */
        public static ReadOnlyCollection<Playlist> Playlists { get; }

        /**
         * Builds the device, game, and playlist collections once, on first use.
         * A failure to read games.json is logged and swallowed, leaving the
         * catalogue empty rather than preventing the app from starting.
         */
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
            var games = new List<LumoplayGame>();
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("games.json").GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                games = JsonSerializer.Deserialize<List<LumoplayGame>>(json);
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., file not found, deserialization error)
                Console.WriteLine($"Error loading games: {ex.Message}");
            }

            Devices = new ReadOnlyCollection<LumoplayDevice>(devices);
            Games = new ReadOnlyCollection<LumoplayGame>(games);

            // Initialize playlists
            var playlists = new List<Playlist>
            {
                new Playlist
                {
                    Name = "TML",
                    Games = games
                },
            };
            Playlists = new ReadOnlyCollection<Playlist>(playlists);
        }

        /**
         * Finds a device by name, ignoring case.
         *
         * <param name="name">the device name to look for</param>
         * <returns>the matching device, or null when there is none</returns>
         */
        public static LumoplayDevice GetDeviceByName(string name)
        {
            return Devices.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /**
         * Finds a game by its id, compared as a string.
         *
         * <param name="gameId">the game id to look for</param>
         * <returns>the matching game, or null when there is none</returns>
         */
        public static LumoplayGame GetGameById(string gameId)
        {
            return Games.FirstOrDefault(g => g.GameId == gameId);
        }

        /**
         * Finds a game by its numeric id, for callers holding the vendor's
         * integer scene id rather than the string form.
         *
         * <param name="gameId">the numeric game id to look for</param>
         * <returns>the matching game, or null when there is none</returns>
         * <exception cref="FormatException">when any catalogued game has a
         * non-numeric id, since every id is parsed during the scan</exception>
         */
        public static LumoplayGame GetGameById(int gameId)
        {
            return Games.FirstOrDefault(g => Convert.ToInt64(g.GameId) == gameId);
        }
    }
}
