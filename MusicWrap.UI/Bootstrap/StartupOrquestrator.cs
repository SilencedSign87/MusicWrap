using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using Serilog;
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
        await Task.Yield();

        var all = serviceProvider.GetServices<IStartupInitializer>().ToList();
        var background = all.OfType<IBackgroundStartupInitializer>().Cast<IStartupInitializer>().ToList();
        var foreground = all.Where(i => !background.Contains(i)).ToList();

        try
        {
            foreach (var initializer in foreground)
            {
                var sw = Stopwatch.StartNew();
                initializer.Initialize();
                bool isWarmup = initializer is StartupWarmup;
                String ServiceName = isWarmup ? ((StartupWarmup)initializer).Type.Name : initializer.GetType().Name;

                Log.Information("UI initializer | {Elapsed} ms | {Initializer}", sw.ElapsedMilliseconds, ServiceName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during UI initialization");
        }

        try
        {
            var windowManager = serviceProvider.GetRequiredService<WindowManagerService>();
            var modeToShow = serviceProvider.GetRequiredService<MusicWrapSettings>().LastWindowMode;

            splash?.Close(TimeSpan.FromMilliseconds(500));

            switch (modeToShow)
            {
                case PlayerMode.CompactPlayer:
                    windowManager.SwitchToCompactPlayer();
                    break;
                case PlayerMode.FullScreenPlayer:
                    windowManager.SwitchToFullScreenPlayer();
                    break;
                default:
                    windowManager.SwitchToMainPlayer();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during window initialization");
        }

        _ = Task.Run(() =>
        {
            foreach (var initializer in background)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    initializer.Initialize();
                    Log.Information("Background initializer | {Elapsed} ms | {Initializer}", sw.ElapsedMilliseconds, initializer.GetType().Name);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during background initialization of {Initializer}", initializer.GetType().Name);
                }
            }
        });

        Application.Current.Dispatcher.Invoke(
            () => serviceProvider.GetRequiredService<TrayService>().Initialize(),
            System.Windows.Threading.DispatcherPriority.Background
            );
    }
}
