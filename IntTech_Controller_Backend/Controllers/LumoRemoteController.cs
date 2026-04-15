using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using IntTech_Controller_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IntTech_Controller_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LumoRemoteController : ControllerBase
    {
        private readonly IntTechDBContext _context;
        private readonly LumoCommandService _commandService;
        private readonly IWebHostEnvironment _env;

        public LumoRemoteController(IntTechDBContext context, LumoCommandService commandService, IWebHostEnvironment env)
        {
            _context = context;
            _commandService = commandService;
            _env = env;
        }

        // ==========================================
        // DEVICE ENDPOINTS
        // ==========================================

        // GET: api/LumoRemote/devices
        [HttpGet("devices")]
        public async Task<IActionResult> GetDevices()
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var locationsClaim = User.FindFirstValue("AllowedLocationsIds");
            var allowedLocationIdsStr = string.IsNullOrEmpty(locationsClaim) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(locationsClaim) ?? new List<string>();

            var allowedLocationIds = allowedLocationIdsStr.Where(idStr => ObjectId.TryParse(idStr, out _)).Select(ObjectId.Parse).ToList();

            var query = _context.Devices.AsQueryable();
            if (userRole != "Admin")
            {
                query = query.Where(d => allowedLocationIds.Contains(d.LocationId));
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

        // GET: api/LumoRemote/games
        [HttpGet("games")]
        public async Task<IActionResult> GetGames([FromQuery] string? platform)
        {
            if (platform != null && !PlatformTypes.IsValid(platform))
            {
                return BadRequest(new { Message = $"Invalid platform: '{platform}'. Valid values: {string.Join(",", PlatformTypes.All)}" });
            }

            var gamesQuery = _context.Games.AsQueryable();
            if (platform != null)
            {
                if (platform == PlatformTypes.LumoPlay)
                    gamesQuery = gamesQuery.Where(g => g.Platform == null || g.Platform == PlatformTypes.LumoPlay);
                else
                    gamesQuery = gamesQuery.Where(g => g.Platform == platform);
            }
            var games = await gamesQuery.ToListAsync();
            var allTags = await _context.Tags.ToListAsync();
            var allCategories = await _context.Categories.ToListAsync();

            var tagLookup = allTags.ToDictionary(t => t.Id);
            var categoryLookup = allCategories.ToDictionary(c => c.Id);

            var response = games.Select(game =>
            {
                // Resolve tagIds to structured tag info
                var resolvedTags = (game.TagIds ?? new List<ObjectId>())
                    .Where(id => tagLookup.ContainsKey(id))
                    .Select(id =>
                    {
                        var tag = tagLookup[id];
                        var cat = categoryLookup.ContainsKey(tag.CategoryId)
                            ? categoryLookup[tag.CategoryId]
                            : null;

                        return new
                        {
                            Id = tag.Id.ToString(),
                            tag.Name,
                            tag.Slug,
                            tag.ColorHex,
                            CategoryId = tag.CategoryId.ToString(),
                            CategoryName = cat?.Name ?? "Unknown",
                            CategorySlug = cat?.Slug ?? "unknown",
                            ParentTagId = tag.ParentTagId?.ToString()
                        };
                    })
                    .ToList();

                return new
                {
                    Id = game.Id.ToString(),
                    game.GameId,
                    game.Name,
                    game.ImageFileName,
                    game.Description,
                    game.Platform,
                    game.OnePagerFileName,
                    Tags = resolvedTags
                };
            });

            return Ok(response);
        }

        // GET: api/LumoRemote/games/{gameId}
        [HttpGet("games/{gameId}")]
        public async Task<IActionResult> GetGameById(string gameId)
        {
            var game = await _context.Games
                .FirstOrDefaultAsync(g => g.GameId == gameId);

            if (game == null)
            {
                return NotFound($"Game with ID '{gameId}' not found.");
            }

            var allTags = await _context.Tags.ToListAsync();
            var allCategories = await _context.Categories.ToListAsync();

            var tagLookup = allTags.ToDictionary(t => t.Id);
            var categoryLookup = allCategories.ToDictionary(c => c.Id);

            var resolvedTags = (game.TagIds ?? new List<ObjectId>())
                .Where(id => tagLookup.ContainsKey(id))
                .Select(id =>
                {
                    var tag = tagLookup[id];
                    var cat = categoryLookup.ContainsKey(tag.CategoryId)
                        ? categoryLookup[tag.CategoryId]
                        : null;

                    return new
                    {
                        Id = tag.Id.ToString(),
                        tag.Name,
                        tag.Slug,
                        tag.ColorHex,
                        CategoryId = tag.CategoryId.ToString(),
                        CategoryName = cat?.Name ?? "Unknown",
                        CategorySlug = cat?.Slug ?? "unknown",
                        ParentTagId = tag.ParentTagId?.ToString()
                    };
                })
                .ToList();

            return Ok(new
            {
                Id = game.Id.ToString(),
                game.GameId,
                game.Name,
                game.ImageFileName,
                game.Description,
                game.Platform,
                game.OnePagerFileName,
                Tags = resolvedTags
            });
        }

        // POST: api/LumoRemote/games
        [HttpPost("games")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddGame([FromBody] CreateGameDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { Message = "Name is required." });

            // Validate platform
            var platform = dto.Platform ?? "lumoplay";
            if (!PlatformTypes.IsValid(platform))
                return BadRequest(new { Message = $"Invalid platform: '{platform}'." });

            // For lumoplay, GameId is required (used for device commands)
            // For vr/switch, auto-generate if not provided
            string gameId;
            if (platform == PlatformTypes.LumoPlay)
            {
                if (string.IsNullOrWhiteSpace(dto.GameId))
                    return BadRequest(new { Message = "GameId is required for LUMOplay games." });
                gameId = dto.GameId.Trim();
            }
            else
            {
                gameId = !string.IsNullOrWhiteSpace(dto.GameId)
                    ? dto.GameId.Trim()
                    : $"{platform}-{ObjectId.GenerateNewId()}";
            }

            // Check for duplicate GameId
            var existing = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (existing != null)
                return Conflict(new { Message = $"A game with ID '{gameId}' already exists." });

            var game = new Game
            {
                Id = ObjectId.GenerateNewId(),
                GameId = gameId,
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                ImageFileName = dto.ImageFileName?.Trim(),
                OnePagerFileName = dto.OnePagerFileName?.Trim(),
                Platform = platform,
                TagIds = new List<ObjectId>(),

            };

            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Id = game.Id.ToString(),
                game.GameId,
                game.Name,
                game.Description,
                game.ImageFileName,
                game.OnePagerFileName,
                game.Platform
            });
        }

        // PUT: api/LumoRemote/games/{gameId}
        [HttpPut("games/{gameId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateGame(string gameId, [FromBody] UpdateGameDto dto)
        {
            if (dto == null) return BadRequest("Request body is required.");

            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game == null) return NotFound($"Game with ID '{gameId}' not found.");

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                game.Name = dto.Name.Trim();
            }

            // Allow setting description to empty string (clearing it)
            if (dto.Description != null)
            {
                game.Description = dto.Description.Trim();
            }

            // Allow setting imageFileName to empty string (clearing it)
            if (dto.ImageFileName != null)
            {
                game.ImageFileName = string.IsNullOrWhiteSpace(dto.ImageFileName)
                    ? null
                    : dto.ImageFileName.Trim();
            }

            if (dto.OnePagerFileName != null)
            {
                game.OnePagerFileName = string.IsNullOrWhiteSpace(dto.OnePagerFileName)
                    ? null
                    : dto.OnePagerFileName.Trim();
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Id = game.Id.ToString(),
                game.GameId,
                game.Name,
                game.Description,
                game.ImageFileName,
                game.OnePagerFileName,
                game.Platform
            });
        }

        // DELETE: api/LumoRemote/games/{gameId}
        [HttpDelete("games/{gameId}")]
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

        // POST: api/LumoRemote/games/{gameId}/image
        // Uploads an image file and associates it with a game
        [HttpPost("games/{gameId}/image")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadGameImage(string gameId, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp"};
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension)) return BadRequest($"Invalid file type '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}");

            const long maxFileSize = 5 * 1024 * 1024; // 5 MB
            if (file.Length > maxFileSize) return BadRequest($"File size exceeds the 5MB limit.");

            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game == null) return NotFound($"Game with ID '{gameId}' not found.");

            var sanitized = Regex.Replace(game.Name.ToLower().Trim(), @"\s+", "-");
            sanitized = Regex.Replace(sanitized, @"[^a-z0-9\-]", ""); // Remove invalid chars
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = game.GameId.ToString();
            }
            var newFileName = $"{sanitized}{extension}";

            var imagesPath = Path.Combine(_env.WebRootPath, "images");
            if (!Directory.Exists(imagesPath))
            {
                Directory.CreateDirectory(imagesPath);
            }

            var imagesFullPath = Path.GetFullPath(imagesPath);
            var filePath = Path.Combine(imagesFullPath, newFileName);

            if (!string.IsNullOrEmpty(game.ImageFileName))
            {
                var storedFileName = Path.GetFileName(game.ImageFileName);
                if (string.Equals(storedFileName, game.ImageFileName, StringComparison.Ordinal))
                {
                    var oldFilePath = Path.GetFullPath(Path.Combine(imagesFullPath, storedFileName));
                    var imagesPathPrefix = imagesFullPath.EndsWith(Path.DirectorySeparatorChar)
                        ? imagesFullPath
                        : imagesFullPath + Path.DirectorySeparatorChar;

                    if (oldFilePath.StartsWith(imagesPathPrefix, StringComparison.Ordinal) &&
                        System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            game.ImageFileName = newFileName;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = $"Image uploaded and associated with game '{game.Name}'",
                ImageFileName = newFileName,
                ImageUrl = $"/images/{newFileName}"
            });
        }

        // DELETE: api/LumoRemote/games/{gameId}/image
        // Removes the image file from the associated game and deletes the file from the server
        [HttpDelete("games/{gameId}/image")]
        [Authorize (Roles = "Admin")]
        public async Task<IActionResult> DeleteGameImage(string gameId)
        {
            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game == null) return NotFound($"Game with ID '{gameId}' not found.");
            if (string.IsNullOrEmpty(game.ImageFileName))
            {
                return BadRequest("This game does not have an associated image.");
            }
            var imagesPath = Path.Combine(_env.WebRootPath, "images");
            var filePath = Path.Combine(imagesPath, game.ImageFileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            game.ImageFileName = null;
            await _context.SaveChangesAsync();
            return Ok($"Image for game '{game.Name}' has been removed.");
        }

        // POST: api/LumoRemote/games/{gameId}/one-pager
        // Uploads an one pager file and associates it with a game
        [HttpPost("games/{gameId}/one-pager")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadGameOnePager(string gameId, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension)) return BadRequest($"Invalid file type '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}");

            const long maxFileSize = 10 * 1024 * 1024; // 10 MB
            if (file.Length > maxFileSize) return BadRequest($"File size exceeds the 10 MB limit.");

            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game == null) return NotFound($"Game with ID '{gameId}' not found.");

            var sanitized = Regex.Replace(game.Name.ToLower().Trim(), @"\s+", "-");
            sanitized = Regex.Replace(sanitized, @"[^a-z0-9\-]", ""); // Remove invalid chars
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = game.GameId.ToString();
            }
            var newFileName = $"{sanitized}-onepager{extension}";

            var onePagersPath = Path.Combine(_env.WebRootPath, "one-pagers");
            if (!Directory.Exists(onePagersPath))
            {
                Directory.CreateDirectory(onePagersPath);
            }

            var onePagersFullPath = Path.GetFullPath(onePagersPath);
            var filePath = Path.Combine(onePagersFullPath, newFileName);

            if (!string.IsNullOrEmpty(game.OnePagerFileName))
            {
                var storedFileName = Path.GetFileName(game.OnePagerFileName);
                if (string.Equals(storedFileName, game.OnePagerFileName, StringComparison.Ordinal))
                {
                    var oldFilePath = Path.GetFullPath(Path.Combine(onePagersFullPath, storedFileName));
                    var imagesPathPrefix = onePagersFullPath.EndsWith(Path.DirectorySeparatorChar)
                        ? onePagersFullPath
                        : onePagersFullPath + Path.DirectorySeparatorChar;

                    if (oldFilePath.StartsWith(imagesPathPrefix, StringComparison.Ordinal) &&
                        System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            game.OnePagerFileName = newFileName;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = $"One Pager uploaded and associated with game '{game.Name}'",
                OnePagerFileName = newFileName,
                OnePagerUrl = $"/one-pagers/{newFileName}"
            });
        }

        // DELETE: api/LumoRemote/games/{gameId}/one-pager
        // Removes the one pager file from the associated game and deletes the file from the server
        [HttpDelete("games/{gameId}/one-pager")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteGameOnePager(string gameId)
        {
            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game == null) return NotFound($"Game with ID '{gameId}' not found.");
            if (string.IsNullOrEmpty(game.OnePagerFileName))
            {
                return BadRequest("This game does not have an associated one pager.");
            }
            var imagesPath = Path.Combine(_env.WebRootPath, "one-pagers");
            var filePath = Path.Combine(imagesPath, game.OnePagerFileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            game.OnePagerFileName = null;
            await _context.SaveChangesAsync();
            return Ok($"One pager for game '{game.Name}' has been removed.");
        }



        // POST: api/LumoRemote/games/{gameId}/tags
        // Replaces ALL tag assignments for a game in one call.
        [HttpPost("games/{gameId}/tags")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetGameTags(string gameId, [FromBody] SetGameTagsToDto dto)
        {
            if (dto == null) return BadRequest("Request body is required.");

            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game == null) return NotFound($"Game with ID '{gameId}' not found.");

            var validTagIds = new List<ObjectId>();
            var allTags = await _context.Tags.ToDictionaryAsync(t => t.Id);

            foreach (var idStr in dto.TagIds)
            {
                if (!ObjectId.TryParse(idStr, out ObjectId tagOid))
                    return BadRequest($"Invalid tag ID format: '{idStr}'");

                if (!allTags.ContainsKey(tagOid))
                    return NotFound($"Tag not found: '{idStr}'");

                if (!validTagIds.Contains(tagOid))
                    validTagIds.Add(tagOid);
            }

            game.TagIds = validTagIds;
            await _context.SaveChangesAsync();

            // Return the resolved tag info for immediate UI use
            var resolvedTags = validTagIds
                .Where(id => allTags.ContainsKey(id))
                .Select(id => new
                {
                    Id = id.ToString(),
                    allTags[id].Name,
                    allTags[id].Slug,
                    CategoryId = allTags[id].CategoryId.ToString(),
                    allTags[id].ColorHex
                })
                .ToList();

            return Ok(new
            {
                Message = $"Updated tags for game '{game.Name}'",
                TagCount = validTagIds.Count,
                Tags = resolvedTags
            });

        }

        // GET: api/LumoRemote/games/{gameId}/tags
        // Returns resolved tag details for a single game.
        [HttpGet("games/{gameId}/tags")]
        public async Task<IActionResult> GetGameTags(string gameId)
        {
            var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (game == null)
                return NotFound($"Game with ID '{gameId}' not found.");

            var allTags = await _context.Tags.ToListAsync();
            var allCategories = await _context.Categories.ToListAsync();

            var tagLookup = allTags.ToDictionary(t => t.Id);
            var categoryLookup = allCategories.ToDictionary(c => c.Id);

            // Resolve each tagId to its full info, grouped by category
            var tagsByCategory = (game.TagIds ?? Enumerable.Empty<ObjectId>())
                .Where(id => tagLookup.ContainsKey(id))
                .Select(id => tagLookup[id])
                .GroupBy(t => t.CategoryId)
                .Select(group =>
                {
                    var category = categoryLookup.ContainsKey(group.Key)
                        ? categoryLookup[group.Key]
                        : null;

                    return new
                    {
                        CategoryId = group.Key.ToString(),
                        CategoryName = category?.Name ?? "Unknown",
                        Tags = group.Select(t => new
                        {
                            Id = t.Id.ToString(),
                            t.Name,
                            t.Slug,
                            t.ColorHex,
                            ParentTagId = t.ParentTagId?.ToString()
                        }).ToList()
                    };
                })
                .ToList();

            return Ok(tagsByCategory);
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
        [HttpPost("playlists/remove")]
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