using IntTech_Controller_Backend.Data;
using IntTech_Controller_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

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
    [HttpGet]
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

    // POST: api/Playlists/add
    [HttpPost("add")]
    public async Task<IActionResult> AddPlaylist([FromBody] Playlist playlist)
    {
        if (string.IsNullOrEmpty(playlist.Name)) return BadRequest("Name is required");

        if (playlist.Games == null) playlist.Games = new List<PlaylistGame>();


        playlist.Id = ObjectId.GenerateNewId();

        _context.Playlists.Add(playlist);
        await _context.SaveChangesAsync();

        return Ok(playlist);
    }

    // POST: api/Playlists/remove/{playlistId}
    [HttpPost("remove/{playlistId}")]
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

    // POST: api/Playlists/{playlistId}/add-game-to-playlist/{gameId}
    [HttpPost("{playlistId}/add-game-to-playlist/{gameId}")]
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

    // POST: api/Playlists/{playlistId}/remove-game-from-playlist/{gameId}
    [HttpPost("{playlistId}/remove-game-from-playlist/{gameId}")]
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

    // PUT: api/Playlists/{playlistId}/update-order
    [HttpPut("{playlistId}/update-order")]
    public async Task<IActionResult> UpdatePlaylistOrder(string playlistId, [FromBody] List<string> gameIds)
    {
        if (gameIds == null) return BadRequest("Game ID list is required.");

        if (!ObjectId.TryParse(playlistId, out ObjectId oid))
            return BadRequest("Invalid Playlist ID format.");

        var playlist = await _context.Playlists.FirstOrDefaultAsync(p => p.Id == oid);

        if (playlist == null) return NotFound($"No playlist with ID '{playlistId}'");

        var existing = (playlist.Games ?? new List<PlaylistGame>())
            .ToDictionary(g => g.GameId);

        playlist.Games = gameIds
            .Where(id => existing.ContainsKey(id))
            .Select(id => existing[id])
            .ToList();

        await _context.SaveChangesAsync();

        return Ok(new { Message = $"Order updated for playlist '{playlist.Name}'", GameCount = playlist.Games.Count });
    }
}
