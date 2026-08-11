using LUMOplay_Remote_Controller.Model;
using System.Diagnostics;
using System.Text.Json;

namespace IntTech_Controller_Backend.Services
{
    /**
     * Drives LUMOplay units by shelling out to the vendor's scripting tool.
     * Every call returns null on failure rather than throwing, so callers treat
     * a timeout, a protocol rejection, and a non-zero exit identically.
     */
    public class LumoCommandService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LumoCommandService> _logger;
        /** Serialises process spawning; concurrent launches proved unreliable on the host. */
        private static readonly SemaphoreSlim _spawnGate = new SemaphoreSlim(1, 1);

        /**
         * <param name="config">configuration supplying Lumo:ToolPath</param>
         * <param name="logger">logger for command traffic and failures</param>
         */
        public LumoCommandService(IConfiguration config, ILogger<LumoCommandService> logger)
        {
            _config = config;
            _logger = logger;
        }

        /**
         * Runs the scripting tool against one device and returns its stdout.
         * The process is killed if it has not exited within five seconds.
         *
         * <param name="targetIp">address of the LUMOplay unit</param>
         * <param name="targetSecurityKey">that unit's shared secret</param>
         * <param name="arguments">tool arguments to append, such as "-g 42"</param>
         * <returns>the tool's stdout on success; null on timeout, error output,
         * a non-zero exit, or a protocol rejection. A missing tool yields an
         * error string rather than null.</returns>
         */
        public async Task<String> ExecuteCommand(string targetIp, string targetSecurityKey, string arguments)
        {
            var exePath = _config["Lumo:ToolPath"];

            if (!File.Exists(exePath))
                return "Error: Scripting tool not found on Server";

            var fullArgs = $"-a {targetIp} -k \"{targetSecurityKey}\" {arguments}";

            _logger.LogInformation($"Sending to {targetIp}: {fullArgs}");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = fullArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Process process;
            Task<string> stdoutTask;
            Task<string> stderrTask;

            // Start under the gate, but read and wait outside it so one slow
            // device cannot block commands to the others.
            await _spawnGate.WaitAsync();
            try
            {
                process = new Process { StartInfo = psi };
                process.Start();
                stdoutTask = process.StandardOutput.ReadToEndAsync();
                stderrTask = process.StandardError.ReadToEndAsync();
            }
            finally
            {
                _spawnGate.Release();
            }

            var timeout = TimeSpan.FromSeconds(5);
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    try { await Task.WhenAll(stdoutTask, stderrTask); } catch { }
                    _logger.LogWarning("Command to {Ip} timed out after {Ms}ms", targetIp, (int)timeout.TotalMilliseconds);
                    return null;
                }


                string output = await stdoutTask;
                string err = await stderrTask;

                if (!string.IsNullOrWhiteSpace(err) || process.ExitCode != 0)
                {
                    _logger.LogWarning($"LUMO Error from {targetIp}: {err}");
                    return null; // Return NULL so Controller knows it failed
                }

                // The tool reports these on stdout with a zero exit code, so they
                // have to be caught by inspecting the text.
                if (output.Contains("Invalid packet", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("stale timestamp", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"LUMO Protocol Rejection from {targetIp}: {output.Trim()}");
                    return null; // Return NULL so the Controller knows it failed
                }

                // 4. SUCCESS
                // Return the output, if the process returns successfully.
                return output;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Command execution failed for {targetIp}: {ex.Message}");
                return null;
            }
            finally {
                process.Dispose();
            }
        }

        // --- PUBLIC HELPER METHODS ---

        /**
         * Starts a game on a device.
         *
         * <param name="ip">address of the LUMOplay unit</param>
         * <param name="key">that unit's shared secret</param>
         * <param name="gameId">vendor id of the game to launch</param>
         * <returns>true when the device accepted the command</returns>
         */
        public async Task<bool> PlayGameAsync(string ip, string key, string gameId)
        {
            var result = await ExecuteCommand(ip, key, $"-g {gameId}");
            return result != null;
        }

        /**
         * Stops whatever the device is currently playing.
         *
         * <param name="ip">address of the LUMOplay unit</param>
         * <param name="key">that unit's shared secret</param>
         * <returns>true when the device accepted the command</returns>
         */
        public async Task<bool> StopContentAsync(string ip, string key)
        {
            var result = await ExecuteCommand(ip, key, "-s");
            return result != null;
        }


        /**
         * Asks a device what it is currently playing.
         *
         * <param name="ip">address of the LUMOplay unit</param>
         * <param name="key">that unit's shared secret</param>
         * <returns>the device's reported state, or null when it is unreachable
         * or its reply could not be parsed</returns>
         */
        public async Task<LumoplayServiceResponse?> CurrentStatusAsync(string ip, string key)
        {
            var jsonOutput = await ExecuteCommand(ip, key, "-N");

            if (string.IsNullOrWhiteSpace(jsonOutput)) return null;

            try
            {
                return JsonSerializer.Deserialize<LumoplayServiceResponse>(jsonOutput);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to parse JSON from {ip}: {ex.Message}");
                return null;
            }
        }
    }
}
