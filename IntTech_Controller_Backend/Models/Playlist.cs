using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("playlists")]    
    public class Playlist
    {  
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public List<LumoPlayGame> Games { get; set; }

    }
}
