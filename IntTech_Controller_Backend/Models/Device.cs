using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;
using MongoDB.Bson.Serialization.Attributes; // <--- Required for mapping

namespace IntTech_Controller_Backend.Models
{
    /**
     * A LUMOplay projector unit the backend can drive, stored in the "devices"
     * collection. Playback fields mirror the last state observed on the unit
     * itself, so they may lag until the next poll refreshes them.
     */
    [Collection("devices")]
    public class Device
    {

        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }



        /** Human-readable device name shown in the UI. */
        [BsonElement("name")]
        public string Name { get; set; }


        /** Host or IP address of the LUMOplay service on the local network. */
        [BsonElement("ipAddress")]
        public string IpAddress { get; set; }


        /** Shared secret required by the LUMOplay API on this unit. Never returned to clients. */
        [BsonElement("securityKey")]
        public string SecurityKey { get; set; }


        /** Id of the <see cref="Location"/> this device sits in; drives per-user access. */
        [BsonElement("locationId")]
        public ObjectId LocationId { get; set; }



        /** Reachability as of <see cref="LastChecked"/>: "online" or "offline". */
        [BsonElement("status")]
        public string Status { get; set; } = "offline";


        /** Whether a game was playing at the last poll. */
        [BsonElement("isPlaying")]
        public bool IsPlaying { get; set; }


        /** Vendor id of the game on screen, or null when nothing is playing. */
        [BsonElement("currentLumoGameId")]
        public string? CurrentLumoGameId { get; set; }

        /** Playlist currently running on this device, or null when none is active. */
        [BsonElement("activePlaylist")]
        public ActivePlaylistState? ActivePlaylist { get; set; }

        /** UTC timestamp of the last successful poll of this device. */
        [BsonElement("lastChecked")]
        public DateTime LastChecked { get; set; }
    }

    /** Where a device has got to in the playlist it is running. */
    public class ActivePlaylistState
    {
        /** Id of the playlist being run, or null if it has since been deleted. */
        [BsonId]
        public ObjectId? PlaylistId { get; set; }

        /** Zero-based index of the playlist entry currently on screen. */
        [BsonElement("currentIndex")]
        public int CurrentIndex { get; set; } = 0;

        /** UTC timestamp of when the playlist was started, or null if unknown. */
        [BsonElement("startedAt")]
        public DateTime? StartedAt { get; set; }
    }
}
