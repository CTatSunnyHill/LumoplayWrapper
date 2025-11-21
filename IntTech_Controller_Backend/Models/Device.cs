using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("devices")]
    public class Device
    {
        public ObjectId id { get; set; }
        public string name { get; set; }
        public string ipAddress { get; set; }
        public string securityKey { get; set; }
        public LumoPlayGame? currentGame { get; set; }
        public bool isConnected { get; set; }
        public DateTime lastSeen { get; set; }
        public bool isPlaying { get; set; }
        

    }
}
