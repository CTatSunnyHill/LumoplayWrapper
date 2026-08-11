using MongoDB.Bson;

namespace IntTech_Controller_Backend.Models
{
    /**
     * A playlist returned to clients with its entries expanded into full
     * <see cref="Game"/> records, so the UI can render artwork and tags without
     * a second lookup per entry.
     */
    public class PlaylistDTO
    {
        /** Mongo document identifier of the playlist. */
        public ObjectId Id { get; set; }
        /** Display name of the playlist. */
        public string Name { get; set; }
        /** Id of the user who created the playlist. */
        public ObjectId OwnerId { get; set; }
        /** True for a shared playlist visible to every user. */
        public bool IsDefault { get; set; }
        /** The playlist's games in playback order, resolved from the library. */
        public List<Game> Games { get; set; }
    }
}
