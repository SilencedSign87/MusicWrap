using Jot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.DI;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Threading;
using MusicWrap.UI.Features.Activity.Viewmodel;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Features.Library.Views;
using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Playlist.ViewModels;
using MusicWrap.UI.Features.Playlist.Views;
using MusicWrap.UI.Features.Providers.ViewModels;
using MusicWrap.UI.Features.Providers.Views;
using MusicWrap.UI.Features.Settings.ViewModels;
using MusicWrap.UI.Features.Settings.Views;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Controls.ViewModel;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using MusicWrap.UI.Shell.ViewModel;
using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.ViewModels;
using Serilog;
using System.Windows;

namespace MusicWrap.UI.Bootstrap;

public static class ServiceRegistration
{
    public static void AddAppServices(this IServiceCollection services)
    {
        // Core implementation
        services.AddMusicWrapCore();

        // Logging
        services.AddLogging(loggingBuilder =>
                   {
                       loggingBuilder.ClearProviders();
                       loggingBuilder.AddSerilog(dispose: true);
                   });

        var Tracker = new Tracker();
        ConfigureJot(Tracker);

        services.AddSingleton(Tracker);

        services.AddSingleton<IUIDispatcher, UIDispatcher>();
        services.AddTransient<IwindowsImageService, ImageService>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<WindowManager>();
        services.AddTransient<IEditMetadataService, EditMetadataService>();
        services.AddSingleton<TrackActionService>();

        // View Models
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DirectoriesManagerViewModel>();
        services.AddTransient<SettingsGeneralViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<AlbumTracksViewModel>();
        services.AddTransient<PlaylistViewModel>();
        services.AddTransient<ServicePageViewModel>();
        services.AddTransient<PlaylistManagerViewModel>();
        services.AddTransient<SettingsIndexViewModel>();
        services.AddTransient<SettingsYoutubeViewModel>();
        services.AddTransient<YoutubeProviderViewModel>();
        services.AddSingleton<IndexingViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddTransient<DeviceViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddTransient<VolumeControlViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();
        services.AddTransient<TaskbarIconViewModel>();
        services.AddTransient<MetadataEditorViewModel>();
        services.AddTransient<DJControlViewModel>();
        services.AddTransient<LibraryEntryDetailPanelViewModel>();
        services.AddTransient<LibraryEntryAlbumViewModel>();
        services.AddTransient<LibraryEntryTracksViewModel>();
        services.AddTransient<ActivityCenterViewModel>();
        services.AddTransient<TrackInformationViewModel>();
        services.AddTransient<NowPlayingViewModel>();
        services.AddTransient<FullscreenWindowViewModel>();

        // UI
        services.AddTransient<MainWindow>();
        services.AddTransient<CompactPlayer>();
        services.AddTransient<TrayFlyoutWindow>();
        services.AddTransient<FullScreenWindow>();

        services.AddTransient<SettingsWindow>();
        services.AddTransient<SettingsGeneralPage>();
        services.AddTransient<SettingsDirectoriesManagerPage>();
        services.AddTransient<DevicePage>();
        services.AddTransient<AboutPage>();
        services.AddTransient<SettingsYoutubeProviderPage>();

        services.AddTransient<IndexingWindow>();
        services.AddTransient<MetadataEditorWindow>();
        services.AddTransient<NewPlaylistWindow>();
        services.AddTransient<QueueListPage>();
        services.AddTransient<TrackInformationPage>();

        services.AddTransient<PlayerPage>();
        services.AddTransient<LibraryPage>();
        services.AddTransient<PlaylistPage>();
        services.AddTransient<ServicesPage>();
        services.AddTransient<NowPlayingPage>();

        services.AddTransient<YoutubeProviderPage>();
    }

    private static void ConfigureJot(Tracker tracker)
    {
        tracker.Configure<Window>()
            .Id(w => w.Name)
            .Properties(w => new { w.Top, w.Left, w.Width, w.Height, w.WindowState })
            .PersistOn(nameof(Window.Closing));
    }
}
