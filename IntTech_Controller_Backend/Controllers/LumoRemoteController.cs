using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using System.Diagnostics;

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
        // PLAYLIST ENDPOINTS (Updated for ObjectId)
        // ==========================================

        // GET: api/LumoRemote/playlists
        [HttpGet("playlists")]
        public async Task<IActionResult> GetPlaylists()
        {
            var playlists = await _context.Playlists.ToListAsync();

            var gameIdsToFetch = playlists
             .SelectMany(playlist => playlist.Games)
             .Select(gameRef => gameRef.GameId)
             .Distinct()
             .ToList();

            var libraryGames = await _context.Games
                .Where(game => gameIdsToFetch.Contains(game.GameId))
                .ToListAsync();

            var response = playlists.Select(p => new PlaylistDTO
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

        // POST: api/LumoRemote/playlists/add
        [HttpPost("playlists/add")]
        public async Task<IActionResult> AddPlaylist([FromBody] Playlist playlist)
        {
            if (string.IsNullOrEmpty(playlist.Name)) return BadRequest("Name is required");

            if (playlist.Games == null) playlist.Games = new List<PlaylistGame>();

            
            playlist.Id = ObjectId.GenerateNewId();

            _context.Playlists.Add(playlist);
            await _context.SaveChangesAsync();

            return Ok(playlist);
        }

        // POST: api/LumoRemote/playlists/remove
        [HttpPost("playlists/remove/{playlistId}")]
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

        // POST: api/LumoRemote/playlists/{playlistId}/add-game-to-playlist/{gameId}
        [HttpPost("playlists/{playlistId}/add-game-to-playlist/{gameId}")]
        public async Task<IActionResult> AddGameToPlaylistById(string playlistId, string gameId)
        {
            if (!ObjectId.TryParse(playlistId, out ObjectId oid))
                return BadRequest("Invalid Playlist ID format.");

            var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);

            if (playlist == null) return NotFound("Playlist ID not found");
            if (game == null) return NotFound("Game ID not found");

            if ((game.Platform ?? PlatformTypes.LumoPlay) != PlatformTypes.LumoPlay)
            {
                return BadRequest(new { Message = "Only LUMOplay games can be added to playlists." });
            }


            if (playlist.Games == null) playlist.Games = new List<PlaylistGame>();

            if (!playlist.Games.Any(x => x.GameId == gameId))
            {
                playlist.Games.Add(new PlaylistGame
                {
                    GameId = game.GameId,
                    Name = game.Name
                });

                await _context.SaveChangesAsync();
                return Ok($"Added `{game.Name}` to playlist `{playlist.Name}`");
            }

            return Ok("Game already exists in this playlist.");
        }

        // POST: api/LumoRemote/playlists/{playlistId}/remove-game-from-playlist/{gameId}
        [HttpPost("playlists/{playlistId}/remove-game-from-playlist/{gameId}")]
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

            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game != null && (game.Platform ?? PlatformTypes.LumoPlay) != PlatformTypes.LumoPlay)
            {
                return BadRequest(new { Message = "Only LUMOplay games can be played on devices." });
            }

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