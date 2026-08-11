using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;


namespace IntTech_Controller_Backend.Models
{
    /**
     * An account that can sign in to the controller, stored in the "users"
     * collection. What a non-admin user may reach is the intersection of their
     * allowed locations (which devices) and allowed tags (which games).
     */
    [Collection("users")]
    public class User
    {
        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }

        /** Login name, unique across users. */
        [BsonElement("username")]
        public string Username { get; set; }

        /** BCrypt hash of the password. Never returned to clients. */
        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; }

        /** Access level. "Admin" grants unrestricted access; any other value is a standard user. */
        [BsonElement("role")]
        public string Role { get; set; }

        /** Counter bumped to invalidate existing tokens; see SessionVersionMiddleware. */
        [BsonElement("sessionVersion")]
        public int SessionVersion { get; set; }

        /** Locations this user may control; ignored for admins, who see all. */
        [BsonElement("allowedLocationsIds")]
        public List<ObjectId> AllowedLocationsIds { get; set; } = new();

        /** Tags gating which games this user may see; ignored for admins. */
        [BsonElement("allowedTagIds")]
        public List<ObjectId> AllowedTagIds { get; set; } = new();

    }

    /** Credentials posted to the login endpoint. */
    public class LoginRequest
    {
        /** Login name of the account. */
        public string Username { get; set; }
        /** Plain-text password, checked against the stored hash. */
        public string Password{ get; set; }
    }
}
