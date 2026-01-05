using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("lumoPlaylists")]    
    public class LumoPlayPlaylist
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("games")]
        public List<LumoPlaylistGame> Games { get; set; }

    }

    public class LumoPlaylistGame 
    {
        [BsonElement("gameId")]
        public string GameId { get; set; }
        [BsonElement("name")]
        public string Name { get; set; }
    }
}
