using CommunityToolkit.Maui;
using LUMOplay_Remote_Controller.Services;
using LUMOplay_Remote_Controller.ViewModels;
using LUMOplay_Remote_Controller.Views;
using Microsoft.Extensions.Logging;


namespace LUMOplay_Remote_Controller
{
    /** Composition root for the MAUI app: fonts, services, and pages. */
    public static class MauiProgram
    {
        /**
         * Builds the configured application.
         *
         * Lifetimes are deliberate: the API client and the managers are
         * singletons because they hold shared device and playlist state, and the
         * dashboard is a singleton so returning to it preserves what the user
         * was looking at. The other pages are transient and rebuilt per visit.
         *
         * <returns>the assembled MauiApp, ready for the platform head to run</returns>
         */
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Supplies the play/pause glyphs emitted by BoolToPlayPauseIconConverter.
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<LumoPlayApiClient>();
            builder.Services.AddSingleton<DeviceManager>();
            builder.Services.AddSingleton<PlaylistManager>();
            builder.Services.AddSingleton<DashboardViewModel>();
            builder.Services.AddSingleton<DashboardPage>();

            builder.Services.AddTransient<GameLibraryViewModel>();
            builder.Services.AddTransient<GameLibraryPage>();
            builder.Services.AddTransient<PlaylistViewModel>();
            builder.Services.AddTransient<PlaylistPage>();


            return builder.Build();
        }
    }
}
