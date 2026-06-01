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

        [BsonElement("locationId")]
        public ObjectId LocationId { get; set; }

        [BsonElement("lastPolled")]
        public DateTime LastPolled { get; set; }

        [BsonElement("inputs")]
        public List<ProjectorInput>? Inputs { get; set; }

        [BsonElement("currentInput")]
        public string? CurrentInput { get; set; }
    }

    public class ProjectorInput
    {
        [BsonElement("code")]
        public string Code { get; set; }

        [BsonElement("label")]
        public string? Label { get; set; }
    }
}
