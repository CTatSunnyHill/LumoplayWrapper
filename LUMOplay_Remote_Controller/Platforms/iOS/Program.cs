using ObjCRuntime;
using UIKit;

namespace LUMOplay_Remote_Controller
{
    /** iOS entry point. */
    public class Program
    {
        /**
         * Starts UIKit against <see cref="AppDelegate"/>, which in turn builds
         * the shared MAUI app.
         *
         * <param name="args">command-line arguments passed through to UIKit</param>
         */
        // This is the main entry point of the application.
        static void Main(string[] args)
        {
            // if you want to use a different Application Delegate class from "AppDelegate"
            // you can specify it here.
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
    }
}
