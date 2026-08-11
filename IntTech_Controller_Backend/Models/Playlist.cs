using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    /**
     * An ordered run of games a device can play through, stored in the
     * "playlists" collection. Playlists are owned by the user who created them
     * unless flagged as a default.
     */
    [Collection("playlists")]
    public class Playlist
    {
        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }

        /** Display name of the playlist. */
        [BsonElement("name")]
        public string Name { get; set; }

        /** The games to play, in playback order. */
        [BsonElement("games")]
        public List<PlaylistGame> Games { get; set; }

        /** Id of the user who created the playlist. */
        [BsonElement("ownerId")]
        public ObjectId OwnerId { get; set; }

        /** True for a shared playlist visible to every user regardless of owner. */
        [BsonElement("isDefault")]
        public bool IsDefault { get; set; } = false;
    }

    /** One entry in a playlist: enough of a game to launch and label it. */
    public class PlaylistGame
    {
        /** Vendor game id, used when instructing a device to launch this entry. */
        [BsonElement("gameId")]
        public string GameId { get; set; }
        /** Display name captured at the time the entry was added. */
        [BsonElement("name")]
        public string Name { get; set; }
    }
}
