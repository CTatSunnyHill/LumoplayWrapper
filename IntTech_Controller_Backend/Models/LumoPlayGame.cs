using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;

namespace IntTech_Controller_Backend.Models
{
    
    public class LumoPlayGame
    {
        public ObjectId Id { get; set; }

        /// Gets or sets the unique identifier of the game.
        public string GameId { get; set; }


        /// Gets or sets the display name of the game.
        public string Name { get; set; }


        /// Gets or sets the URL or local path to the game's thumbnail image.
        public string ImageUrl { get; set; }


        /// Gets or sets the description of the game.
        public string Description { get; set; }


        /// Gets or sets the location type where the game is meant to be played (Wall/Floor).
        public string LocationType { get; set; }
    }
}
