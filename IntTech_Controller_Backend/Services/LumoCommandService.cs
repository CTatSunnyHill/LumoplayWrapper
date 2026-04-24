using LUMOplay_Remote_Controller.Model;
using System.Diagnostics;
using System.Text.Json;

namespace IntTech_Controller_Backend.Services
{
    public class LumoCommandService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LumoCommandService> _logger;
        private static readonly SemaphoreSlim _spawnGate = new SemaphoreSlim(1, 1);

        public LumoCommandService(IConfiguration config, ILogger<LumoCommandService> logger)
        {
            _config = config;
            _logger = logger;
        }

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

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    _logger.LogWarning("Command to {Ip} timed out after {Ms}ms", targetIp, 5000);
                    return null;
                }


                string output = await stdoutTask;
                string err = await stderrTask;

                if (!string.IsNullOrWhiteSpace(err) || process.ExitCode != 0)
                {
                    _logger.LogWarning($"LUMO Error from {targetIp}: {err}");
                    return null; // Return NULL so Controller knows it failed
                }

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

        public async Task<bool> PlayGameAsync(string ip, string key, string gameId)
        {
            var result = await ExecuteCommand(ip, key, $"-g {gameId}");
            return result != null;
        }

        public async Task<bool> StopContentAsync(string ip, string key)
        {
            var result = await ExecuteCommand(ip, key, "-s");
            return result != null;
        }


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
