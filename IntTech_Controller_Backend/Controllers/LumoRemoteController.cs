using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using System.Diagnostics;
using System.Security.Claims;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LumoRemoteController : ControllerBase
    {
        private readonly IntTechDBContext _context;
        private readonly LumoCommandService _commandService;

        public LumoRemoteController(IntTechDBContext context, LumoCommandService commandService)
        {
            _context = context;
            _commandService = commandService;
        }

        // ==========================================
        // DEVICE ENDPOINTS
        // ==========================================

        // GET: api/LumoRemote/devices
        [HttpGet("devices")]
        public async Task<IActionResult> GetDevices()
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var locationsClaim = User.FindFirstValue("AllowedLocations");
            var allowedLocations = string.IsNullOrEmpty(locationsClaim) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(locationsClaim);

            var query = _context.Devices.AsQueryable();
            if (userRole.ToLower() != "Admin")
            {
                query = query.Where(d => allowedLocations.Contains(d.Location));
            }

            // 1. Get all devices from DB
            var devices = await query.ToListAsync();
            var allGames = await _context.Games.ToDictionaryAsync(g => g.GameId);

            // 2. Create a list of checking tasks (Launch them all in parallel)
            var pingTasks = devices.Select(async device =>
            {
                try
                {
                    // We send the "-N" command (Now Playing). 
                    var result = await _commandService.CurrentStatusAsync(
                        device.IpAddress,
                        device.SecurityKey
                    );

                    if (result != null)
                    {
                        device.Status = "online";
                        device.LastChecked = DateTime.UtcNow;

                        if (result.NowPlayingIndex.HasValue)
                        {
                            int nowPlayingIndex = result.NowPlayingIndex.Value;
                            int gameId = result.Scenes[nowPlayingIndex].Scene.ID;

                            device.IsPlaying = true;

                            if (device.CurrentLumoGameId != null)
                            {
                                // If the game has changed, update it
                                if (device.CurrentLumoGameId != gameId.ToString() && allGames.ContainsKey(gameId.ToString()))
                                {
                                    device.CurrentLumoGameId = allGames[gameId.ToString()].GameId;
                                    device.ActivePlaylist = null;
                                }
                            }
                            else
                            {
                                // No current game, just set it
                                if (allGames.ContainsKey(gameId.ToString()))
                                {
                                    device.CurrentLumoGameId = allGames[gameId.ToString()].GameId;
                                }
                            }
                        }
                    }
                    else
                    {
                        device.Status = "offline";
                    }
                }
                catch
                {
                    // If the TCP connection fails or times out
                    device.Status = "offline";
                }
            });

            // 3. Wait for ALL pings to finish
            await Task.WhenAll(pingTasks);

            // 4. Save the new statuses to the database
            await _context.SaveChangesAsync();

            return Ok(devices);
        }

        // GET: api/LumoRemote/devices/{ipAddress}
        [HttpGet("devices/{ipAddress}")]
        public async Task<IActionResult> GetDevicesByIpAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return BadRequest("IP Address is required");

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);
            var allGames = await _context.Games.ToDictionaryAsync(g => g.GameId);

            if (device == null) return NotFound($"No device found with IP Address: '{ipAddress}'");

            try
            {
                var result = await _commandService.CurrentStatusAsync(
                    device.IpAddress,
                    device.SecurityKey
                );

                if (result != null)
                {
                    device.Status = "online";
                    device.LastChecked = DateTime.UtcNow;

                    if (result.NowPlayingIndex.HasValue)
                    {
                        int nowPlayingIndex = result.NowPlayingIndex.Value;
                        int gameId = result.Scenes[nowPlayingIndex].Scene.ID;

                        device.IsPlaying = true;

                        if (device.CurrentLumoGameId != null)
                        {
                            if (device.CurrentLumoGameId != gameId.ToString() && allGames.ContainsKey(gameId.ToString()))
                            {
                                device.CurrentLumoGameId = allGames[gameId.ToString()].GameId;
                                device.ActivePlaylist = null;
                            }
                        }
                        else
                        {
                            if (allGames.ContainsKey(gameId.ToString()))
                            {
                                device.CurrentLumoGameId = allGames[gameId.ToString()].GameId;
                            }
                        }
                    }
                }
                else
                {
                    device.Status = "offline";
                }
            }
            catch
            {
                device.Status = "offline";
            }

            return Ok(device);
        }

        // POST: api/LumoRemote/devices
        [HttpPost("devices")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddDevice([FromBody] Device device)
        {
            if (string.IsNullOrWhiteSpace(device.IpAddress))
                return BadRequest("IP Address is required.");

            var existingDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == device.IpAddress);

            if (existingDevice != null)
            {
                return Conflict($"A device with IP {device.IpAddress} already exists.");
            }

            device.Id = ObjectId.GenerateNewId();
            device.Status = "offline";
            device.LastChecked = DateTime.UtcNow;
            device.CurrentLumoGameId = null;
            device.ActivePlaylist = null;
            device.IsPlaying = false;

            _context.Devices.Add(device);
            await _context.SaveChangesAsync();

            return Ok(device);
        }

        // DELETE: api/LumoRemote/devices
        [HttpDelete("devices")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveDevice([FromQuery] string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return BadRequest("IP Address is required.");

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

            if (device == null)
            {
                return NotFound($"No device found with IP {ipAddress}");
            }

            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();

            return Ok($"Device '{device.Name}' was removed.");
        }


        // ==========================================
        // GAME ENDPOINTS
        // ==========================================

        // GET: api/LumoRemote/lumoGames
        [HttpGet("lumoGames")]
        public async Task<IActionResult> GetLumoGames()
        {
            var lumoGames = await _context.Games.ToListAsync();
            return Ok(lumoGames);
        }

        // GET: api/LumoRemote/lumoGames/{gameId}
        [HttpGet("lumoGames/{gameId}")]
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

        // POST: api/LumoRemote/lumoGames
        [HttpPost("lumoGames")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddGame([FromBody] LumoPlayGame game)
        {
            if (string.IsNullOrWhiteSpace(game.GameId))
                return BadRequest("Game ID is required.");

            var existingGame = await _context.Games
                .FirstOrDefaultAsync(g => g.GameId == game.GameId);

            if (existingGame != null)
            {
                return Conflict($"A game with ID {game.GameId} already exists.");
            }

            game.Id = ObjectId.GenerateNewId();
            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            return Ok(game);
        }

        // DELETE: api/LumoRemote/lumoGames/{gameId}
        [HttpDelete("lumoGames/{gameId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveGame(string gameId)
        {
            var game = await _context.Games
                .FirstOrDefaultAsync(g => g.GameId == gameId);

            if (game == null)
            {
                return NotFound($"Game with ID '{gameId}' not found.");
            }

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return Ok($"Game '{game.Name}' was removed from the library.");
        }


        // ==========================================
        // PLAYLIST ENDPOINTS (Updated for ObjectId)
        // ==========================================

        // GET: api/LumoRemote/lumoPlaylists
        [HttpGet("lumoPlaylists")]
        public async Task<IActionResult> GetLumoPlaylists()
        {
            var lumoPlaylists = await _context.Playlists.ToListAsync();

            var gameIdsToFetch = lumoPlaylists
             .SelectMany(playlist => playlist.Games)
             .Select(gameRef => gameRef.GameId)
             .Distinct()
             .ToList();

            var libraryGames = await _context.Games
                .Where(game => gameIdsToFetch.Contains(game.GameId))
                .ToListAsync();

            var response = lumoPlaylists.Select(p => new LumoPlayPlaylistDTO
            {
                Id = p.Id,
                Name = p.Name,
                Games = p.Games
                    .Select(pg => libraryGames.FirstOrDefault(lg => lg.GameId == pg.GameId))
                    .Where(g => g != null)
                    .ToList()!
            });

            return Ok(response);
        }

        // POST: api/LumoRemote/lumoPlaylist/add
        [HttpPost("lumoPlaylist/add")]
        public async Task<IActionResult> AddPlaylist([FromBody] LumoPlayPlaylist playlist)
        {
            if (string.IsNullOrEmpty(playlist.Name)) return BadRequest("Name is required");

            if (playlist.Games == null) playlist.Games = new List<LumoPlaylistGame>();

            
            playlist.Id = ObjectId.GenerateNewId();

            _context.Playlists.Add(playlist);
            await _context.SaveChangesAsync();

            return Ok(playlist);
        }

        // POST: api/LumoRemote/lumoPlaylist/remove
        [HttpPost("lumoPlaylist/remove")]
        public async Task<IActionResult> RemovePlaylist(string playlistId)
        {
            if (!ObjectId.TryParse(playlistId, out ObjectId oid))
                return BadRequest("Invalid Playlist ID format.");

            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

            if (playlist == null) return NotFound($"No playlist with ID '{playlistId}'");

            _context.Playlists.Remove(playlist);
            await _context.SaveChangesAsync();

            return Ok($"Playlist '{playlist.Name}' has been removed.");
        }

        // POST: api/LumoRemote/lumoPlaylists/{playlistId}/add-game-to-playlist/{gameId}
        [HttpPost("lumoPlaylists/{playlistId}/add-game-to-playlist/{gameId}")]
        public async Task<IActionResult> AddGameToPlaylistById(string playlistId, string gameId)
        {
            if (!ObjectId.TryParse(playlistId, out ObjectId oid))
                return BadRequest("Invalid Playlist ID format.");

            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
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

        // POST: api/LumoRemote/lumoPlaylists/{playlistId}/remove-game-from-playlist/{gameId}
        [HttpPost("lumoPlaylists/{playlistId}/remove-game-from-playlist/{gameId}")]
        public async Task<IActionResult> RemoveGameFromPlaylistById(string playlistId, string gameId)
        {
            if (!ObjectId.TryParse(playlistId, out ObjectId oid))
                return BadRequest("Invalid Playlist ID format.");

            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

            if (playlist == null) return NotFound("Playlist ID not found");

            var gameToRemove = playlist.Games.FirstOrDefault(g => g.GameId == gameId);

            if (gameToRemove == null) return NotFound($"Game `{gameId}` is not in the playlist.");

            playlist.Games.Remove(gameToRemove);

            await _context.SaveChangesAsync();

            return Ok($"Removed game '{gameToRemove.Name}' from Playlist '{playlist.Name}'");
        }


        // ==========================================
        // COMMAND ENDPOINTS
        // ==========================================

        // POST: api/LumoRemote/play-game/{ipAddress}/game/{gameID}
        [HttpPost("play-game/{ipAddress}/game/{gameId}")]
        public async Task<IActionResult> PlayGame(string ipAddress, string gameId)
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

            device.Status = "online";
            device.LastChecked = DateTime.UtcNow;

            // SMART LOGIC: If we are switching games, clear the playlist.
            if (device.CurrentLumoGameId != gameId)
            {
                device.ActivePlaylist = null;
            }

            device.CurrentLumoGameId = gameId;
            device.IsPlaying = true;

            await _context.SaveChangesAsync();

            return Ok(new { Status = "Sent", Output = result });
        }

        // POST: api/LumoRemote/stop-game/{ipAddress}
        [HttpPost("stop-game/{ipAddress}")]
        public async Task<IActionResult> StopGame(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return BadRequest("IP Address is required.");

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

            if (device == null)
            {
                return NotFound($"No device found with IP: {ipAddress}");
            }

            var result = await _commandService.ExecuteCommand(
                device.IpAddress,
                device.SecurityKey,
                "-s" // -s is the Stop command
            );

            device.Status = "online";
            device.IsPlaying = false;
            device.LastChecked = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Status = "Stopped", Output = result });
        }

        // GET: api/LumoRemote/now-playing/{ipAddress}
        [HttpGet("now-playing/{ipAddress}")]
        public async Task<IActionResult> GetNowPlaying(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return BadRequest("IP Address is required.");

            var device = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

            if (device == null)
            {
                return NotFound($"No device found with IP: {ipAddress}");
            }

            var result = await _commandService.ExecuteCommand(
                device.IpAddress,
                device.SecurityKey,
                "-N"
            );

            device.Status = "online";
            device.LastChecked = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Output = result });
        }

        // POST: api/LumoRemote/play-playlist
        [HttpPost("play-playlist/{ipAddress}/{playlistId}")]
        public async Task<IActionResult> PlayPlaylist(string ipAddress, string playlistId)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(playlistId))
                return BadRequest("IP Address and Playlist ID are required.");

            // 1. Fetch Device
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);
            if (device == null) return NotFound($"Device not found: {ipAddress}");

            // 2. Fetch Playlist using ObjectId
            if (!ObjectId.TryParse(playlistId, out ObjectId oid))
                return BadRequest("Invalid Playlist ID format.");

            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

            if (playlist == null) return NotFound($"Playlist not found: {playlistId}");
            if (playlist.Games == null || !playlist.Games.Any()) return BadRequest("Playlist is empty.");

            // 3. Determine the first game to play
            var firstGameId = playlist.Games.First().GameId;

            // 4. Send Command to Device
            var result = await _commandService.ExecuteCommand(
                device.IpAddress,
                device.SecurityKey,
                $"-g {firstGameId}"
            );

            // 5. Update Database State
            device.Status = "online";
            device.LastChecked = DateTime.UtcNow;
            device.CurrentLumoGameId = firstGameId;
            device.IsPlaying = true;

            ActivePlaylistState devicePlaylist = new ActivePlaylistState();
            // Store the ObjectId directly
            devicePlaylist.PlaylistId = playlist.Id;
            devicePlaylist.CurrentIndex = 0;
            devicePlaylist.StartedAt = DateTime.UtcNow;

            device.ActivePlaylist = devicePlaylist;

            await _context.SaveChangesAsync();

            return Ok(new { Status = "Playlist Started", FirstGame = firstGameId, Output = result });
        }

        // POST: api/LumoRemote/playlist/next-game/{ipAddress}
        [HttpPost("playlist/next-game/{ipAddress}")]
        public async Task<IActionResult> PlaylistNext(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return BadRequest("IP Address is required.");

            // 1. Fetch the Device State
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

            if (device == null) return NotFound($"Device not found: {ipAddress}");
            if (device.ActivePlaylist == null) return BadRequest("No active playlist on this device.");

            // 2. Fetch the Playlist Definition
            // Modified: Directly access the ObjectId property, check for null
            if (device.ActivePlaylist.PlaylistId == null)
                return BadRequest("Device has invalid active playlist ID.");

            var oid = device.ActivePlaylist.PlaylistId.Value;

            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

            if (playlist == null || playlist.Games == null || !playlist.Games.Any())
                return BadRequest("The active playlist data is missing or empty.");

            // 3. Calculate New Index
            int newIndex = device.ActivePlaylist.CurrentIndex + 1;
            if (newIndex >= playlist.Games.Count)
            {
                newIndex = 0;
            }

            var nextGame = playlist.Games[newIndex];

            // 4. Send Command
            var result = await _commandService.ExecuteCommand(
                device.IpAddress,
                device.SecurityKey,
                $"-g {nextGame.GameId}"
            );

            // 5. Update Database
            device.ActivePlaylist.CurrentIndex = newIndex;
            device.CurrentLumoGameId = nextGame.GameId;
            device.IsPlaying = true;
            device.Status = "online";
            device.LastChecked = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Switched to next game",
                NewGame = nextGame.Name,
                Index = newIndex,
                gameId = nextGame.GameId // Important for optimistic UI updates
            });
        }

        // POST: api/LumoRemote/playlist/previous-game/{ipAddress}
        [HttpPost("playlist/previous-game/{ipAddress}")]
        public async Task<IActionResult> PlaylistPrevious(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return BadRequest("IP Address is required.");

            // 1. Fetch the Device State
            var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

            if (device == null) return NotFound($"Device not found: {ipAddress}");
            if (device.ActivePlaylist == null) return BadRequest("No active playlist on this device.");

            // 2. Fetch the Playlist Definition
            // Modified: Directly access the ObjectId property, check for null
            if (device.ActivePlaylist.PlaylistId == null)
                return BadRequest("Device has invalid active playlist ID.");

            var oid = device.ActivePlaylist.PlaylistId.Value;

            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

            if (playlist == null || playlist.Games == null || !playlist.Games.Any())
                return BadRequest("The active playlist data is missing or empty.");

            // 3. Calculate New Index
            int newIndex = device.ActivePlaylist.CurrentIndex - 1;
            if (newIndex < 0)
            {
                newIndex = playlist.Games.Count - 1;
            }

            var prevGame = playlist.Games[newIndex];

            // 4. Send Command
            var result = await _commandService.ExecuteCommand(
                device.IpAddress,
                device.SecurityKey,
                $"-g {prevGame.GameId}"
            );

            // 5. Update Database
            device.ActivePlaylist.CurrentIndex = newIndex;
            device.CurrentLumoGameId = prevGame.GameId;
            device.IsPlaying = true;
            device.Status = "online";
            device.LastChecked = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Switched to previous game",
                NewGame = prevGame.Name,
                Index = newIndex,
                gameId = prevGame.GameId
            });
        }
    }
}