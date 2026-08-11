using LUMOplay_Remote_Controller.Views;

namespace LUMOplay_Remote_Controller
{
    /**
     * Navigation shell. Registers each page under its own type name, so code can
     * navigate with nameof(SomePage) rather than a hard-coded route string.
     */
    public partial class AppShell : Shell
    {
        /** Builds the shell and registers the navigable routes. */
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(DashboardPage), typeof(DashboardPage));
            Routing.RegisterRoute(nameof(GameLibraryPage), typeof(GameLibraryPage));
            Routing.RegisterRoute(nameof(PlaylistPage), typeof(PlaylistPage));
        }
    }
}
