using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LUMOplay_Remote_Controller.WinUI
{
    /**
     * Provides application-specific behavior to supplement the default Application class.
     * Windows head of the app; hands off to the shared MAUI startup.
     */
    public partial class App : MauiWinUIApplication
    {
        /**
         * Initializes the singleton application object.  This is the first line of authored code
         * executed, and as such is the logical equivalent of main() or WinMain().
         */
        public App()
        {
            this.InitializeComponent();
        }

        /**
         * Builds the shared MAUI app.
         *
         * <returns>the configured app from <see cref="MauiProgram"/></returns>
         */
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
