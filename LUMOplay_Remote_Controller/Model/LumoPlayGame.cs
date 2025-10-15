using System;
using System.Collections.Generic;
using System.Text;

namespace LUMOplay_Remote_Controller.Model
{

    /// Represents a LUMOplay game with its metadata.
    public class LumoplayGame
    {

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
