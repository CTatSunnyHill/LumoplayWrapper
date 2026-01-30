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
            if (response.Contains("ERR")) return "error";

            return "offline";
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
