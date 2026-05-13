using System.Linq.Expressions;
using IntTech_Controller_Backend.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Helpers;

public static class PlaylistVisibility
{
    /// <summary>
    /// LINQ predicate for playlists visible to a user: owned by them OR marked as default.
    /// </summary>
    public static Expression<Func<Playlist, bool>> VisibleTo(ObjectId userId)
    {
        return p => p.OwnerId == userId || p.IsDefault;
    }

    public static bool CanUserSee(Playlist playlist, ObjectId userId)
    {
        return playlist.OwnerId == userId || playlist.IsDefault;
    }

    public static async Task<HashSet<ObjectId>> ResolveVisiblePlaylistIds(
        IEnumerable<ObjectId> playlistIds,
        ObjectId userId,
        IQueryable<Playlist> playlists)
    {
        var distinctPlaylistIds = playlistIds.ToHashSet();
        if (distinctPlaylistIds.Count == 0)
            return [];

        var visibleOids = await playlists
            .Where(p => distinctPlaylistIds.Contains(p.Id))
            .Where(VisibleTo(userId))
            .Select(p => p.Id)
            .ToListAsync();

        return [.. visibleOids];
    }

    /// <summary>
    /// Returns the ActivePlaylistState the user should see for this device.
    /// Returns null if the device has no active playlist, the playlist no longer exists,
    /// or the playlist is not visible to this user. Read-time filter only — DB is not mutated.
    /// </summary>
    public static async Task<ActivePlaylistState?> ResolveVisibleActivePlaylist(
        Device device,
        ObjectId userId,
        IQueryable<Playlist> playlists)
    {
        if (device.ActivePlaylist?.PlaylistId == null) return null;

        var oid = device.ActivePlaylist.PlaylistId.Value;
        var visiblePlaylistOids = await ResolveVisiblePlaylistIds([oid], userId, playlists);
        return visiblePlaylistOids.Contains(oid) ? device.ActivePlaylist : null;
    }
}
