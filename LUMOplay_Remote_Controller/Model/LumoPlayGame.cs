using System;
using System.Collections.Generic;
using System.Text;

namespace LUMOplay_Remote_Controller.Model
{

    /**
     * A LUMOplay game with its metadata, as loaded from the bundled games.json
     * or fetched from the controller backend.
     */
    public class LumoplayGame
    {

        /** Vendor game id, used when instructing a device to launch this title. */
        public string GameId { get; set; }

        /** Display name of the game. */
        public string Name { get; set; }

        /** URL or local path to the game's thumbnail image. */
        public string ImageUrl { get; set; }

        /** Blurb shown on the game card. */
        public string Description { get; set; }

        /** Surface the game is meant to be projected onto: "Wall" or "Floor". */
        public string LocationType { get; set; }
    }
}
