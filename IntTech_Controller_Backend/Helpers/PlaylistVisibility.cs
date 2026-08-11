using System.Linq.Expressions;
using IntTech_Controller_Backend.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace IntTech_Controller_Backend.Helpers;

/**
 * Decides which playlists a user may see. A playlist is visible when the user
 * owns it or it is marked as a default; the same rule is expressed both as a
 * queryable predicate and as an in-memory check.
 */
public static class PlaylistVisibility
{
    /**
     * LINQ predicate for playlists visible to a user: owned by them OR marked as default.
     *
     * <param name="userId">the user to test visibility for</param>
     * <returns>a predicate suitable for composing into a database query</returns>
     */
    public static Expression<Func<Playlist, bool>> VisibleTo(ObjectId userId)
    {
        return p => p.OwnerId == userId || p.IsDefault;
    }

    /**
     * Determines whether a user may see an already-loaded playlist.
     *
     * <param name="playlist">the playlist to test</param>
     * <param name="userId">the user to test visibility for</param>
     * <returns>true when the user owns the playlist or it is a default</returns>
     */
    public static bool CanUserSee(Playlist playlist, ObjectId userId)
    {
        return playlist.OwnerId == userId || playlist.IsDefault;
    }

    /**
     * Narrows a set of playlist ids to those the user may see, in a single query.
     * Ids that no longer exist are dropped along with the invisible ones.
     *
     * <param name="playlistIds">candidate ids; duplicates are collapsed</param>
     * <param name="userId">the user to test visibility for</param>
     * <param name="playlists">queryable source of playlists</param>
     * <returns>the subset of ids that exist and are visible to the user</returns>
     */
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

    /**
     * Returns the ActivePlaylistState the user should see for this device.
     * Returns null if the device has no active playlist, the playlist no longer exists,
     * or the playlist is not visible to this user. Read-time filter only — DB is not mutated.
     *
     * <param name="device">the device whose active playlist is being reported</param>
     * <param name="userId">the user the response is being built for</param>
     * <param name="playlists">queryable source of playlists</param>
     * <returns>the device's active playlist state, or null when it should be hidden</returns>
     */
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
