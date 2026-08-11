using Foundation;

namespace LUMOplay_Remote_Controller
{
    /** iOS application delegate; hands off to the shared MAUI startup. */
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        /**
         * Builds the shared MAUI app.
         *
         * <returns>the configured app from <see cref="MauiProgram"/></returns>
         */
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
