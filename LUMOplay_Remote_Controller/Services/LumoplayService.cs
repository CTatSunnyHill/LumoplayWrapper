using LUMOplay_Remote_Controller.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LUMOplay_Remote_Controller.Services
{
    /// <summary>
    /// Service class for interacting with the LUMOplay motion gaming platform.
    /// Provides methods to control game playback, navigation, and volume settings.
    /// </summary>
    public class LumoplayService
    {
        private readonly LumoplayDevice _device;

        /// <summary>
        /// Initializes a new instance of the LumoplayService for a specific device.
        /// </summary>
        /// <param name="device">The LUMOplay device to control.</param>
        public LumoplayService(LumoplayDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        /// <summary>
        /// Gets the device this service is controlling.
        /// </summary>
        public LumoplayDevice Device => _device;

        /// <summary>
        /// Executes a LUMOplay command asynchronously through the Motion Player executable.
        /// </summary>
        /// <param name="command">The command to execute with its parameters.</param>
        /// <returns>True if the command was executed successfully; otherwise, false.</returns>
        private async Task<bool> ExecuteCommandAsync(string command)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = _device.ExePath,
                    Arguments = $"-a {_device.IpAddress} -k \"{_device.SecurityKey}\" {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Log the full command being executed
                System.Diagnostics.Debug.WriteLine($"Executing LUMOplay command on {_device.Name}: {_device.ExePath} {process.StartInfo.Arguments}");

                bool started = process.Start();
                if (!started)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start the process on device {_device.Name}");
                    return false;
                }

                // Create tasks to read both output and error streams
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Wait for the process to exit and capture all output
                await process.WaitForExitAsync();
                string output = await outputTask;
                string error = await errorTask;

                // Log all relevant information
                System.Diagnostics.Debug.WriteLine($"Command completed on {_device.Name} with exit code: {process.ExitCode}");
                if (!string.IsNullOrEmpty(output))
                {
                    System.Diagnostics.Debug.WriteLine($"Command output from {_device.Name}: {output}");
                }
                if (!string.IsNullOrEmpty(error))
                {
                    System.Diagnostics.Debug.WriteLine($"Command error from {_device.Name}: {error}");
                }

                // Check if the executable exists
                if (!File.Exists(_device.ExePath))
                {
                    System.Diagnostics.Debug.WriteLine($"LUMOplay executable not found at: {_device.ExePath} for device {_device.Name}");
                    return false;
                }

                _device.IsConnected = process.ExitCode == 0;
                return _device.IsConnected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing command on {_device.Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                _device.IsConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Starts playing a specific game on the LUMOplay platform.
        /// </summary>
        /// <param name="game">The game to play.</param>
        /// <returns>True if the game started successfully; otherwise, false.</returns>
        public Task<bool> PlayGameAsync(LumoplayGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return ExecuteCommandAsync($"-g {game.GameId}");
        }

  

        /// <summary>
        /// Pauses the currently playing content.
        /// </summary>
        /// <returns>True if the pause command was successful; otherwise, false.</returns>
        public Task<bool> PauseContentAsync()
        {
            return ExecuteCommandAsync("-pause");
        }

        /// <summary>
        /// Stops the currently playing content.
        /// </summary>
        /// <returns>True if the stop command was successful; otherwise, false.</returns>
        public Task<bool> StopContentAsync()
        {
            return ExecuteCommandAsync("-s");
        }

        /// <summary>
        /// Advances to the next content item in the playlist.
        /// </summary>
        /// <returns>True if successfully moved to next content; otherwise, false.</returns>
        public Task<bool> NextContentAsync()
        {
            return ExecuteCommandAsync("-next");
        }

        /// <summary>
        /// Returns to the previous content item in the playlist.
        /// </summary>
        /// <returns>True if successfully moved to previous content; otherwise, false.</returns>
        public Task<bool> PreviousContentAsync()
        {
            return ExecuteCommandAsync("-previous");
        }

        
    }
}
