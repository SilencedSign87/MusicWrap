using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Metadata;
using MusicWrap.Core.Queue;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Activity;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Playlists;
using MusicWrap.Core.Services.Providers.Youtube;
using MusicWrap.Core.Services.Search;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Core.Sources.Providers.Local;
using MusicWrap.Core.Sources.Providers.Queue;
using MusicWrap.Core.Sources.Providers.Runtime;
using MusicWrap.Core.Sources.Providers.Youtube;
using MusicWrap.Data.Library;
using MusicWrap.Data.Player;
using MusicWrap.Data.Playlist;
using MusicWrap.Data.User;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.DI
{
    public static class CoreServiceRegistration
    {
        public static IServiceCollection AddMusicWrapCore(this IServiceCollection services)
        {
            // Messenger
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // DataLayer
            services.AddSingleton<ILibraryRepository, LibraryRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<ILibraryRepository>().Load());
            services.AddSingleton<IPlaybackRepository, PlaybackRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<IPlaybackRepository>().Load());
            services.AddSingleton<IUserSettingsRepository, UserSettingsRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<IUserSettingsRepository>().Load());
            services.AddSingleton<IPlaylistRepository, PlaylistRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<IPlaylistRepository>().Load());
            services.AddSingleton<ILibraryService, LibraryService>();

            // Core Services
            services.AddSingleton<ActivityService>();
            services.AddTransient<ILibraryScanner, LibraryScanner>();
            services.AddTransient<ILibraryIndexer, LibraryIndexer>();
            services.AddSingleton<ISaveCoordinator, SaveScheduler>();
            services.AddSingleton<IMetadataAutocompleteService, MetadataAutocompleteService>();
            
            services.AddSingleton<IQueueManager, QueueManager>();
            services.AddSingleton<ILibraryIntegrityService, LibraryIntegrityService>();
            services.AddSingleton<SearchService>();
            services.AddSingleton<ISearchQueryProvider, SearchService>(
                sp => sp.GetRequiredService<SearchService>());

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

            // Player
            services.AddSingleton<IMusicPlayerService, MusicPlayerService>();


            return services;
        }
    }
}
