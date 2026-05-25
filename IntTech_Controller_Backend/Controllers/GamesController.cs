using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Helpers;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly IntTechDBContext _context;
    private readonly GameFileStorage _fileStorage;

    public GamesController(IntTechDBContext context, GameFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    // ===== CATALOG =====

    // GET: api/Games
    [HttpGet]
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

        var userRole = ClaimsHelper.GetUserRole(User);
        var allowedTagIds = ClaimsHelper.GetAllowedTagIds(User).ToHashSet();
        var visibleGames = GameAccessHelper.FilterVisibleGames(games, userRole, allowedTagIds, tagLookup);

        var response = visibleGames.Select(game =>
        {
            // Resolve tagIds to structured tag info, sorted by category and tag display order
            var resolvedTags = (game.TagIds ?? new List<ObjectId>())
                .Where(id => tagLookup.ContainsKey(id))
                .Select(id => tagLookup[id])
                .OrderBy(tag => categoryLookup.ContainsKey(tag.CategoryId)
                    ? categoryLookup[tag.CategoryId].DisplayOrder
                    : int.MaxValue)
                .ThenBy(tag => tag.DisplayOrder)
                .ThenBy(tag => tag.Name)
                .Select(tag =>
                {
                    var cat = categoryLookup.ContainsKey(tag.CategoryId)
                        ? categoryLookup[tag.CategoryId]
                        : null;

                    return new
                    {
                        Id = tag.Id.ToString(),
                        tag.Name,
                        tag.Slug,
                        tag.ColorHex,
                        tag.IsVisible,
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

    // GET: api/Games/{gameId}
    [HttpGet("{gameId}")]
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

        var userRole = ClaimsHelper.GetUserRole(User);
        if (userRole != "Admin")
        {
            var allowedTagIds = ClaimsHelper.GetAllowedTagIds(User).ToHashSet();
            if (!GameAccessHelper.IsGameVisibleToUser(game, allowedTagIds, tagLookup))
                return NotFound($"Game with ID '{gameId}' not found.");
        }

        var resolvedTags = (game.TagIds ?? new List<ObjectId>())
            .Where(id => tagLookup.ContainsKey(id))
            .Select(id => tagLookup[id])
            .OrderBy(tag => categoryLookup.ContainsKey(tag.CategoryId)
                ? categoryLookup[tag.CategoryId].DisplayOrder
                : int.MaxValue)
            .ThenBy(tag => tag.DisplayOrder)
            .ThenBy(tag => tag.Name)
            .Select(tag =>
            {
                var cat = categoryLookup.ContainsKey(tag.CategoryId)
                    ? categoryLookup[tag.CategoryId]
                    : null;

                return new
                {
                    Id = tag.Id.ToString(),
                    tag.Name,
                    tag.Slug,
                    tag.ColorHex,
                    tag.IsVisible,
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

    // POST: api/Games
    [HttpPost]
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

    // PUT: api/Games/{gameId}
    [HttpPut("{gameId}")]
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
        // Sanitize to prevent path traversal and ensure only base file names are stored
        if (dto.ImageFileName != null)
        {
            if (string.IsNullOrWhiteSpace(dto.ImageFileName))
            {
                game.ImageFileName = null;
            }
            else
            {
                var sanitized = dto.ImageFileName.Trim();
                var baseName = Path.GetFileName(sanitized);
                
                // Validate that the sanitized name matches the original (no path components)
                if (!string.Equals(baseName, sanitized, StringComparison.Ordinal))
                {
                    return BadRequest("ImageFileName must be a simple file name without path components.");
                }
                
                // Validate file extension
                var extension = Path.GetExtension(baseName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest($"ImageFileName has invalid extension '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}");
                }
                
                game.ImageFileName = baseName;
            }
        }

        if (dto.OnePagerFileName != null)
        {
            if (string.IsNullOrWhiteSpace(dto.OnePagerFileName))
            {
                game.OnePagerFileName = null;
            }
            else
            {
                var sanitized = dto.OnePagerFileName.Trim();
                var baseName = Path.GetFileName(sanitized);

                // Validate that the sanitized name matches the original (no path components)
                if (!string.Equals(baseName, sanitized, StringComparison.Ordinal))
                {
                    return BadRequest("OnePagerFileName must be a simple file name without path components.");
                }

                // Validate file extension
                var extension = Path.GetExtension(baseName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };
                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest($"OnePagerFileName has invalid extension '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}");
                }

                game.OnePagerFileName = baseName;
            }
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

    // DELETE: api/Games/{gameId}
    [HttpDelete("{gameId}")]
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

    // ===== FILES =====

    // POST: api/Games/{gameId}/image
    [HttpPost("{gameId}/image")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadGameImage(string gameId, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        var validationError = _fileStorage.ValidateImageFile(file);
        if (validationError != null) return BadRequest(validationError);

        var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
        if (game == null) return NotFound($"Game with ID '{gameId}' not found.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var newFileName = _fileStorage.BuildSanitizedFileName(game.Name, game.GameId, extension);
        await _fileStorage.SaveAndReplaceAsync("images", newFileName, file, game.ImageFileName);

        game.ImageFileName = newFileName;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = $"Image uploaded and associated with game '{game.Name}'",
            ImageFileName = newFileName,
            ImageUrl = $"/images/{newFileName}"
        });
    }

    // DELETE: api/Games/{gameId}/image
    [HttpDelete("{gameId}/image")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteGameImage(string gameId)
    {
        var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
        if (game == null) return NotFound($"Game with ID '{gameId}' not found.");
        if (string.IsNullOrEmpty(game.ImageFileName))
        {
            return BadRequest("This game does not have an associated image.");
        }
        _fileStorage.DeleteIfExists("images", game.ImageFileName);
        game.ImageFileName = null;
        await _context.SaveChangesAsync();
        return Ok($"Image for game '{game.Name}' has been removed.");
    }

    // POST: api/Games/{gameId}/one-pager
    [HttpPost("{gameId}/one-pager")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadGameOnePager(string gameId, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension)) return BadRequest($"Invalid file type '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}");

        const long maxFileSize = 20 * 1024 * 1024; // 20 MB
        if (file.Length > maxFileSize) return BadRequest($"File size exceeds the 20 MB limit.");

        var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
        if (game == null) return NotFound($"Game with ID '{gameId}' not found.");

        var newFileName = _fileStorage.BuildSanitizedFileName(game.Name, game.GameId, $"-onepager{extension}");
        await _fileStorage.SaveAndReplaceAsync("one-pagers", newFileName, file, game.OnePagerFileName);

        game.OnePagerFileName = newFileName;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = $"One Pager uploaded and associated with game '{game.Name}'",
            OnePagerFileName = newFileName,
            OnePagerUrl = $"/one-pagers/{newFileName}"
        });
    }

    // DELETE: api/Games/{gameId}/one-pager
    [HttpDelete("{gameId}/one-pager")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteGameOnePager(string gameId)
    {
        var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
        if (game == null) return NotFound($"Game with ID '{gameId}' not found.");
        if (string.IsNullOrEmpty(game.OnePagerFileName))
        {
            return BadRequest("This game does not have an associated one pager.");
        }
        _fileStorage.DeleteIfExists("one-pagers", game.OnePagerFileName);
        game.OnePagerFileName = null;
        await _context.SaveChangesAsync();
        return Ok($"One pager for game '{game.Name}' has been removed.");
    }

    // ===== TAGS =====

    // GET: api/Games/{gameId}/tags
    // Returns resolved tag details for a single game.
    [HttpGet("{gameId}/tags")]
    public async Task<IActionResult> GetGameTags(string gameId)
    {
        var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
        if (game == null)
            return NotFound($"Game with ID '{gameId}' not found.");

        var allTags = await _context.Tags.ToListAsync();
        var allCategories = await _context.Categories.ToListAsync();

        var tagLookup = allTags.ToDictionary(t => t.Id);
        var categoryLookup = allCategories.ToDictionary(c => c.Id);

        // Resolve each tagId to its full info, grouped by category.
        // Categories ordered by DisplayOrder; tags within each by DisplayOrder/Name.
        var tagsByCategory = (game.TagIds ?? Enumerable.Empty<ObjectId>())
            .Where(id => tagLookup.ContainsKey(id))
            .Select(id => tagLookup[id])
            .GroupBy(t => t.CategoryId)
            .OrderBy(g => categoryLookup.ContainsKey(g.Key)
                ? categoryLookup[g.Key].DisplayOrder
                : int.MaxValue)
            .Select(group =>
            {
                var category = categoryLookup.ContainsKey(group.Key)
                    ? categoryLookup[group.Key]
                    : null;

                return new
                {
                    CategoryId = group.Key.ToString(),
                    CategoryName = category?.Name ?? "Unknown",
                    Tags = group
                        .OrderBy(t => t.DisplayOrder)
                        .ThenBy(t => t.Name)
                        .Select(t => new
                        {
                            Id = t.Id.ToString(),
                            t.Name,
                            t.Slug,
                            t.ColorHex,
                            t.IsVisible,
                            ParentTagId = t.ParentTagId?.ToString()
                        })
                        .ToList()
                };
            })
            .ToList();

        return Ok(tagsByCategory);
    }

    // POST: api/Games/{gameId}/tags
    // Replaces ALL tag assignments for a game in one call.
    [HttpPost("{gameId}/tags")]
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

        // Return the resolved tag info for immediate UI use, sorted by category and tag display order
        var categoryLookup = await _context.Categories.ToDictionaryAsync(c => c.Id);
        var resolvedTags = validTagIds
            .Where(id => allTags.ContainsKey(id))
            .Select(id => allTags[id])
            .OrderBy(tag => categoryLookup.ContainsKey(tag.CategoryId)
                ? categoryLookup[tag.CategoryId].DisplayOrder
                : int.MaxValue)
            .ThenBy(tag => tag.DisplayOrder)
            .ThenBy(tag => tag.Name)
            .Select(tag => new
            {
                Id = tag.Id.ToString(),
                tag.Name,
                tag.Slug,
                CategoryId = tag.CategoryId.ToString(),
                tag.ColorHex,
                tag.IsVisible
            })
            .ToList();

        return Ok(new
        {
            Message = $"Updated tags for game '{game.Name}'",
            TagCount = validTagIds.Count,
            Tags = resolvedTags
        });

    }
}
