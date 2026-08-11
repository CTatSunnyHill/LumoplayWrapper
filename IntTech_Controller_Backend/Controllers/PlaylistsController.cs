using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Helpers;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IntTech_Controller_Backend.Controllers;

/**
 * Playlist management. Two access rules run side by side here: a user may
 * *see* their own playlists plus every published default, but may only *edit*
 * the ones they own. Games inside a playlist are separately filtered by tag, so
 * a shared default can show different contents to different clinicians.
 *
 * Per-owner name uniqueness is enforced by a MongoDB index rather than a
 * pre-check, so a collision surfaces as a write exception and is translated to
 * 409 here.
 */
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlaylistsController : ControllerBase
{
    private readonly IntTechDBContext _context;

    /**
     * <param name="context">database context for playlists, games, tags, and categories</param>
     */
    public PlaylistsController(IntTechDBContext context)
    {
        _context = context;
    }

    /**
     * Returns caller's own playlists + all default playlists, with hydrated game objects and resolved tags.
     * Entries whose game has since left the library are dropped, as are games
     * the caller's tags do not permit.
     *
     * <returns>200 with the visible playlists and their visible games</returns>
     */
    // GET: api/Playlists
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

        // Admins get an empty map here: tags are still resolved for display via
        // allTagsById, but the visibility check is skipped outright.
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

    /**
     * Fetches one playlist with its games hydrated and tag-filtered.
     * Returns 404 (not 403) when the playlist exists but isn't visible — avoids leaking existence.
     *
     * <param name="id">string form of the playlist's ObjectId</param>
     * <returns>200 with the playlist; 400 for a malformed id; 404 when it does
     * not exist or is not visible to the caller</returns>
     */
    // GET: api/Playlists/{id}
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

    /**
     * Creates a playlist owned by the caller. IsDefault is always false on creation.
     * Publishing is a separate, admin-only step.
     *
     * <param name="dto">the name and optional initial games</param>
     * <returns>201 with the created playlist; 400 when the name is blank;
     * 409 when the caller already has a playlist with that name</returns>
     */
    // POST: api/Playlists
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

    /**
     * Full update (name + games). Owner-only. Does not touch IsDefault.
     * The supplied game list replaces the existing one outright.
     *
     * <param name="id">string form of the playlist's ObjectId</param>
     * <param name="dto">the new name and full game list</param>
     * <returns>204 on success; 400 for a malformed id; 403 when the caller does
     * not own it; 404 when not found; 409 on a name collision</returns>
     */
    // PUT: api/Playlists/{id}
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

    /**
     * Owner-only deletion. Deleting a published default removes it from
     * everyone, since defaults are shared rather than copied.
     *
     * <param name="id">string form of the playlist's ObjectId</param>
     * <returns>204 on success; 400 for a malformed id; 403 when the caller does
     * not own it; 404 when not found</returns>
     */
    // DELETE: api/Playlists/{id}
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

    /**
     * Toggles IsDefault. Admin-only, and only the playlist's own admin-owner can publish it.
     * Publishing makes the playlist visible to every user; unpublishing hides it
     * again without deleting anyone's clones of it.
     *
     * <param name="id">string form of the playlist's ObjectId</param>
     * <param name="dto">the publish state to set</param>
     * <returns>204 on success; 400 for a malformed id; 403 when the caller does
     * not own it; 404 when not found</returns>
     */
    // POST: api/Playlists/{id}/publish
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

    /**
     * Copies a visible playlist into the caller's personal library.
     * Name collision is resolved with " (Copy)" / " (Copy 2)" suffixes.
     * This is how a clinician takes a published default and makes it their own
     * to edit.
     *
     * <param name="id">string form of the source playlist's ObjectId</param>
     * <returns>201 with the new playlist; 400 for a malformed id; 404 when the
     * source does not exist or is not visible</returns>
     */
    // POST: api/Playlists/{id}/clone
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

    /**
     * Appends a game to a playlist the caller owns. Adding a game already
     * present is a no-op rather than an error, so a double-tap is harmless.
     *
     * <param name="playlistId">string form of the playlist's ObjectId</param>
     * <param name="gameId">vendor id of the game to add</param>
     * <returns>200 on success or when already present; 400 for a malformed
     * playlist id; 403 when the caller does not own the playlist or is not
     * permitted the game; 404 when either the playlist or game is unknown</returns>
     */
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

    /**
     * Removes a game from a playlist the caller owns.
     *
     * <param name="playlistId">string form of the playlist's ObjectId</param>
     * <param name="gameId">vendor id of the game to remove</param>
     * <returns>200 on success; 400 for a malformed playlist id; 403 when the
     * caller does not own it; 404 when the playlist is unknown or does not
     * contain that game</returns>
     */
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

    /**
     * Rearranges a playlist to match the order given. Ids not currently in the
     * playlist are ignored, and any entry the caller omits is dropped — so this
     * doubles as a bulk remove.
     *
     * <param name="playlistId">string form of the playlist's ObjectId</param>
     * <param name="gameIds">the playlist's game ids in the desired order</param>
     * <returns>200 with the resulting game count; 400 when the list is missing
     * or the id is malformed; 403 when the caller does not own it; 404 when not
     * found</returns>
     */
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

    /**
     * Shapes a game for the API, expanding its tag ids into full tag objects
     * with their category details. Tags are ordered by category first, then
     * within the category, so the UI can render them in a stable grouping
     * without sorting them itself. Tags that no longer exist are dropped, and a
     * tag whose category is missing falls back to "Unknown".
     *
     * <param name="game">the game to shape</param>
     * <param name="tagsById">every known tag, keyed by id</param>
     * <param name="categoriesById">every known category, keyed by id</param>
     * <returns>an anonymous object ready to serialise</returns>
     */
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

    /**
     * Recognises the per-owner name collision raised by the "owner_name_unique"
     * index, checking one level of inner exception because EF Core wraps the
     * driver's write exception.
     *
     * <param name="ex">the exception thrown by the save</param>
     * <returns>true when it was caused by a duplicate key</returns>
     */
    private static bool IsDuplicateKeyError(Exception ex)
    {
        if (ex is MongoWriteException mwe)
            return mwe.WriteError?.Category == ServerErrorCategory.DuplicateKey;
        if (ex.InnerException is MongoWriteException inner)
            return inner.WriteError?.Category == ServerErrorCategory.DuplicateKey;
        return false;
    }

    /**
     * Finds a free name for a clone by appending " (Copy)", then " (Copy 2)",
     * " (Copy 3)", and so on until one is unused by that owner.
     *
     * <param name="sourceName">name of the playlist being cloned</param>
     * <param name="ownerId">the user the clone will belong to</param>
     * <returns>a name not currently used by that owner</returns>
     */
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

/** Request body for creating a playlist. */
public class CreatePlaylistDto
{
    /** Display name; must be unused by the caller. */
    public string Name { get; set; }
    /** Games to start the playlist with, or null for an empty one. */
    public List<PlaylistGame>? Games { get; set; }
}

/** Request body for a full playlist update; both fields are replaced wholesale. */
public class UpdatePlaylistDto
{
    /** Display name to set. */
    public string Name { get; set; }
    /** The complete new game list; null clears the playlist. */
    public List<PlaylistGame>? Games { get; set; }
}

/** Request body for publishing or unpublishing a playlist. */
public class PublishDto
{
    /** True to share the playlist with every user, false to make it private again. */
    public bool IsDefault { get; set; }
}
