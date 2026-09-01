using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.ViewModels;
using Serilog;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace MusicWrap.UI.Bootstrap;

public static class StartupOrquestrator
{
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        MusicWrapDirectories.EnsureCreated();

        // configure logging
        Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .Enrich.FromLogContext()
                        .Enrich.WithThreadId()
                        .Enrich.WithMachineName()
                        .Enrich.WithEnvironmentUserName()
                        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}")
                        .WriteTo.File(
                            path: System.IO.Path.Combine(MusicWrapDirectories.LogsDirectory, "log-.txt"),
                            rollingInterval: RollingInterval.Day,
                            outputTemplate:
                            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] " +
                            "[Machine:{MachineName}] [User:{EnvironmentUserName}] [Thread:{ThreadId}] " +
                            "{Message:lj}{NewLine}{Exception}"
                        )
                        .CreateLogger();

        Log.Information("Starting application...");

        services.AddAppServices();

        return services.BuildServiceProvider();
    }

    public static async Task InitializeAsync(IServiceProvider serviceProvider, SplashScreen? splash = null)
    {
        WindowManagerService? windowManager = null;
        PlayerMode modeToShow = PlayerMode.MainPlayer;

        try
        {
            windowManager = serviceProvider.GetRequiredService<WindowManagerService>();
            modeToShow = serviceProvider.GetRequiredService<MusicWrapSettings>().LastWindowMode;

            foreach (var initializer in serviceProvider.GetServices<IStartupInitializer>())
            {
                Log.Information("Initializing service {Initializer}...", initializer.GetType().Name);

                initializer.Initialize();
            }

        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during application initialization");
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    splash?.Close(TimeSpan.FromSeconds(0.5));
                }
                catch { }

                try
                {
                    if (modeToShow == PlayerMode.CompactPlayer)
                    {
                        windowManager?.SwitchToCompactPlayer();
                    }
                    else
                    {
                        windowManager?.SwitchToMainPlayer();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error showing main window");
                }
            });

            // Initialize the tray icon in the background
            Application.Current.Dispatcher.Invoke(
                () => serviceProvider.GetRequiredService<TrayService>().Initialize(),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
