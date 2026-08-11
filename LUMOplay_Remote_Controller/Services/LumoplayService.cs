using LUMOplay_Remote_Controller.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LUMOplay_Remote_Controller.Services
{
    /**
     * Service class for interacting with the LUMOplay motion gaming platform.
     * Provides methods to control game playback, navigation, and volume settings.
     *
     * Commands are issued by shelling out to the vendor's scripting tool on this
     * machine, which then talks to the device over the network — so the tool must
     * be installed locally at the device's configured ExePath. Each instance is
     * bound to one device and updates that device's IsConnected flag as a side
     * effect of every command.
     */
    public class LumoplayService
    {
        private readonly LumoplayDevice _device;

        /**
         * Initializes a new instance of the LumoplayService for a specific device.
         *
         * <param name="device">The LUMOplay device to control.</param>
         * <exception cref="ArgumentNullException">when device is null</exception>
         */
        public LumoplayService(LumoplayDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        /** The device this service is controlling. */
        public LumoplayDevice Device => _device;

        /**
         * Executes a LUMOplay command asynchronously through the Motion Player executable.
         * Success is judged by the exit code, and the device's IsConnected flag
         * is updated to match. Every failure path — missing executable, failed
         * start, or thrown exception — marks the device disconnected and returns
         * false rather than propagating.
         *
         * <param name="command">The command to execute with its parameters.</param>
         * <returns>True if the command was executed successfully; otherwise, false.</returns>
         */
        private async Task<bool> ExecuteCommandAsync(string command)
        {
            try
            {
                // Check if the executable exists before attempting to run
                if (!File.Exists(_device.ExePath))
                {
                    System.Diagnostics.Debug.WriteLine($"LUMOplay executable not found at: {_device.ExePath} for device {_device.Name}");
                    _device.IsConnected = false;
                    return false;
                }

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
                    _device.IsConnected = false;
                    return false;
                }

                // Create tasks to read both output and error streams
                // Started before waiting: a full pipe buffer would otherwise
                // block the child process from ever exiting.
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

        /**
         * Runs a command and returns its stdout, for commands whose output the
         * caller needs to parse. Unlike <see cref="ExecuteCommandAsync"/> this
         * ignores the exit code and leaves IsConnected untouched.
         *
         * <param name="command">the command to execute with its parameters</param>
         * <returns>the command's stdout, or null when it could not be run</returns>
         */
        private async Task<string?> ExecuteCommandAndGetOutputAsync(string command)
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

                bool started = process.Start();
                if (!started)
                    return null;

                var outputTask = process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                string output = await outputTask;

                return output;
            }
            catch
            {
                return null;
            }
        }

        /**
         * Checks the connection to the device by executing a simple command.
         *
         * <returns>True if the device is connected; otherwise, false.</returns>
         */
        public Task<bool> CheckConnectionAsync()
        {
            // Use the "-N" command as a lightweight way to check the connection.
            // It requests the current game/playlist status.
            return ExecuteCommandAsync("-N");
        }

        /**
         * Starts playing a specific game on the LUMOplay platform.
         *
         * <param name="game">The game to play.</param>
         * <returns>True if the game started successfully; otherwise, false.</returns>
         * <exception cref="ArgumentNullException">when game is null</exception>
         */
        public Task<bool> PlayGameAsync(LumoplayGame game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return ExecuteCommandAsync($"-g {game.GameId}");
        }



        /**
         * Pauses the currently playing content.
         *
         * NOTE: sends the same "-s" command as <see cref="StopContentAsync"/> —
         * the tool exposes no separate pause. The two differ only in how
         * DeviceManager updates local state afterwards.
         *
         * <returns>True if the pause command was successful; otherwise, false.</returns>
         */
        public Task<bool> PauseContentAsync()
        {
            return ExecuteCommandAsync("-s");
        }

        /**
         * Stops the currently playing content.
         *
         * <returns>True if the stop command was successful; otherwise, false.</returns>
         */
        public Task<bool> StopContentAsync()
        {
            return ExecuteCommandAsync("-s");
        }

        /**
         * Advances to the next content item in the playlist the device itself is
         * running, which is not the same as the playlist this app tracks.
         *
         * <returns>True if successfully moved to next content; otherwise, false.</returns>
         */
        public Task<bool> NextContentAsync()
        {
            return ExecuteCommandAsync("-n");
        }

        /**
         * Returns to the previous content item in the playlist the device itself
         * is running.
         *
         * <returns>True if successfully moved to previous content; otherwise, false.</returns>
         */
        public Task<bool> PreviousContentAsync()
        {
            return ExecuteCommandAsync("-p");
        }

        /**
         * Returns the current game and playlist information.
         *
         * <returns>the device's reported state, or null when it could not be
         * reached</returns>
         * <exception cref="JsonException">when the device replies with something
         * that is not the expected JSON</exception>
         */
        public async Task<LumoplayServiceResponse?> CurrentGamePlaylistAsync()
        {
            var output = await ExecuteCommandAndGetOutputAsync("-N");
            if (string.IsNullOrWhiteSpace(output))
                return null;

            return JsonSerializer.Deserialize<LumoplayServiceResponse>(output);
        }

    }
}
