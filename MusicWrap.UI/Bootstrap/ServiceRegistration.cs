using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Metadata;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Playlists;
using MusicWrap.Core.Services.Providers.Youtube;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Core.Sources.Providers.Local;
using MusicWrap.Core.Sources.Providers.Runtime;
using MusicWrap.Core.Sources.Providers.Youtube;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library;
using MusicWrap.Data.Player;
using MusicWrap.Data.Playlist;
using MusicWrap.Data.User;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Playlist.ViewModels;
using MusicWrap.UI.Features.Providers.ViewModels;
using MusicWrap.UI.Features.Settings.ViewModels;
using MusicWrap.UI.Features.State.Services;
using MusicWrap.UI.Features.State.ViewModels;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.ViewModels;
using Serilog;

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

        // Services
        services.AddSingleton<IUIDispatcher, UIDispatcher>();
        services.AddSingleton<IImageService, ImageService>();
        services.AddSingleton<TracksContextMenuService>();
        services.AddSingleton<ILibraryScanner, LibraryScanner>();
        services.AddSingleton<ILibraryIndexer, LibraryIndexer>();
        services.AddSingleton<ILibraryCacheService, LibraryCacheService>();
        services.AddSingleton<ILibraryCacheStore, LibraryCacheStoreAdapter>();
        services.AddSingleton<ISaveOrchestration, SaveOrchestration>();
        services.AddSingleton<ISaveStateProvider>(sp => (ISaveStateProvider)sp.GetRequiredService<ISaveOrchestration>());
        services.AddSingleton<ISaveCoordinator, SaveScheduler>();
        services.AddSingleton<IMetadataAutocompleteService, MetadataAutocompleteService>();
        services.AddSingleton<IEditMetadataService, EditMetadataService>();

        // Providers
        services.AddSingleton<ITrackSourceProvider, LocalTrackSourceProvider>();
        services.AddSingleton<IYoutubeResolutionService, YoutubeResolutionService>();
        services.AddSingleton<IYoutubeStagingService, YoutubeStagingService>();
        services.AddSingleton<IYoutubeSearchService, YoutubeSearchService>();
        services.AddSingleton<IYoutubeLibraryIndexingService, YoutubeLibraryIndexingService>();
        services.AddSingleton<IYoutubeIndexingWorkflowService, YoutubeIndexingWorkflowService>();
        services.AddSingleton<ITrackSourceProvider, YoutubeSourceProvider>();
        services.AddSingleton<ITrackPlaybackResolver, TrackPlaybackResolver>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IStatusService, StatusService>();

        //Player
        services.AddSingleton<IMusicPlayerService, MusicPlayerService>();

        // View Models
        services.AddTransient<DirectoriesManagerViewModel>();
        services.AddTransient<SettingsGeneralViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<LibraryEntryDetailPanelViewModel>();
        services.AddTransient<AlbumTracksViewModel>();
        services.AddTransient<PlaylistViewModel>();
        services.AddTransient<PlaylistManagerViewModel>();
        services.AddTransient<SettingsYoutubeViewModel>();
        services.AddTransient<YoutubeProviderViewModel>();
        services.AddSingleton<IndexingViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<DeviceViewModel>();
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();
        services.AddSingleton<TaskbarIconViewModel>();
        services.AddTransient<MetadataEditorViewModel>();
        services.AddSingleton<DJControlViewModel>();
        services.AddSingleton<StatusbarViewModel>();

        // UI
        services.AddTransient<MainWindow>();
        services.AddTransient<CompactPlayer>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<IndexingWindow>();
        services.AddTransient<MetadataEditorWindow>();
        services.AddTransient<TrackInformationPage>();
    }
}
