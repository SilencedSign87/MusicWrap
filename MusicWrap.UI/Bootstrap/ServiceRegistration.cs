using Jot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Metadata;
using MusicWrap.Core.Queue;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Playlists;
using MusicWrap.Core.Services.Providers.Youtube;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Core.Sources.Providers.Local;
using MusicWrap.Core.Sources.Providers.Queue;
using MusicWrap.Core.Sources.Providers.Runtime;
using MusicWrap.Core.Sources.Providers.Youtube;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library;
using MusicWrap.Data.Player;
using MusicWrap.Data.Playlist;
using MusicWrap.Data.User;
using MusicWrap.UI.Features.Activity.Services;
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
        // Logging
        services.AddLogging(loggingBuilder =>
                   {
                       loggingBuilder.ClearProviders();
                       loggingBuilder.AddSerilog(dispose: true);
                   });

        // window state
        var Tracker = new Tracker();
        ConfigureJot(Tracker);

        services.AddSingleton(Tracker);

        services.AddSingleton<ITrayService, TrayService>();

        //Data layer
        services.AddSingleton<ILibraryRepository, LibraryRepository>();
        services.AddSingleton(sp => sp.GetRequiredService<ILibraryRepository>().Load());// Provide Music library
        services.AddSingleton<IPlaybackRepository, PlaybackRepository>();
        services.AddSingleton(sp => sp.GetRequiredService<IPlaybackRepository>().Load()); // Provide Queue settings
        services.AddSingleton<IUserSettingsRepository, UserSettingsRepository>();
        services.AddSingleton(sp => sp.GetRequiredService<IUserSettingsRepository>().Load()); // Provide user settings
        services.AddSingleton<IPlaylistRepository, PlaylistRepository>();
        services.AddSingleton(sp => sp.GetRequiredService<IPlaylistRepository>().Load()); // Provide playlist data
        services.AddSingleton<ILibraryService, LibraryService>();
        services.AddSingleton<ActivityService>();

        // Services
        services.AddSingleton<IUIDispatcher, UIDispatcher>();
        services.AddTransient<IImageService, ImageService>();
        services.AddSingleton<TracksContextMenuService>();
        services.AddTransient<ILibraryScanner, LibraryScanner>();
        services.AddTransient<ILibraryIndexer, LibraryIndexer>();
        services.AddSingleton<ILibraryCacheStore, LibraryCacheStoreAdapter>();
        services.AddSingleton<ISaveOrchestration, SaveOrchestration>();
        services.AddSingleton<ISaveStateProvider>(sp => (ISaveStateProvider)sp.GetRequiredService<ISaveOrchestration>());
        services.AddSingleton<ISaveCoordinator, SaveScheduler>();
        services.AddSingleton<IMetadataAutocompleteService, MetadataAutocompleteService>();
        services.AddTransient<IEditMetadataService, EditMetadataService>();
        services.AddSingleton<IQueueManager, QueueManager>();
        services.AddSingleton<ILibraryIntegrityService, LibraryIntegrityService>();
        services.AddSingleton<SearchService>();
        services.AddSingleton<ISearchQueryProvider, SearchService>(sp => sp.GetRequiredService<SearchService>());
        services.AddSingleton<WindowManager>();

        // Providers
        services.AddTransient<IQueueItemPlaybackResolver, QueueItemPlaybackResolver>();
        services.AddSingleton<ITrackSourceProvider, LocalTrackSourceProvider>();

        services.AddTransient<IYoutubeResolutionService, YoutubeResolutionService>();
        services.AddTransient<IYoutubeStagingService, YoutubeStagingService>();
        services.AddTransient<IYoutubeSearchService, YoutubeSearchService>();
        services.AddTransient<IYoutubeLibraryIndexingService, YoutubeLibraryIndexingService>();
        services.AddTransient<IYoutubeIndexingWorkflowService, YoutubeIndexingWorkflowService>();

        services.AddSingleton<ITrackSourceProvider, YoutubeSourceProvider>();
        services.AddSingleton<ITrackPlaybackResolver, TrackPlaybackResolver>();
        services.AddSingleton<IPlaylistService, PlaylistService>();

        //Player
        services.AddSingleton<IMusicPlayerService, MusicPlayerService>();

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

        // UI
        services.AddTransient<MainWindow>();
        services.AddTransient<CompactPlayer>();

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
