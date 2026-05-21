using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Helpers;
using IntTech_Controller_Backend.Models;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlaybackController : ControllerBase
{
    private readonly IntTechDBContext _context;
    private readonly LumoCommandService _commandService;

    public PlaybackController(IntTechDBContext context, LumoCommandService commandService)
    {
        _context = context;
        _commandService = commandService;
    }

    // POST: api/Playback/play-game/{ipAddress}/game/{gameId}
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

        if (!User.IsInRole("Admin"))
        {
            if (game == null) return NotFound($"Game with ID '{gameId}' not found.");
            var allowedTagIds = ClaimsHelper.GetAllowedTagIds(User).ToHashSet();
            var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
            if (!GameAccessHelper.IsGameVisibleToUser(game, allowedTagIds, allTagsById))
                return Forbid();
        }

        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

        if (device == null)
        {
            return NotFound($"No device found with IP: {ipAddress}");
        }

        // Enforce location-based authorization
        if (!User.IsInRole("Admin"))
        {
            var allowedLocationIds = ClaimsHelper.GetAllowedLocationIds(User);
            if (!allowedLocationIds.Contains(device.LocationId))
            {
                return NotFound($"No device found with IP: {ipAddress}");
            }
        }

        var result = await _commandService.ExecuteCommand(
            device.IpAddress,
            device.SecurityKey,
            $"-g {gameId}"
        );

        // Check if command execution failed
        if (result == null)
        {
            return StatusCode(502, new { Status = "Failed", Message = "Device command timed out or failed" });
        }

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

    // POST: api/Playback/stop-game/{ipAddress}
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

        // Enforce location-based authorization
        if (!User.IsInRole("Admin"))
        {
            var allowedLocationIds = ClaimsHelper.GetAllowedLocationIds(User);
            if (!allowedLocationIds.Contains(device.LocationId))
            {
                return NotFound($"No device found with IP: {ipAddress}");
            }
        }

        var result = await _commandService.ExecuteCommand(
            device.IpAddress,
            device.SecurityKey,
            "-s" // -s is the Stop command
        );

        // Check if command execution failed
        if (result == null)
        {
            return StatusCode(502, new { Status = "Failed", Message = "Device command timed out or failed" });
        }

        device.Status = "online";
        device.IsPlaying = false;
        device.LastChecked = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { Status = "Stopped", Output = result });
    }

    // GET: api/Playback/now-playing/{ipAddress}
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

        // Enforce location-based authorization
        if (!User.IsInRole("Admin"))
        {
            var allowedLocationIds = ClaimsHelper.GetAllowedLocationIds(User);
            if (!allowedLocationIds.Contains(device.LocationId))
            {
                return NotFound($"No device found with IP: {ipAddress}");
            }
        }

        var result = await _commandService.ExecuteCommand(
            device.IpAddress,
            device.SecurityKey,
            "-N"
        );

        // Check if command execution failed
        if (result == null)
        {
            return StatusCode(502, new { Status = "Failed", Message = "Device command timed out or failed" });
        }

        device.Status = "online";
        device.LastChecked = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { Output = result });
    }

    // POST: api/Playback/play-playlist/{ipAddress}/{playlistId}
    [HttpPost("play-playlist/{ipAddress}/{playlistId}")]
    public async Task<IActionResult> PlayPlaylist(string ipAddress, string playlistId)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(playlistId))
            return BadRequest("IP Address and Playlist ID are required.");

        // 1. Fetch Device
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);
        if (device == null) return NotFound($"Device not found: {ipAddress}");

        // Enforce location-based authorization
        if (!User.IsInRole("Admin"))
        {
            var allowedLocationIds = ClaimsHelper.GetAllowedLocationIds(User);
            if (!allowedLocationIds.Contains(device.LocationId))
            {
                return NotFound($"Device not found: {ipAddress}");
            }
        }

        // 2. Fetch Playlist using ObjectId
        if (!ObjectId.TryParse(playlistId, out ObjectId oid))
            return BadRequest("Invalid Playlist ID format.");

        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

        if (playlist == null) return NotFound($"Playlist not found: {playlistId}");
        if (playlist.Games == null || !playlist.Games.Any()) return BadRequest("Playlist is empty.");

        // 3. Determine the first launchable (lumoplay) game in the playlist, skipping any non-lumo entries.
        var found = await FindLaunchableAsync(playlist, startIndex: 0, direction: +1);
        if (found == null)
        {
            return BadRequest(new
            {
                Status = "NoLaunchableGames",
                Message = "This playlist has no LumoPlay games to launch."
            });
        }

        var (firstIndex, firstPlaylistGame) = found.Value;
        var firstGameId = firstPlaylistGame.GameId;

        if (!User.IsInRole("Admin"))
        {
            var firstGame = await _context.Games.FirstOrDefaultAsync(g => g.GameId == firstGameId);
            if (firstGame == null) return NotFound();
            var allowedTagIds = ClaimsHelper.GetAllowedTagIds(User).ToHashSet();
            var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
            if (!GameAccessHelper.IsGameVisibleToUser(firstGame, allowedTagIds, allTagsById))
                return Forbid();
        }

        // 4. Send Command to Device
        var result = await _commandService.ExecuteCommand(
            device.IpAddress,
            device.SecurityKey,
            $"-g {firstGameId}"
        );

        // Check if command execution failed
        if (result == null)
        {
            return StatusCode(502, new { Status = "Failed", Message = "Device command timed out or failed" });
        }

        // 5. Update Database State
        device.Status = "online";
        device.LastChecked = DateTime.UtcNow;
        device.CurrentLumoGameId = firstGameId;
        device.IsPlaying = true;

        ActivePlaylistState devicePlaylist = new ActivePlaylistState();
        // Store the ObjectId directly
        devicePlaylist.PlaylistId = playlist.Id;
        devicePlaylist.CurrentIndex = firstIndex;
        devicePlaylist.StartedAt = DateTime.UtcNow;

        device.ActivePlaylist = devicePlaylist;

        await _context.SaveChangesAsync();

        return Ok(new { Status = "Playlist Started", FirstGame = firstGameId, Output = result });
    }

    // POST: api/Playback/playlist/next-game/{ipAddress}
    [HttpPost("playlist/next-game/{ipAddress}")]
    public async Task<IActionResult> PlaylistNext(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return BadRequest("IP Address is required.");

        // 1. Fetch the Device State
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

        if (device == null) return NotFound($"Device not found: {ipAddress}");

        // Enforce location-based authorization
        if (!User.IsInRole("Admin"))
        {
            var allowedLocationIds = ClaimsHelper.GetAllowedLocationIds(User);
            if (!allowedLocationIds.Contains(device.LocationId))
            {
                return NotFound($"Device not found: {ipAddress}");
            }
        }

        if (device.ActivePlaylist == null) return BadRequest("No active playlist on this device.");

        // 2. Fetch the Playlist Definition
        // Modified: Directly access the ObjectId property, check for null
        if (device.ActivePlaylist.PlaylistId == null)
            return BadRequest("Device has invalid active playlist ID.");

        var oid = device.ActivePlaylist.PlaylistId.Value;

        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

        if (playlist == null || playlist.Games == null || !playlist.Games.Any())
            return BadRequest("The active playlist data is missing or empty.");

        // 3. Find the next launchable (lumoplay) game, skipping any non-lumo entries.
        var found = await FindLaunchableAsync(
            playlist,
            startIndex: device.ActivePlaylist.CurrentIndex + 1,
            direction: +1);

        if (found == null)
        {
            return BadRequest(new
            {
                Status = "NoLaunchableGames",
                Message = "This playlist has no LumoPlay games to skip to."
            });
        }

        var (newIndex, nextGame) = found.Value;

        if (!User.IsInRole("Admin"))
        {
            var nextGameFull = await _context.Games.FirstOrDefaultAsync(g => g.GameId == nextGame.GameId);
            if (nextGameFull == null) return NotFound();
            var allowedTagIds = ClaimsHelper.GetAllowedTagIds(User).ToHashSet();
            var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
            if (!GameAccessHelper.IsGameVisibleToUser(nextGameFull, allowedTagIds, allTagsById))
                return Forbid();
        }

        // 4. Send Command
        var result = await _commandService.ExecuteCommand(
            device.IpAddress,
            device.SecurityKey,
            $"-g {nextGame.GameId}"
        );

        // Check if command execution failed
        if (result == null)
        {
            return StatusCode(502, new { Status = "Failed", Message = "Device command timed out or failed" });
        }

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

    // POST: api/Playback/playlist/previous-game/{ipAddress}
    [HttpPost("playlist/previous-game/{ipAddress}")]
    public async Task<IActionResult> PlaylistPrevious(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return BadRequest("IP Address is required.");

        // 1. Fetch the Device State
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

        if (device == null) return NotFound($"Device not found: {ipAddress}");

        // Enforce location-based authorization
        if (!User.IsInRole("Admin"))
        {
            var allowedLocationIds = ClaimsHelper.GetAllowedLocationIds(User);
            if (!allowedLocationIds.Contains(device.LocationId))
            {
                return NotFound($"Device not found: {ipAddress}");
            }
        }

        if (device.ActivePlaylist == null) return BadRequest("No active playlist on this device.");

        // 2. Fetch the Playlist Definition
        // Modified: Directly access the ObjectId property, check for null
        if (device.ActivePlaylist.PlaylistId == null)
            return BadRequest("Device has invalid active playlist ID.");

        var oid = device.ActivePlaylist.PlaylistId.Value;

        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

        if (playlist == null || playlist.Games == null || !playlist.Games.Any())
            return BadRequest("The active playlist data is missing or empty.");

        // 3. Find the previous launchable (lumoplay) game, skipping any non-lumo entries.
        var found = await FindLaunchableAsync(
            playlist,
            startIndex: device.ActivePlaylist.CurrentIndex - 1,
            direction: -1);

        if (found == null)
        {
            return BadRequest(new
            {
                Status = "NoLaunchableGames",
                Message = "This playlist has no LumoPlay games to skip to."
            });
        }

        var (newIndex, prevGame) = found.Value;

        if (!User.IsInRole("Admin"))
        {
            var prevGameFull = await _context.Games.FirstOrDefaultAsync(g => g.GameId == prevGame.GameId);
            if (prevGameFull == null) return NotFound();
            var allowedTagIds = ClaimsHelper.GetAllowedTagIds(User).ToHashSet();
            var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
            if (!GameAccessHelper.IsGameVisibleToUser(prevGameFull, allowedTagIds, allTagsById))
                return Forbid();
        }

        // 4. Send Command
        var result = await _commandService.ExecuteCommand(
            device.IpAddress,
            device.SecurityKey,
            $"-g {prevGame.GameId}"
        );

        // Check if command execution failed
        if (result == null)
        {
            return StatusCode(502, new { Status = "Failed", Message = "Device command timed out or failed" });
        }

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

    // Walks the playlist in the given direction from startIndex and returns the
    // first index whose game is platform == "lumoplay". Returns null if the playlist
    // contains no launchable games.
    //
    // direction == +1 for next, -1 for previous, 0 (treated as +1) for "first launchable from here".
    // startIndex may be negative or >= Count; it is normalized.
    private async Task<(int Index, PlaylistGame Game)?> FindLaunchableAsync(
        Playlist playlist, int startIndex, int direction)
    {
        if (playlist.Games == null || playlist.Games.Count == 0) return null;

        int step = direction < 0 ? -1 : 1;
        int count = playlist.Games.Count;

        var gameIds = playlist.Games.Select(g => g.GameId).Distinct().ToList();
        var fullGames = await _context.Games
            .Where(g => gameIds.Contains(g.GameId))
            .ToListAsync();
        var platformByGameId = fullGames.ToDictionary(
            g => g.GameId,
            g => g.Platform ?? PlatformTypes.LumoPlay);

        // Normalize startIndex into [0, count)
        int idx = ((startIndex % count) + count) % count;
        for (int i = 0; i < count; i++)
        {
            var candidate = playlist.Games[idx];
            if (platformByGameId.TryGetValue(candidate.GameId, out var platform)
                && platform == PlatformTypes.LumoPlay)
            {
                return (idx, candidate);
            }
            idx = ((idx + step) % count + count) % count;
        }

        return null;
    }
}
