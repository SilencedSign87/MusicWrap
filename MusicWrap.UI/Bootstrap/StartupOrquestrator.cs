using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
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

        void ApplyTrayBehavior(bool keepInTray, ITrayService? tray)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.ShutdownMode = keepInTray
                    ? ShutdownMode.OnExplicitShutdown
                    : ShutdownMode.OnLastWindowClose;

                tray?.SetEnabled(keepInTray);

                if (!keepInTray)
                {
                    var current = App.CurrentWindow;

                    if (current is not null
                            && current.IsLoaded
                            && !current.Dispatcher.HasShutdownStarted
                            && !current.Dispatcher.HasShutdownFinished
                            && !current.IsVisible)
                    {
                        try
                        {
                            current.Show();
                            current.Activate();
                        }
                        catch { }
                    }
                }
            });
        }

        try
        {
            var musicLibrary = serviceProvider.GetService<MusicLibrary>();
            var userSettings = serviceProvider.GetRequiredService<UserSettings>();
            var player = serviceProvider.GetRequiredService<IMusicPlayerService>();
            var trayService = serviceProvider.GetService<ITrayService>();

            // subcribe to tray behavior changes
            void OnUserSettingsChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(UserSettings.KeepAppInTray))
                {
                    ApplyTrayBehavior(userSettings.KeepAppInTray, trayService);
                }
            }

            userSettings.PropertyChanged += OnUserSettingsChanged;

            // initial behavior
            ApplyTrayBehavior(userSettings.KeepAppInTray, trayService);

            // Library service initialization (preserve previous defaults)
            var listBy = string.IsNullOrWhiteSpace(userSettings.LibraryListBy)
                ? "Album"
                : userSettings.LibraryListBy;
            var ascending = userSettings.LibraryAscending;

            //var libraryService = serviceProvider.GetRequiredService<ILibraryService>();
            //libraryService.Initialize(listBy, ascending);

            // Pre-resolve important VMs / services
            serviceProvider.GetService<PlayerViewModel>();

            windowToShow = (int)userSettings.LastWindowMode;

            // Ensure save orchestration/coordinator are created (they may be used on exit)
            serviceProvider.GetService<ISaveCoordinator>();
            serviceProvider.GetService<ISaveOrchestration>();

            // If keep in tray, ensure tray is initialized (safe to call again)
            if (userSettings.KeepAppInTray)
            {
                try { trayService?.SetEnabled(true); } catch { }
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
                    if (windowToShow == 1)
                    {
                        App.ShowCompactPlayer();
                    }
                    else
                    {
                        App.ShowMain();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error showing main window");
                }
            });
        }

    }

}
