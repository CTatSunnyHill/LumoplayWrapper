using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LumoRemoteController: ControllerBase
    {
        private readonly IntTechDBContext _context;
        private readonly LumoCommandService _commandService;

        public LumoRemoteController(IntTechDBContext context, LumoCommandService commandService) 
        {
            _context = context;
            _commandService = commandService;

        }

        // Endpoints for the devices

        // GET: api/LumoRemote/devices
        [HttpGet("devices/get-devices")]
        public async Task<IActionResult> GetDevices()
        {
            var devices = await _context.Devices.ToListAsync();
            var threshold = DateTime.UtcNow.AddMinutes(-5);
            var needsSave = false;

            foreach (var device in devices)
            {
                if (device.IsConnected && device.LastSeen < threshold)
                {
                    device.IsConnected = false;
                    needsSave = true;
                }
            }

            if (needsSave)
            {
                await _context.SaveChangesAsync();
            }
            return Ok(devices);
        }

        [HttpGet("devices/get-device-by-ip/{ipAddress}")]
        public async Task<IActionResult> GetDevicesByIpAddress([FromQuery] string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return BadRequest("IP Address is required");

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

            if (device == null) return NotFound($"No device found with IP Address: '{ipAddress}'");

            return Ok(device);
        }

        // POST: api/LumoRemote/devices/add-device
        [HttpPost("devices/add-device")]
        public async Task<IActionResult> AddDevice([FromBody] Device device)
        {
            if (string.IsNullOrWhiteSpace(device.IpAddress))
                return BadRequest("IP Address is required.");

            // 1. Check for duplicates
            var existingDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == device.IpAddress);

            if (existingDevice != null)
            {
                return Conflict($"A device with IP {device.IpAddress} already exists.");
            }

            // 2. Set defaults
            device.Id = ObjectId.GenerateNewId();
            device.IsConnected = false; // Assume online when adding
            device.LastSeen = DateTime.UtcNow;
            device.CurrentGame = null;
            device.IsPlaying = false;

            // 3. Save to DB
            _context.Devices.Add(device);
            await _context.SaveChangesAsync();

            return Ok(device);
        }

        // DELETE: api/remote/devices/remove-device
        [HttpDelete("devices/remove-device")]
        public async Task<IActionResult> RemoveDevice([FromQuery] string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return BadRequest("IP Address is required.");

            // 1. Find the device
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

            if (device == null)
            {
                return NotFound($"No device found with IP {ipAddress}");
            }

            // 2. Remove it
            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();

            return Ok($"Device '{device.Name}' was removed.");
        }



        // Endpoints for Games


        // GET: api/LumoRemote/lumoGames
        [HttpGet("lumoGames/get-all-games")]
        public async Task<IActionResult> GetLumoGames()
        {
            var lumoGames = await _context.Games.ToListAsync();
         
            return Ok(lumoGames);
        }

        // GET: api/LumoRemote/lumoGames/get-game-by-id/1799
        [HttpGet("lumoGames/get-game-by-id/{gameId}")]
        public async Task<IActionResult> GetGameById(string gameId)
        {
            var game = await _context.Games
                .FirstOrDefaultAsync(g => g.GameId == gameId);

            if (game == null)
            {
                return NotFound($"Game with ID '{gameId}' not found.");
            }

            return Ok(game);
        }

        // POST: api/remote/games
        [HttpPost("lumoGames/add-game")]
        public async Task<IActionResult> AddGame([FromBody] LumoPlayGame game)
        {
            if (string.IsNullOrWhiteSpace(game.GameId))
                return BadRequest("Game ID is required.");

            // 1. Check for duplicates
            var existingGame = await _context.Games
                .FirstOrDefaultAsync(g => g.GameId == game.GameId);

            if (existingGame != null)
            {
                return Conflict($"A game with ID {game.GameId} already exists.");
            }

            // 2. Generate internal ID and Save
            game.Id = ObjectId.GenerateNewId();
            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            return Ok(game);
        }

        // DELETE: api/remote/games/17391
        [HttpDelete("lumoGames/remove-game/{gameId}")]
        public async Task<IActionResult> RemoveGame(string gameId)
        {
            // 1. Find the game
            var game = await _context.Games
                .FirstOrDefaultAsync(g => g.GameId == gameId);

            if (game == null)
            {
                return NotFound($"Game with ID '{gameId}' not found.");
            }

            // 2. Remove it
            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return Ok($"Game '{game.Name}'  was removed from the library.");
        }







        // Endpoints for playlists

        // GET: api/LumoRemote/lumoPlaylists/get-all-playlists
        [HttpGet("lumoPlaylists/get-all-playlists")]
        public async Task<IActionResult> GetLumoPlaylists()
        {
            var lumoPlaylists = await _context.Playlists.ToListAsync();

            var gameIdsToFetch = lumoPlaylists
             .SelectMany(playlist => playlist.Games)
             .Select(gameRef => gameRef.GameId)
             .Distinct()
             .ToList();

            // Step 3: Fetch the Games from the Library
            var libraryGames = await _context.Games
                .Where(game => gameIdsToFetch.Contains(game.GameId))
                .ToListAsync();

            // Step 4: Stitch the Data (Join Playlist Refs -> Full Game Objects)
            var response = lumoPlaylists.Select(p => new LumoPlayPlaylistDTO
            {
                Id = p.Id,
                PlaylistId = p.PlaylistId,
                Name = p.Name,
                Games = p.Games
                    .Select(pg =>
                    {
                        // Match String to String
                        return libraryGames.FirstOrDefault(lg => lg.GameId == pg.GameId);
                    })
                    .Where(g => g != null) // Remove games that weren't found in the library
                    .ToList()!
            });

            return Ok(response);
        }

        [HttpPost("lumoPlaylist/add-playlist")]
        public async Task<IActionResult> AddPlaylist([FromBody] LumoPlayPlaylist playlist)
        {
            _context.Playlists.Add(playlist);
            await _context.SaveChangesAsync();
            return Ok(playlist);
        }

        [HttpPost("lumoPlaylist/remove-playlist")]
        public async Task<IActionResult> RemovePlaylist(string playlistId)
        {
            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.PlaylistId == playlistId);
            if (playlist == null) return NotFound($"No playlist with playlistId '{playlistId}'");

            _context.Playlists.Remove(playlist);
            await _context.SaveChangesAsync();
            return Ok($"Playlist '{playlist.Name}' has been removed.");
        }

        [HttpPost("lumoPlaylists/{playlistId}/add-game-to-playlist/{gameId}")]
        public async Task<IActionResult> AddGameToPlaylistById(string playlistId, string gameId)
        {
            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.PlaylistId == playlistId);
            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);

            if (playlist == null) return NotFound("Playlist ID not found");
            if (game == null) return NotFound("Game ID not found");

            if (playlist.Games == null) playlist.Games = new List<LumoPlaylistGame>();

            if (!playlist.Games.Any(x => x.GameId == gameId))
            {
                playlist.Games.Add(new LumoPlaylistGame
                {
                    GameId = game.GameId,
                    Name = game.Name
                });

                await _context.SaveChangesAsync();
                return Ok($"Added `{game.Name}` to playlist `{playlist.Name}`");
            }

            return Ok("Game already exists in this playlist.");

        }

        [HttpPost("lumoPlaylists/{playlistId}/remove-game-from-playlist/{gameId}")]
        public async Task<IActionResult> RemoveGameFromPlaylistById(string playlistId, string gameId)
        {
            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.PlaylistId == playlistId);

            if (playlist == null) return NotFound("Playlist ID not found");

            var gameToRemove = playlist.Games.FirstOrDefault(g => g.GameId == gameId);

            if (gameToRemove == null) return NotFound($"Game `{gameId}` is not in the playlist.");

            playlist.Games.Remove(gameToRemove);

            await _context.SaveChangesAsync();

            return Ok($"Removed game '{gameToRemove.Name}' from Playlist '{playlist.Name}'");

        }


        // Command Endpoints

        // POST: api/LumoRemote/play-game
        [HttpPost("play-game")]
        public async Task<IActionResult> PlayGame([FromQuery]string ipAddress, [FromQuery]string gameId)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(gameId))
                return BadRequest("IP Address and Game ID are required.");

           
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

            if (device == null)
            {
                return NotFound($"No device found with IP: {ipAddress}");
            }

           
            var result = await _commandService.ExecuteCommand(
                device.IpAddress,
                device.SecurityKey,
                $"-g {gameId}"
            );

           
            device.IsConnected = true;
            device.LastSeen = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Status = "Sent", Output = result });
        }

        // POST: api/LumoRemote/stop-game
        [HttpPost("stop-game")]
        public async Task<IActionResult> StopGame([FromQuery] string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return BadRequest("IP Address is required.");

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == ip);

            if (device == null)
            {
                return NotFound($"No device found with IP: {ip}");
            }

            var result = await _commandService.ExecuteCommand(
                device.IpAddress,
                device.SecurityKey,
                "-s" // -s is the Stop command
            );

            device.IsConnected = true;
            device.LastSeen = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Status = "Stopped", Output = result });
        }

        // GET: api/LumoRemote/now-playing
        [HttpGet("now-playing")]
        public async Task<IActionResult> GetNowPlaying([FromQuery] string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return BadRequest("IP Address is required.");

           
            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == ip);

            if (device == null)
            {
                return NotFound($"No device found with IP: {ip}");
            }

            // 2. Send the "-N" command (Now Playing)
            var result = await _commandService.ExecuteCommand(
                device.IpAddress,
                device.SecurityKey,
                "-N"
            );

            // 3. Mark as Online since it responded
            device.IsConnected = true;
            device.LastSeen = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // The 'result' string will contain the name/ID of the current game
            return Ok(new
            {
                Output = result
            });
        }


    }
}
