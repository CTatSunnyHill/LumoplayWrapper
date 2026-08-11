using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    /**
     * A physical room or site, stored in the "locations" collection. Devices and
     * projectors belong to one, and a user's allowed locations decide what they see.
     */
    [Collection("locations")]
    public class Location
    {
        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }

        /** Human-readable location name shown in the UI. */
        [BsonElement("name")]
        public string Name { get; set; }
    }
}
