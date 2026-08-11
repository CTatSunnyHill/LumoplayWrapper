using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    /**
     * A top-level grouping of tags (for example "Difficulty" or "Body Part"),
     * stored in the "categories" collection. Tags reference their category by id.
     */
    [Collection("categories")]
    public class Category
    {
        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }

        /** Human-readable category name shown in the UI. */
        [BsonElement("name")]
        public string Name { get; set; }

        /** URL-safe form of <see cref="Name"/>, unique across categories. */
        [BsonElement("slug")]
        public string Slug { get; set; }

        /** Optional explanatory text for administrators. */
        [BsonElement("description")]
        public string? Description { get; set; }

        /** Sort position among categories; lower values are listed first. */
        [BsonElement("displayOrder")]
        public int DisplayOrder { get; set; }

        /** UTC timestamp of when the category was created. */
        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }


}
