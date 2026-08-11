using Android.App;
using Android.Runtime;

namespace LUMOplay_Remote_Controller
{
    /** Android application object; hands off to the shared MAUI startup. */
    [Application]
    public class MainApplication : MauiApplication
    {
        /**
         * <param name="handle">native peer handle supplied by the Android runtime</param>
         * <param name="ownership">how the managed side owns that handle</param>
         */
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        /**
         * Builds the shared MAUI app.
         *
         * <returns>the configured app from <see cref="MauiProgram"/></returns>
         */
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
