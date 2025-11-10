using LUMOplay_Remote_Controller.Views;

namespace LUMOplay_Remote_Controller
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(DashboardPage), typeof(DashboardPage));
            Routing.RegisterRoute(nameof(GameLibraryPage), typeof(GameLibraryPage));
            Routing.RegisterRoute(nameof(PlaylistPage), typeof(PlaylistPage));
        }
    }
}
