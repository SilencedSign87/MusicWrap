using Microsoft.Extensions.Logging;
using MusicWrap.Core.DI;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Mobile.Bootstrap;
using MusicWrap.Mobile.Domain;

namespace MusicWrap.Mobile
{
    public static class MauiProgram
    {
        public static IServiceProvider Services { get; private set; } = null!;
        public static MauiApp CreateMauiApp()
        {
            MusicWrapDirectories.EnsureCreated();

            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddMusicWrapCore();
            builder.Services.AddMauiServices();

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            var app = builder.Build();
            Services = app.Services;

            _ = Task.Run(() => MauiStartup.InitializeAsync(app.Services));

            return app;
        }
    }
}
