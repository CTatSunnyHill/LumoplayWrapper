using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    [Collection("lumoGames")]
    public class LumoPlayGame
    {
        public ObjectId Id { get; set; }
        public string GameId { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string Description { get; set; }
        public string LocationType { get; set; }
    }
}
