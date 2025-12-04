using System.Diagnostics;

namespace IntTech_Controller_Backend.Services
{
    public class LumoCommandService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LumoCommandService> _logger;

        public LumoCommandService(IConfiguration config, ILogger<LumoCommandService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<String> ExecuteCommand (string targetIp, string targetSecurityKey, string arguments)
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

            using var process = new Process();

            process.StartInfo = psi;
            process.Start();

            string result = await process.StandardOutput.ReadToEndAsync();
            string err = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(err))
                _logger.LogError($"LUMO Error: {err}");

            return string.IsNullOrEmpty(result) ? "Command Sent" : result;
            

        }
    }
}
