using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("devices")]
    public class Device
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public string SecurityKey { get; set; }
        public LumoPlayGame? CurrentGame { get; set; }
        public bool IsConnected { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsPlaying { get; set; }
        

    }
}
