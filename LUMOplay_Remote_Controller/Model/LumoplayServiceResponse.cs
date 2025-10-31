using System;
using System.Collections.Generic;
using System.Text;

namespace LUMOplay_Remote_Controller.Model
{
    public class LumoplayServiceResponse
    {
        public int ID { get; set; }
        public bool IsLocal { get; set; }
        public string Name { get; set; }
        public int? NowPlayingIndex { get; set; }
        public List<SceneWrapper> Scenes { get; set; }
    }

    public class SceneWrapper
    {
        public int Duration { get; set; }
        public Scene Scene { get; set; }
    }

    public class Scene
    {
        public string Created { get; set; }
        public bool HideFromGUI { get; set; }
        public int ID { get; set; }
        public bool IsInstalled { get; set; }
        public string MinApplicationVersion { get; set; }
        public string Name { get; set; }
        public string ServerVersion { get; set; }
        public List<int> SetupTypes { get; set; }
        public List<Tag> Tags { get; set; }
    }

    public class Tag
    {
        public int tagID { get; set; }
        public string tagName { get; set; }
    }
}
