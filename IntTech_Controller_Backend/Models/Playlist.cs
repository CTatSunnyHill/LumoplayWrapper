using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("playlists")]
    public class Playlist
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("games")]
        public List<PlaylistGame> Games { get; set; }

        [BsonElement("ownerId")]
        public ObjectId OwnerId { get; set; }

        [BsonElement("isDefault")]
        public bool IsDefault { get; set; } = false;
    }

    public class PlaylistGame
    {
        [BsonElement("gameId")]
        public string GameId { get; set; }
        [BsonElement("name")]
        public string Name { get; set; }
    }
}
