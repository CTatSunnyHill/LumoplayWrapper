using System;
using System.Collections.Generic;
using System.Text;

namespace LUMOplay_Remote_Controller.Model
{
    /**
     * A playlist as reported by the LUMOplay service running on a device.
     * Property names mirror the vendor's JSON, so they are deserialised as-is
     * rather than renamed to match this project's conventions.
     */
    public class LumoplayServiceResponse
    {
        /** Vendor playlist identifier. */
        public int ID { get; set; }
        /** True when the playlist is stored on the device rather than the vendor cloud. */
        public bool IsLocal { get; set; }
        /** Display name of the playlist. */
        public string Name { get; set; }
        /** Index into <see cref="Scenes"/> of the scene on screen, or null when stopped. */
        public int? NowPlayingIndex { get; set; }
        /** The playlist's scenes, in playback order. */
        public List<SceneWrapper> Scenes { get; set; }
    }

    /** A scene together with the playlist-specific settings applied to it. */
    public class SceneWrapper
    {
        /** How long the scene runs, in seconds, before the playlist advances. */
        public int Duration { get; set; }
        /** The scene itself. */
        public Scene Scene { get; set; }
    }

    /** A single playable LUMOplay title as described by the device. */
    public class Scene
    {
        /** Vendor-formatted creation timestamp. */
        public string Created { get; set; }
        /** True when the vendor marks this scene as not for display in pickers. */
        public bool HideFromGUI { get; set; }
        /** Vendor scene identifier, used when launching the scene. */
        public int ID { get; set; }
        /** True when the scene's assets are present on the device and it can be played. */
        public bool IsInstalled { get; set; }
        /** Lowest LUMOplay application version able to run this scene. */
        public string MinApplicationVersion { get; set; }
        /** Display name of the scene. */
        public string Name { get; set; }
        /** Version of the scene held by the vendor server. */
        public string ServerVersion { get; set; }
        /** Vendor codes for the room setups this scene supports. */
        public List<int> SetupTypes { get; set; }
        /** Vendor-side tags on the scene; unrelated to this system's own tags. */
        public List<Tag> Tags { get; set; }
    }

    /** A vendor-side label attached to a <see cref="Scene"/>. */
    public class Tag
    {
        /** Vendor tag identifier. */
        public int tagID { get; set; }
        /** Display name of the tag. */
        public string tagName { get; set; }
    }
}
