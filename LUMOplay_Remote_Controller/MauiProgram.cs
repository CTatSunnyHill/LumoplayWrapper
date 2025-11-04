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
                });
            builder.Services.AddSingleton<DashboardViewModel>();
            builder.Services.AddSingleton<DeviceManager>();
            builder.Services.AddTransient<DashboardPage>();


#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
