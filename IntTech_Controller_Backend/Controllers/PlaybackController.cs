using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Helpers;
using IntTech_Controller_Backend.Models;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers;

/**
 * Drives what is on screen: launching games, stopping them, and stepping
 * through playlists.
 *
 * Two guards run on nearly every endpoint. The device must be in one of the
 * caller's allowed locations — answered as 404, not 403, so the response does
 * not confirm the address exists — and the game must be permitted by the
 * caller's tags. Only LUMOplay titles can be launched; VR and Switch entries
 * are catalogued but skipped over.
 *
 * The database records what this system last told a device to do. A clinician
 * driving the unit by hand will drift from it until the next poll in
 * DevicesController reconciles the two.
 */
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlaybackController : ControllerBase
{
    private readonly IntTechDBContext _context;
    private readonly LumoCommandService _commandService;

    /**
     * <param name="context">database context for devices, games, playlists, and tags</param>
     * <param name="commandService">service used to send commands to the LUMOplay units</param>
     */
    public PlaybackController(IntTechDBContext context, LumoCommandService commandService)
    {
        _context = context;
        _commandService = commandService;
    }

    /**
     * Launches a single game on a device. Doing so ends any playlist the device
     * was running, since the device is no longer following that sequence.
     *
     * <param name="ipAddress">address of the target device</param>
     * <param name="gameId">vendor id of the game to launch</param>
     * <returns>200 with the tool's output; 400 when a parameter is blank or the
     * game is not a LUMOplay title; 403 when the caller's tags do not permit the
     * game; 404 when the game or device is unknown or out of scope; 502 when the
     * device did not respond</returns>
     */
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
        // Nothing is persisted on failure, so stored state never claims a
        // launch the device did not accept.
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

    /**
     * Stops whatever a device is playing. The active playlist binding is left
     * in place, so a stop can be resumed with next/previous.
     *
     * <param name="ipAddress">address of the target device</param>
     * <returns>200 with the tool's output; 400 when the address is blank;
     * 404 when the device is unknown or out of scope; 502 when it did not
     * respond</returns>
     */
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

    /**
     * Asks a device directly what it is playing and returns the tool's raw
     * output, bypassing this system's stored state.
     *
     * <param name="ipAddress">address of the target device</param>
     * <returns>200 with the raw output; 400 when the address is blank; 404 when
     * the device is unknown or out of scope; 502 when it did not respond</returns>
     */
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

    /**
     * Starts a playlist on a device by launching its first LUMOplay entry and
     * binding the playlist to the device, so next/previous can walk it.
     *
     * NOTE: the playlist itself is not checked against
     * <see cref="PlaylistVisibility"/> here — any playlist id the caller knows
     * can be started. The tag check on the launched game still applies.
     *
     * <param name="ipAddress">address of the target device</param>
     * <param name="playlistId">string form of the playlist's ObjectId</param>
     * <returns>200 with the launched game; 400 when a parameter is blank or
     * malformed, or the playlist is empty or has no LUMOplay entries; 403 when
     * the caller's tags do not permit that first game; 404 when the device or
     * playlist is unknown or out of scope; 502 when the device did not
     * respond</returns>
     */
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

    /**
     * Advances a device to the next LUMOplay entry in its active playlist,
     * skipping non-launchable ones and wrapping past the end.
     *
     * <param name="ipAddress">address of the target device</param>
     * <returns>200 with the new game and index; 400 when the address is blank,
     * no playlist is active, or the playlist is gone, empty, or has nothing
     * launchable; 403 when the caller's tags do not permit the next game;
     * 404 when the device is unknown or out of scope; 502 when the device did
     * not respond</returns>
     */
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

    /**
     * Steps a device back to the previous LUMOplay entry in its active playlist,
     * the mirror of <see cref="PlaylistNext"/>.
     *
     * <param name="ipAddress">address of the target device</param>
     * <returns>200 with the new game and index; 400 when the address is blank,
     * no playlist is active, or the playlist is gone, empty, or has nothing
     * launchable; 403 when the caller's tags do not permit the previous game;
     * 404 when the device is unknown or out of scope; 502 when the device did
     * not respond</returns>
     */
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

    /**
     * Walks the playlist in the given direction from startIndex and returns the
     * first index whose game is platform == "lumoplay". Returns null if the playlist
     * contains no launchable games.
     *
     * The walk wraps around and visits each position at most once, so stepping
     * past either end of a playlist lands back at the other.
     *
     * <param name="playlist">the playlist to search</param>
     * <param name="startIndex">where to begin; may be negative or >= Count, and is normalized</param>
     * <param name="direction">+1 for next, -1 for previous, 0 (treated as +1) for "first launchable from here"</param>
     * <returns>the index and entry of the first launchable game, or null when
     * the playlist holds none</returns>
     */
    private async Task<(int Index, PlaylistGame Game)?> FindLaunchableAsync(
        Playlist playlist, int startIndex, int direction)
    {
        if (playlist.Games == null || playlist.Games.Count == 0) return null;

        int step = direction < 0 ? -1 : 1;
        int count = playlist.Games.Count;

        // One query for every platform up front, rather than a lookup per step.
        var gameIds = playlist.Games.Select(g => g.GameId).Distinct().ToList();
        var gamePlatforms = await _context.Games
            .Where(g => gameIds.Contains(g.GameId))
            .Select(g => new { g.GameId, g.Platform })
            .ToListAsync();
        var platformByGameId = gamePlatforms.ToDictionary(
            g => g.GameId,
            g => g.Platform ?? PlatformTypes.LumoPlay);

        // Normalize startIndex into [0, count)
        int idx = ((startIndex % count) + count) % count;
        for (int i = 0; i < count; i++)
        {
            // An entry missing from the library has no platform and is skipped.
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
