using System.Linq.Expressions;
using IntTech_Controller_Backend.Models;
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
}
