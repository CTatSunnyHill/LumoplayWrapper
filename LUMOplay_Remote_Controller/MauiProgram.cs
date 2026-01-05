using CommunityToolkit.Maui;
using LUMOplay_Remote_Controller.Services;
using LUMOplay_Remote_Controller.ViewModels;
using LUMOplay_Remote_Controller.Views;
using Microsoft.Extensions.Logging;


namespace LUMOplay_Remote_Controller
{
    public static class MauiProgram
    {
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
