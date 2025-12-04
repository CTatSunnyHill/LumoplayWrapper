using MongoDB.Bson;

namespace IntTech_Controller_Backend.Models
{
    public class LumoPlayPlaylistDTO
    {
        public ObjectId Id { get; set; }
        public string PlaylistId { get; set; }
        public string Name { get; set; }
        public List<LumoPlayGame> Games { get; set; }
    }
}
