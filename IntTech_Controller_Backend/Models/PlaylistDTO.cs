using MongoDB.Bson;

namespace IntTech_Controller_Backend.Models
{
    public class PlaylistDTO
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public ObjectId OwnerId { get; set; }
        public bool IsDefault { get; set; }
        public List<Game> Games { get; set; }
    }
}
