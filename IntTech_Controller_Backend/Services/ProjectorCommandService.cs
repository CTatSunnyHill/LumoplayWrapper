using System.Net.Sockets;
using System.Text;
    

namespace IntTech_Controller_Backend.Services
{
    public class ProjectorCommandService
    {
        private readonly ILogger<ProjectorCommandService> _logger;
        public ProjectorCommandService(ILogger<ProjectorCommandService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> SetPowerState(string ipAddress, int port, bool turnOn)
        {
            string command = turnOn ? "%1POWR 1\r" : "%1POWR 0\r";

            return await SendRawCommand(ipAddress, port, command);
        }

        public async Task<string> GetPowerStatus(string ipAddress, int port)
        {
            //%1POWR ? returns status (0=off, 1=on, 2=cooling, 3=warming)
            string response = await SendRawCommandWithResponse(ipAddress, port, "%1POWR ?\r");

            if (response.Contains("=0")) return "off";
            if (response.Contains("=1")) return "on";
            if (response.Contains("=2")) return "cooling";
            if (response.Contains("=3")) return "warming";
            if (response.Contains("=ERR3")) return "transitioning";
            if (response.Contains("ERR")) return "error";


            return "offline";
        }

        /// Returns the list of PJLink input codes the projector reports as available.
        /// Empty list if the projector is offline, errors, or reports nothing.
        public async Task<List<string>> QueryAvailableInputs(string ipAddress, int port)
        {
            string response = await SendRawCommandWithResponse(ipAddress, port, "%1INST ?\r");

            // Expected: "%1INST=11 31 32". Guard against "offline"/"error"/"ERR" responses.
            if (string.IsNullOrWhiteSpace(response)) return new List<string>();
            if (response.Contains("ERR")) return new List<string>();

            int eq = response.IndexOf('=');
            if (eq < 0) return new List<string>();

            string payload = response.Substring(eq + 1).Trim();
            if (string.IsNullOrWhiteSpace(payload)) return new List<string>();

            return payload
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length == 2)   // PJLink codes are exactly 2 chars
                .ToList();
        }

        /// Returns the currently selected input code (e.g. "31"), or null if offline/error.
        public async Task<string?> GetCurrentInput(string ipAddress, int port)
        {
            string response = await SendRawCommandWithResponse(ipAddress, port, "%1INPT ?\r");

            if (string.IsNullOrWhiteSpace(response)) return null;
            if (response.Contains("ERR")) return null;

            int eq = response.IndexOf('=');
            if (eq < 0) return null;

            string code = response.Substring(eq + 1).Trim();
            return code.Length == 2 ? code : null;
        }

        /// Switches the projector to the given PJLink input code.
        /// Returns true only on an explicit OK; false on any ERR or failure.
public async Task<bool> SetInput(string ipAddress, int port, string code)
{
    if (string.IsNullOrWhiteSpace(code) || code.Length != 2 || !code.All(char.IsDigit))
        return false;

    string response = await SendRawCommandWithResponse(ipAddress, port, $"%1INPT {code}\r");

    if (string.IsNullOrWhiteSpace(response)) return false;

    int eq = response.IndexOf('=');
    if (eq < 0) return false;

    string payload = response[(eq + 1)..].Trim();
    if (payload.StartsWith("ERR", StringComparison.OrdinalIgnoreCase)) return false;

    return string.Equals(payload, "OK", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(payload, code, StringComparison.Ordinal);
}

        private async Task<bool> SendRawCommand(string ipAddress, int port, string command)
        {
            try
            {
                using TcpClient client = new TcpClient();
                var connectTask = client.ConnectAsync(ipAddress, port);
                if (await Task.WhenAny(connectTask, Task.Delay(2000)) != connectTask)
                {
                    _logger.LogWarning($"Connection to projector {ipAddress}:{port} timed out.");
                    return false;
                }

                using NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[1024];
                await stream.ReadAsync(buffer, 0, buffer.Length);

                byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                await stream.WriteAsync(commandBytes, 0, commandBytes.Length);

                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                _logger.LogInformation($"Sent command to projector {ipAddress}:{port}, received response: {response.Trim()}");

                return response.Contains("OK") || response.Contains("=");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending command to projector {ipAddress}:{port}: {ex.Message}");
                return false;
            }
        }

        private async Task<string> SendRawCommandWithResponse(string ipAddress, int port, string command)
        {
            try
            {
                using TcpClient client = new TcpClient();
                var connectTask = client.ConnectAsync(ipAddress, port);
                if (await Task.WhenAny(connectTask, Task.Delay(2000)) != connectTask)
                {
                    _logger.LogWarning($"Connection to projector {ipAddress}:{port} timed out.");
                    return "offline";
                }
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                await stream.ReadAsync(buffer, 0, buffer.Length);

                byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                await stream.WriteAsync(commandBytes, 0, commandBytes.Length);

                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                _logger.LogInformation($"Sent command to projector {ipAddress}:{port}, received response: {response.Trim()}");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending command to projector {ipAddress}:{port}: {ex.Message}");
                return "error";
            }
        }
    }
}
