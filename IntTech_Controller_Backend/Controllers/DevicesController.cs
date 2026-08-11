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
 * Device inventory and live status. The read endpoints poll the units
 * themselves rather than trusting stored state, then write what they learn back
 * to the database. Non-admins are filtered on two axes: which devices they see
 * at all (by location) and how much of a device's state they are told (by tag
 * and playlist visibility).
 */
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IntTechDBContext _context;
    private readonly LumoCommandService _commandService;

    /**
     * <param name="context">database context for devices, games, tags, and playlists</param>
     * <param name="commandService">service used to poll the LUMOplay units</param>
     */
    public DevicesController(IntTechDBContext context, LumoCommandService commandService)
    {
        _context = context;
        _commandService = commandService;
    }

    /**
     * Lists the devices the caller may control, polling each one in parallel to
     * refresh its status and now-playing game before answering. A device that
     * does not respond is reported offline rather than failing the request.
     *
     * Non-admins have details redacted from the response — a game they are not
     * tagged for, or a playlist they cannot see, comes back null — while the
     * unredacted state is still what gets persisted.
     *
     * <returns>200 with the visible devices and their refreshed state</returns>
     */
    // GET: api/Devices
    [HttpGet]
    public async Task<IActionResult> GetDevices()
    {
        var userRole = ClaimsHelper.GetUserRole(User);
        var allowedLocationIds = ClaimsHelper.GetAllowedLocationIds(User);

        var query = _context.Devices.AsQueryable();
        if (userRole != "Admin")
        {
            query = query.Where(d => allowedLocationIds.Contains(d.LocationId));
        }

        // 1. Get all devices from DB
        var devices = await query.ToListAsync();
        var allGames = await _context.Games.ToDictionaryAsync(g => g.GameId);

        // 2. Create a list of checking task
        var pingTasks = devices.Select(async device =>
        {
            try
            {
                var result = await _commandService.CurrentStatusAsync(device.IpAddress, device.SecurityKey);

                if (result != null)
                {
                    device.Status = "online";
                    device.LastChecked = DateTime.UtcNow;

                    if (result.NowPlayingIndex.HasValue
                        && result.Scenes != null
                        && result.NowPlayingIndex.Value >= 0
                        && result.NowPlayingIndex.Value < result.Scenes.Count)
                    {
                        int nowPlayingIndex = result.NowPlayingIndex.Value;
                        int gameId = result.Scenes[nowPlayingIndex].Scene.ID;

                        device.IsPlaying = true;

                        if (device.CurrentLumoGameId != null)
                        {
                            // The game changed without us asking — someone drove the
                            // unit directly — so any playlist we were tracking is void.
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
        });

        await Task.WhenAll(pingTasks);
        await _context.SaveChangesAsync();
        // Tag data is only loaded when it will actually be used to redact.
        var allowedTagIds = userRole != "Admin"
            ? ClaimsHelper.GetAllowedTagIds(User).ToHashSet()
            : new HashSet<ObjectId>();
        var allTagsById = userRole != "Admin"
            ? await _context.Tags.ToDictionaryAsync(t => t.Id)
            : new Dictionary<ObjectId, Tag>();

        // Visibility filter: collect distinct bound playlist OIDs, check each once.
        var userId = ClaimsHelper.GetUserId(User);
        var boundOids = devices
            .Where(d => d.ActivePlaylist?.PlaylistId != null)
            .Select(d => d.ActivePlaylist!.PlaylistId!.Value)
            .Distinct()
            .ToList();

        var visibleOids = await PlaylistVisibility.ResolveVisiblePlaylistIds(
            boundOids,
            userId,
            _context.Playlists);

        var response = devices.Select(d =>
        {
            var currentGameId = d.CurrentLumoGameId;
            if (userRole != "Admin" && currentGameId != null)
            {
                if (!allGames.TryGetValue(currentGameId, out var currentGame) ||
                    !GameAccessHelper.IsGameVisibleToUser(currentGame, allowedTagIds, allTagsById))
                {
                    currentGameId = null;
                }
            }
            return new DeviceResponseDto
            {
                Id = d.Id,
                Name = d.Name,
                IpAddress = d.IpAddress,
                LocationId = d.LocationId,
                Status = d.Status,
                IsPlaying = d.IsPlaying,
                CurrentLumoGameId = currentGameId,
                ActivePlaylist = (d.ActivePlaylist?.PlaylistId != null
                    && visibleOids.Contains(d.ActivePlaylist.PlaylistId.Value))
                    ? d.ActivePlaylist
                    : null,
                LastChecked = d.LastChecked,
            };
        });

        return Ok(response);
    }

    /**
     * Fetches one device by address, polling it for fresh status. A device
     * outside the caller's locations answers 404 rather than 403, so the
     * response does not confirm that the address exists.
     *
     * <param name="ipAddress">address of the device to look up</param>
     * <returns>200 with the device's refreshed state; 400 when the address is
     * blank; 404 when no such device exists or it is out of scope</returns>
     */
    // GET: api/Devices/{ipAddress}
    [HttpGet("{ipAddress}")]
    public async Task<IActionResult> GetDevicesByIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) return BadRequest("IP Address is required");

        var device = await _context.Devices.FirstOrDefaultAsync(d => d.IpAddress == ipAddress);

        if (device == null) return NotFound($"No device found with IP Address: '{ipAddress}'");

        if (!User.IsInRole("Admin"))
        {
            var allowedLocationIds = ClaimsHelper.GetAllowedLocationIds(User);
            if (!allowedLocationIds.Contains(device.LocationId))
            {
                return NotFound($"No device found with IP Address: '{ipAddress}'");
            }
        }

        var allGames = await _context.Games.ToDictionaryAsync(g => g.GameId);

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
                        // The game changed without us asking — someone drove the
                        // unit directly — so any playlist we were tracking is void.
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

        var userId = ClaimsHelper.GetUserId(User);
        var visibleActivePlaylist = await PlaylistVisibility.ResolveVisibleActivePlaylist(
            device, userId, _context.Playlists);

        var currentGameId = device.CurrentLumoGameId;
        if (!User.IsInRole("Admin") && currentGameId != null)
        {
            var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
            var allowedTagIds = ClaimsHelper.GetAllowedTagIds(User).ToHashSet();
            if (!allGames.TryGetValue(currentGameId, out var currentGame) ||
                !GameAccessHelper.IsGameVisibleToUser(currentGame, allowedTagIds, allTagsById))
            {
                currentGameId = null;
            }
        }

        return Ok(new DeviceResponseDto
        {
            Id = device.Id,
            Name = device.Name,
            IpAddress = device.IpAddress,
            LocationId = device.LocationId,
            Status = device.Status,
            IsPlaying = device.IsPlaying,
            CurrentLumoGameId = currentGameId,
            ActivePlaylist = visibleActivePlaylist,
            LastChecked = device.LastChecked,
        });
    }

    /**
     * Registers a device. Addresses are unique. All live state on the posted
     * body is overwritten with a clean offline baseline, so a client cannot
     * seed a device as already playing.
     *
     * <param name="device">the device to add; only name, address, security key,
     * and location are taken from the body</param>
     * <returns>200 with the stored device; 400 when the address is blank;
     * 409 when that address is already registered</returns>
     */
    // POST: api/Devices
    [HttpPost]
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

    /**
     * Removes a device from the inventory. The unit itself is left running
     * whatever it is running; only this system stops tracking it.
     *
     * <param name="ipAddress">address of the device to remove</param>
     * <returns>200 on success; 400 when the address is blank; 404 when no such
     * device exists</returns>
     */
    // DELETE: api/Devices
    [HttpDelete]
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
}
