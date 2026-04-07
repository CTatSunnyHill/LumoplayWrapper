using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("tags")]
    public class Tag
    {

        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("categoryId")]
        public ObjectId CategoryId { get; set; }

        [BsonElement("parentTagId")]
        public ObjectId? ParentTagId { get; set; }

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("slug")]
        public string Slug { get; set; }

        [BsonElement("displayOrder")]
        public int DisplayOrder { get; set; }

        [BsonElement("colorHex")]
        public string? ColorHex { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
