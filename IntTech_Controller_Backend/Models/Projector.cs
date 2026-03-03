using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("projectors")]
    public class Projector
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("ipAddress")]
        public string IpAddress { get; set; }

        [BsonElement("port")]
        public int Port { get; set; } = 4352;

        [BsonElement("password")]
        public string? Password { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } = "unknown";

        [BsonElement("location")]
        public string Location { get; set; }

        [BsonElement("lastPolled")]
        public DateTime LastPolled { get; set; }
    }
}
