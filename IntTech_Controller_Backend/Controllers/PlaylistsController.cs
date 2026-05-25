using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Helpers;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IntTech_Controller_Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlaylistsController : ControllerBase
{
    private readonly IntTechDBContext _context;

    public PlaylistsController(IntTechDBContext context)
    {
        _context = context;
    }

    // GET: api/Playlists
    // Returns caller's own playlists + all default playlists, with hydrated game objects and resolved tags.
    [HttpGet]
    public async Task<IActionResult> GetPlaylists()
    {
        var userId = ClaimsHelper.GetUserId(User);

        var playlists = await _context.Playlists
            .Where(PlaylistVisibility.VisibleTo(userId))
            .ToListAsync();

        var gameIdsToFetch = playlists
            .SelectMany(p => p.Games ?? new List<PlaylistGame>())
            .Select(g => g.GameId)
            .Distinct()
            .ToList();

        var libraryGames = await _context.Games
            .Where(g => gameIdsToFetch.Contains(g.GameId))
            .ToListAsync();

        var gamesById = libraryGames.ToDictionary(g => g.GameId);

        var userRole = ClaimsHelper.GetUserRole(User);
        var allowedTagIds = userRole != "Admin"
            ? ClaimsHelper.GetAllowedTagIds(User).ToHashSet()
            : new HashSet<ObjectId>();

        var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
        var allCategoriesById = await _context.Categories.ToDictionaryAsync(c => c.Id);

        var visibilityTagsById = userRole != "Admin"
            ? allTagsById
            : new Dictionary<ObjectId, Models.Tag>();

        var response = playlists.Select(p => new
        {
            Id = p.Id.ToString(),
            p.Name,
            OwnerId = p.OwnerId.ToString(),
            p.IsDefault,
            Games = (p.Games ?? new List<PlaylistGame>())
                .Select(pg => gamesById.TryGetValue(pg.GameId, out var game) ? game : null)
                .Where(g => g != null)
                .Where(g => userRole == "Admin" || GameAccessHelper.IsGameVisibleToUser(g!, allowedTagIds, visibilityTagsById))
                .Select(g => ResolveGameResponse(g!, allTagsById, allCategoriesById))
                .ToList()
        });

        return Ok(response);
    }

    // GET: api/Playlists/{id}
    // Returns 404 (not 403) when the playlist exists but isn't visible — avoids leaking existence.
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlaylistById(string id)
    {
        if (!ObjectId.TryParse(id, out ObjectId oid))
            return BadRequest("Invalid playlist ID format.");

        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
        if (playlist == null) return NotFound();

        var userId = ClaimsHelper.GetUserId(User);
        if (!PlaylistVisibility.CanUserSee(playlist, userId))
            return NotFound();

        var gameIdsToFetch = (playlist.Games ?? new List<PlaylistGame>())
            .Select(g => g.GameId)
            .Distinct()
            .ToList();

        var libraryGames = await _context.Games
            .Where(g => gameIdsToFetch.Contains(g.GameId))
            .ToListAsync();

        var gamesById = libraryGames.ToDictionary(g => g.GameId);

        var userRole = ClaimsHelper.GetUserRole(User);
        var allowedTagIds = userRole != "Admin"
            ? ClaimsHelper.GetAllowedTagIds(User).ToHashSet()
            : new HashSet<ObjectId>();

        var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
        var allCategoriesById = await _context.Categories.ToDictionaryAsync(c => c.Id);

        var visibilityTagsById = userRole != "Admin"
            ? allTagsById
            : new Dictionary<ObjectId, Models.Tag>();

        return Ok(new
        {
            Id = playlist.Id.ToString(),
            playlist.Name,
            OwnerId = playlist.OwnerId.ToString(),
            playlist.IsDefault,
            Games = (playlist.Games ?? new List<PlaylistGame>())
                .Select(pg => gamesById.TryGetValue(pg.GameId, out var game) ? game : null)
                .Where(g => g != null)
                .Where(g => userRole == "Admin" || GameAccessHelper.IsGameVisibleToUser(g!, allowedTagIds, visibilityTagsById))
                .Select(g => ResolveGameResponse(g!, allTagsById, allCategoriesById))
                .ToList()
        });
    }

    // POST: api/Playlists
    // Creates a playlist owned by the caller. IsDefault is always false on creation.
    [HttpPost]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto?.Name))
            return BadRequest(new { error = "Name is required." });

        var userId = ClaimsHelper.GetUserId(User);

        var playlist = new Playlist
        {
            Id = ObjectId.GenerateNewId(),
            Name = dto.Name,
            OwnerId = userId,
            IsDefault = false,
            Games = dto.Games ?? new List<PlaylistGame>(),
        };

        _context.Playlists.Add(playlist);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex) when (IsDuplicateKeyError(ex))
        {
            return Conflict(new { error = "You already have a playlist with that name." });
        }

        return CreatedAtAction(nameof(GetPlaylistById), new { id = playlist.Id.ToString() }, playlist);
    }

    // PUT: api/Playlists/{id}
    // Full update (name + games). Owner-only. Does not touch IsDefault.
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlaylist(string id, [FromBody] UpdatePlaylistDto dto)
    {
        if (!ObjectId.TryParse(id, out ObjectId oid))
            return BadRequest("Invalid playlist ID format.");

        var userId = ClaimsHelper.GetUserId(User);
        var existing = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
        if (existing == null) return NotFound();
        if (existing.OwnerId != userId) return Forbid();

        existing.Name = dto.Name;
        existing.Games = dto.Games ?? new List<PlaylistGame>();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex) when (IsDuplicateKeyError(ex))
        {
            return Conflict(new { error = "You already have a playlist with that name." });
        }

        return NoContent();
    }

    // DELETE: api/Playlists/{id}
    // Owner-only deletion.
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlaylist(string id)
    {
        if (!ObjectId.TryParse(id, out ObjectId oid))
            return BadRequest("Invalid playlist ID format.");

        var userId = ClaimsHelper.GetUserId(User);
        var existing = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
        if (existing == null) return NotFound();
        if (existing.OwnerId != userId) return Forbid();

        _context.Playlists.Remove(existing);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST: api/Playlists/{id}/publish
    // Toggles IsDefault. Admin-only, and only the playlist's own admin-owner can publish it.
    [HttpPost("{id}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetPublishState(string id, [FromBody] PublishDto dto)
    {
        if (!ObjectId.TryParse(id, out ObjectId oid))
            return BadRequest("Invalid playlist ID format.");

        var userId = ClaimsHelper.GetUserId(User);
        var existing = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
        if (existing == null) return NotFound();
        if (existing.OwnerId != userId) return Forbid();

        existing.IsDefault = dto.IsDefault;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST: api/Playlists/{id}/clone
    // Copies a visible playlist into the caller's personal library.
    // Name collision is resolved with " (Copy)" / " (Copy 2)" suffixes.
    [HttpPost("{id}/clone")]
    public async Task<IActionResult> ClonePlaylist(string id)
    {
        if (!ObjectId.TryParse(id, out ObjectId oid))
            return BadRequest("Invalid playlist ID format.");

        var userId = ClaimsHelper.GetUserId(User);
        var source = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
        if (source == null) return NotFound();
        if (!PlaylistVisibility.CanUserSee(source, userId)) return NotFound();

        var cloneName = await GenerateUniqueCloneName(source.Name, userId);

        var clone = new Playlist
        {
            Id = ObjectId.GenerateNewId(),
            Name = cloneName,
            OwnerId = userId,
            IsDefault = false,
            Games = (source.Games ?? new List<PlaylistGame>())
                .Select(g => new PlaylistGame { GameId = g.GameId, Name = g.Name })
                .ToList(),
        };

        _context.Playlists.Add(clone);
        await _context.SaveChangesAsync();

        var gameIds = (clone.Games ?? new List<PlaylistGame>())
            .Select(g => g.GameId)
            .Distinct()
            .ToList();

        var games = await _context.Games
            .Where(g => gameIds.Contains(g.GameId))
            .ToListAsync();

        var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
        var allCategoriesById = await _context.Categories.ToDictionaryAsync(c => c.Id);

        var playlistDto = new
        {
            Id = clone.Id.ToString(),
            clone.Name,
            OwnerId = clone.OwnerId.ToString(),
            clone.IsDefault,
            Games = games.Select(g => ResolveGameResponse(g, allTagsById, allCategoriesById)).ToList()
        };

        return CreatedAtAction(nameof(GetPlaylistById), new { id = clone.Id.ToString() }, playlistDto);
    }

    // POST: api/Playlists/{playlistId}/add-game-to-playlist/{gameId}
    [HttpPost("{playlistId}/add-game-to-playlist/{gameId}")]
    public async Task<IActionResult> AddGameToPlaylistById(string playlistId, string gameId)
    {
        if (!ObjectId.TryParse(playlistId, out ObjectId oid))
            return BadRequest("Invalid Playlist ID format.");

        var userId = ClaimsHelper.GetUserId(User);
        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
        if (playlist == null) return NotFound("Playlist ID not found");
        if (playlist.OwnerId != userId) return Forbid();

        var game = await _context.Games.FirstOrDefaultAsync(g => g.GameId == gameId);
        if (game == null) return NotFound("Game ID not found");

        // Cross-platform playlists: any platform may be added. Only the playlist skip/launch
        // logic in PlaybackController gates on platform == "lumoplay".

        var userRole = ClaimsHelper.GetUserRole(User);
        if (userRole != "Admin")
        {
            var allowedTagIds = ClaimsHelper.GetAllowedTagIds(User).ToHashSet();
            var allTagsById = await _context.Tags.ToDictionaryAsync(t => t.Id);
            if (!GameAccessHelper.IsGameVisibleToUser(game, allowedTagIds, allTagsById))
                return Forbid();
        }

        if (playlist.Games == null) playlist.Games = new List<PlaylistGame>();

        if (!playlist.Games.Any(x => x.GameId == gameId))
        {
            playlist.Games.Add(new PlaylistGame { GameId = game.GameId, Name = game.Name });
            await _context.SaveChangesAsync();
            return Ok($"Added `{game.Name}` to playlist `{playlist.Name}`");
        }

        return Ok("Game already exists in this playlist.");
    }

    // POST: api/Playlists/{playlistId}/remove-game-from-playlist/{gameId}
    [HttpPost("{playlistId}/remove-game-from-playlist/{gameId}")]
    public async Task<IActionResult> RemoveGameFromPlaylistById(string playlistId, string gameId)
    {
        if (!ObjectId.TryParse(playlistId, out ObjectId oid))
            return BadRequest("Invalid Playlist ID format.");

        var userId = ClaimsHelper.GetUserId(User);
        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
        if (playlist == null) return NotFound("Playlist ID not found");
        if (playlist.OwnerId != userId) return Forbid();

        var gameToRemove = playlist.Games?.FirstOrDefault(g => g.GameId == gameId);
        if (gameToRemove == null) return NotFound($"Game `{gameId}` is not in the playlist.");

        playlist.Games!.Remove(gameToRemove);
        await _context.SaveChangesAsync();
        return Ok($"Removed game '{gameToRemove.Name}' from Playlist '{playlist.Name}'");
    }

    // PUT: api/Playlists/{playlistId}/update-order
    [HttpPut("{playlistId}/update-order")]
    public async Task<IActionResult> UpdatePlaylistOrder(string playlistId, [FromBody] List<string> gameIds)
    {
        if (gameIds == null) return BadRequest("Game ID list is required.");
        if (!ObjectId.TryParse(playlistId, out ObjectId oid))
            return BadRequest("Invalid Playlist ID format.");

        var userId = ClaimsHelper.GetUserId(User);
        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);
        if (playlist == null) return NotFound($"No playlist with ID '{playlistId}'");
        if (playlist.OwnerId != userId) return Forbid();

        var existing = (playlist.Games ?? new List<PlaylistGame>()).ToDictionary(g => g.GameId);
        playlist.Games = gameIds
            .Where(id => existing.ContainsKey(id))
            .Select(id => existing[id])
            .ToList();

        await _context.SaveChangesAsync();
        return Ok(new { Message = $"Order updated for playlist '{playlist.Name}'", GameCount = playlist.Games.Count });
    }

    private static object ResolveGameResponse(
        Game game,
        Dictionary<ObjectId, Models.Tag> tagsById,
        Dictionary<ObjectId, Category> categoriesById)
    {
        var resolvedTags = (game.TagIds ?? new List<ObjectId>())
            .Where(id => tagsById.ContainsKey(id))
            .Select(id => tagsById[id])
            .OrderBy(tag => categoriesById.ContainsKey(tag.CategoryId)
                ? categoriesById[tag.CategoryId].DisplayOrder
                : int.MaxValue)
            .ThenBy(tag => tag.DisplayOrder)
            .ThenBy(tag => tag.Name)
            .Select(tag =>
            {
                var cat = categoriesById.ContainsKey(tag.CategoryId)
                    ? categoriesById[tag.CategoryId]
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
    }

    private static bool IsDuplicateKeyError(Exception ex)
    {
        if (ex is MongoWriteException mwe)
            return mwe.WriteError?.Category == ServerErrorCategory.DuplicateKey;
        if (ex.InnerException is MongoWriteException inner)
            return inner.WriteError?.Category == ServerErrorCategory.DuplicateKey;
        return false;
    }

    private async Task<string> GenerateUniqueCloneName(string sourceName, ObjectId ownerId)
    {
        var candidate = $"{sourceName} (Copy)";
        var n = 2;
        while (await _context.Playlists.AnyAsync(p => p.OwnerId == ownerId && p.Name == candidate))
        {
            candidate = $"{sourceName} (Copy {n})";
            n++;
        }
        return candidate;
    }
}

public class CreatePlaylistDto
{
    public string Name { get; set; }
    public List<PlaylistGame>? Games { get; set; }
}

public class UpdatePlaylistDto
{
    public string Name { get; set; }
    public List<PlaylistGame>? Games { get; set; }
}

public class PublishDto
{
    public bool IsDefault { get; set; }
}
