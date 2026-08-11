using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    /**
     * A label applied to games, stored in the "tags" collection. Tags nest via
     * <see cref="ParentTagId"/> to form a tree within their category, and a
     * user's allowed tags decide which games they may see.
     */
    [Collection("tags")]
    public class Tag
    {

        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }

        /** Id of the <see cref="Category"/> this tag belongs to. */
        [BsonElement("categoryId")]
        public ObjectId CategoryId { get; set; }

        /** Id of the tag this one nests under, or null for a root-level tag. */
        [BsonElement("parentTagId")]
        public ObjectId? ParentTagId { get; set; }

        /** Human-readable tag name shown in the UI. */
        [BsonElement("name")]
        public string Name { get; set; }

        /** URL-safe form of <see cref="Name"/>. */
        [BsonElement("slug")]
        public string Slug { get; set; }

        /** Sort position among sibling tags; lower values are listed first. */
        [BsonElement("displayOrder")]
        public int DisplayOrder { get; set; }

        /** Optional "#RRGGBB" swatch used to colour the tag chip. */
        [BsonElement("colorHex")]
        public string? ColorHex { get; set; }

        /** Whether the tag is shown to non-admin users. */
        [BsonElement("isVisible")]
        public bool IsVisible { get; set; } = true;

        /** UTC timestamp of when the tag was created. */
        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
