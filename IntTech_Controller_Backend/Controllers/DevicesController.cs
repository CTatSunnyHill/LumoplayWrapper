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
public class DevicesController : ControllerBase
{
    private readonly IntTechDBContext _context;
    private readonly LumoCommandService _commandService;

    public DevicesController(IntTechDBContext context, LumoCommandService commandService)
    {
        _context = context;
        _commandService = commandService;
    }

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
