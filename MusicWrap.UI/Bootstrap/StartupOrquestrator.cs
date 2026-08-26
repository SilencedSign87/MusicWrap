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

        services.AddAppServices();

        return services.BuildServiceProvider();
    }

    public static async Task InitializeAsync(IServiceProvider serviceProvider, SplashScreen? splash = null)
    {
        int windowToShow = 0;

        try
        {
            var musicLibrary = serviceProvider.GetService<MusicLibrary>();
            var userSettings = serviceProvider.GetRequiredService<MusicWrapSettings>();
            var player = serviceProvider.GetRequiredService<IMusicPlayerService>();
            var trayService = serviceProvider.GetService<ITrayService>();
            var hotkeyService = serviceProvider.GetRequiredService<GlobalHotkeyService>();
            var uiDispatcher = serviceProvider.GetRequiredService<IUIDispatcher>();
            var themeService = serviceProvider.GetRequiredService<ThemeService>();
            var windowManager = serviceProvider.GetRequiredService<WindowManagerService>();
            var taskbarController = serviceProvider.GetRequiredService<TaskbarController>();
            themeService.Initialize();

            trayService?.Initialize();

            player.LoadInitialState();

            // Keyboard Register
            hotkeyService.MediaKeyPressed += key =>
            {
                uiDispatcher.Invoke(() =>
                {
                    switch (key)
                    {
                        case MediaKey.PlayPause:
                            if (player.IsPlaying) player.Pause();
                            else player.Play();
                            break;
                        case MediaKey.Next:
                            player.Next();
                            break;
                        case MediaKey.Previous:
                            player.Previous();
                            break;
                        case MediaKey.Stop:
                            player.Stop();
                            break;
                    }
                });
            };

            // taskbar thumbnail buttons
            taskbarController.PreviousRequested += () =>
            {
                Application.Current.Dispatcher.Invoke(() => player.Previous());
            };

            taskbarController.PlayPauseRequested += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (player.IsPlaying) player.Pause();
                    else player.Play();
                });
            };

            taskbarController.NextRequested += () =>
            {
                Application.Current.Dispatcher.Invoke(() => player.Next());
            };    

            // Library cache initialization (preserve previous defaults)
            var listBy = userSettings.Library.EntryType;
            var ascending = userSettings.Library.EntryListAscending;

            var libraryCache = serviceProvider.GetRequiredService<ILibraryService>();
            //await libraryCache.InitializeAsync(listBy, ascending);

            // Pre-resolve important VMs / services
            serviceProvider.GetService<PlayerViewModel>();

            windowToShow = (int)userSettings.LastWindowMode;

            // Ensure save orchestration/coordinator are created (they may be used on exit)
            serviceProvider.GetService<ISaveCoordinator>();

            // If keep in tray, ensure tray is initialized (safe to call again)
            if (userSettings.KeepAppInTray)
            {
                try { trayService?.SetEnabled(true); } catch { }
            }

            RunIntegrityCheck(serviceProvider);

        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during application initialization");
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var wm = serviceProvider.GetRequiredService<WindowManagerService>();
                try
                {
                    splash?.Close(TimeSpan.FromSeconds(0.5));
                }
                catch { }

                try
                {
                    if (windowToShow == 1)
                    {
                        wm.SwitchToCompactPlayer();
                    }
                    else
                    {
                        wm.SwitchToMainPlayer();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error showing main window");
                }
            });
        }

    }

    private static void RunIntegrityCheck(IServiceProvider serviceProvider)
    {
        try
        {
            var integrity = serviceProvider.GetRequiredService<ILibraryIntegrityService>();

            integrity.Verify();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during library integrity check");
        }

    }

}
