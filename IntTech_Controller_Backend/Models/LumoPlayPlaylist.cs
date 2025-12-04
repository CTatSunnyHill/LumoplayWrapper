using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("lumoPlaylists")]    
    public class LumoPlayPlaylist
    {  
        public ObjectId Id { get; set; }
        public string PlaylistId { get; set; }
        public string Name { get; set; }
        public List<LumoPlaylistGame> Games { get; set; }

    }

    public class LumoPlaylistGame 
    {
        public string GameId { get; set; }
        public string Name { get; set; }
    }
}
