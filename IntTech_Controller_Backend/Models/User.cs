using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;


namespace IntTech_Controller_Backend.Models
{
    [Collection("users")]
    public class User
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("username")]
        public string Username { get; set; }

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; }

        [BsonElement("role")]
        public string Role { get; set; }

        [BsonElement("allowedLocations")]
        public List<string> AllowedLocations { get; set; } = new();

    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password{ get; set; }
    }
}
