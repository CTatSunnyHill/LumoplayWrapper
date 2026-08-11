using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    /**
     * A playable title in the library, stored in the "games" collection.
     * Distinguish the two identifiers: <see cref="Id"/> is ours, whereas
     * <see cref="GameId"/> is what the device's platform recognises.
     */
    [Collection("games")]
    public class Game
    {
        /** Mongo document identifier. */
        [BsonId]
        public ObjectId Id { get; set; }

        /** Vendor game id, used when instructing a device to launch this title. */
        [BsonElement("gameId")]
        public string GameId { get; set; }

        /** Display name of the game. */
        [BsonElement("name")]
        public string Name { get; set; }

        /** File name of the cover image in game storage, or null for none. */
        [BsonElement("imageFileName")]
        public string? ImageFileName { get; set; }

        /** Optional blurb shown on the game card. */
        [BsonElement("description")]
        public string? Description { get; set; }

        /** Ids of the tags applied to this game; also gates per-user visibility. */
        [BsonElement("tagIds")]
        public List<ObjectId>? TagIds { get; set; }

        /** Platform this title runs on; see <see cref="PlatformTypes"/>. */
        [BsonElement("platform")]
        public string Platform { get; set; } = "lumoplay";

        /** File name of the one-pager PDF in game storage, or null for none. */
        [BsonElement("onePagerFileName")]
        public string? OnePagerFileName { get; set; }
    }


    /** The platform identifiers a <see cref="Game"/> may be tagged with. */
    public static class PlatformTypes
    {
        /** LUMOplay projector titles: the only platform the backend can drive remotely. */
        public const string LumoPlay = "lumoplay";
        /** Virtual-reality titles, catalogued but launched manually. */
        public const string VR = "vr";
        /** Nintendo Switch titles, catalogued but launched manually. */
        public const string NintendoSwitch = "switch";

        /** Every recognised platform identifier. */
        public static readonly HashSet<string> All = new()
        {
            LumoPlay, VR, NintendoSwitch
        };

        /**
         * Determines whether a platform identifier is one this system recognises.
         *
         * <param name="platform">the identifier to check, case-sensitive</param>
         * <returns>true when the identifier appears in <see cref="All"/></returns>
         */
        public static bool IsValid(string platform)
        {
            return All.Contains(platform);
        }

    }
}
