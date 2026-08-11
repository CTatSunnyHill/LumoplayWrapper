using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    /**
     * A PJLink-controllable projector, stored in the "projectors" collection.
     * Separate from <see cref="Device"/>: a projector displays the picture and
     * is driven over PJLink, whereas a device runs the games themselves.
     */
    [Collection("projectors")]
    public class Projector
    {
        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }

        /** Human-readable projector name shown in the UI. */
        [BsonElement("name")]
        public string Name { get; set; }

        /** Host or IP address of the projector on the local network. */
        [BsonElement("ipAddress")]
        public string IpAddress { get; set; }

        /** PJLink TCP port; 4352 is the protocol default. */
        [BsonElement("port")]
        public int Port { get; set; } = 4352;

        /** PJLink password, or null when the projector needs no authentication. */
        [BsonElement("password")]
        public string? Password { get; set; }

        /** Power state as of <see cref="LastPolled"/>, or "unknown" if never reached. */
        [BsonElement("status")]
        public string Status { get; set; } = "unknown";

        /** Id of the <see cref="Location"/> this projector sits in. */
        [BsonElement("locationId")]
        public ObjectId LocationId { get; set; }

        /** UTC timestamp of the last status poll of this projector. */
        [BsonElement("lastPolled")]
        public DateTime LastPolled { get; set; }

        /** Input sources the projector reported, or null before the first poll. */
        [BsonElement("inputs")]
        public List<ProjectorInput>? Inputs { get; set; }

        /** PJLink code of the selected input, or null when unknown. */
        [BsonElement("currentInput")]
        public string? CurrentInput { get; set; }
    }

    /** One selectable input on a projector, with the name an admin gave it. */
    public class ProjectorInput
    {
        /** Two-character PJLink input code, such as "31" for HDMI 1. */
        [BsonElement("code")]
        public string Code { get; set; } = string.Empty;

        /** Admin-supplied label, or null to fall back to the raw code. */
        [BsonElement("label")]
        public string? Label { get; set; }
    }
}
