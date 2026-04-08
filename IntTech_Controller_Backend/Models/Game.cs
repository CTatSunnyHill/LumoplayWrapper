using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("games")]
    public class Game
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

        [BsonElement("platform")]
        public string Platform { get; set; } = "lumoplay";
    }


    public static class PlatformTypes
    {
        public const string LumoPlay = "lumoplay";
        public const string VR = "vr";
        public const string NintendoSwitch = "switch";

        public static readonly HashSet<string> All = new()
        {
            LumoPlay, VR, NintendoSwitch
        };

        public static bool IsValid(string platform)
        {
            return All.Contains(platform);
        }
        
    }
}
