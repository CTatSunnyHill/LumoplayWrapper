using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;
using MongoDB.Bson.Serialization.Attributes; // <--- Required for mapping

namespace IntTech_Controller_Backend.Models
{
    [Collection("devices")]
    public class Device
    {

        [BsonId] 
        public ObjectId Id { get; set; }



        [BsonElement("name")]
        public string Name { get; set; }


        [BsonElement("ipAddress")]
        public string IpAddress { get; set; }


        [BsonElement("securityKey")]
        public string SecurityKey { get; set; }


        [BsonElement("location")]
        public string Location { get; set; }



        [BsonElement("status")]
        public string Status { get; set; } = "offline";


        [BsonElement("isPlaying")]
        public bool IsPlaying { get; set; }


        [BsonElement("currentLumoGameId")]
        public string? CurrentLumoGameId { get; set; } 

        [BsonElement("activePlaylist")]
        public ActivePlaylistState? ActivePlaylist { get; set; }

        [BsonElement("lastChecked")]
        public DateTime LastChecked { get; set; }
    }

    public class ActivePlaylistState
    {
        [BsonId]
        public ObjectId? PlaylistId { get; set; }

        [BsonElement("currentIndex")]
        public int CurrentIndex { get; set; } = 0;

        [BsonElement("startedAt")]
        public DateTime? StartedAt { get; set; }
    }
}