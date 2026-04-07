using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("lumoGames")]
    public class LumoPlayGame
    {
        [BsonId]
        public ObjectId Id { get; set; }
        [BsonElement("gameId")]
        public string GameId { get; set; }
        [BsonElement("name")]
        public string Name { get; set; }
        [BsonElement("imageFileName")]
        public string? ImageFileName { get; set; }
        [BsonElement("description")]
        public string? Description { get; set; }
        [BsonElement("tagIds")]
        public List<ObjectId>? TagIds { get; set; }
    }
}
