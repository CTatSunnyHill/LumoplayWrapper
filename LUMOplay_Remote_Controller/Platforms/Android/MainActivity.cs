using Android.App;
using Android.Content.PM;
using Android.OS;

namespace LUMOplay_Remote_Controller
{
    /**
     * Android launcher activity. The ConfigurationChanges list keeps the
     * activity alive through rotation and window resizing, so MAUI relayouts
     * instead of Android recreating it.
     */
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
