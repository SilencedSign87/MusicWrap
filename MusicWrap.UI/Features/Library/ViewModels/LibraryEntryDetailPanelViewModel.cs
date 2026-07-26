using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Helpers;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace MusicWrap.UI.Features.Library.ViewModels
{

    public partial class LibraryEntryDetailPanelViewModel : ObservableObject, IDisposable
    {
        private readonly LibraryWorkspace _workspace;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILibraryService _libraryCache;
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly IwindowsImageService _imageService;
        private int _headerStatsRequestId;
        private bool _disposed;

        [ObservableProperty] private string headerTitle = string.Empty;
        [ObservableProperty] private string? headerImagePath;
        [ObservableProperty] private string headerAlbumsCountText = "0";
        [ObservableProperty] private string headerTracksCountText = "0";
        [ObservableProperty] private string headerTotalDurationText = "00:00:00";

        [ObservableProperty] private LibraryEntryTracksViewModel? tracksViewModel;
        [ObservableProperty] private LibraryEntryAlbumViewModel? albumEntriesViewModel;

        public LibraryWorkspace Workspace => _workspace;

        public LibraryEntryDetailPanelViewModel(
             LibraryWorkspace workspace,
            ILibraryService libraryCache,
            IMusicPlayerService musicPlayerService,
            IwindowsImageService imageService,
            IServiceProvider serviceProvider
            )
        {
            _workspace = workspace;
            _serviceProvider = serviceProvider;
            _libraryCache = libraryCache;
            _musicPlayerService = musicPlayerService;
            _imageService = imageService;

            AlbumEntriesViewModel = serviceProvider.GetRequiredService<LibraryEntryAlbumViewModel>();
            TracksViewModel = serviceProvider.GetRequiredService<LibraryEntryTracksViewModel>();

            _workspace.PropertyChanged += OnWorkspacePropertyChanged;

            if (_workspace.SelectedEntry is not null)
                LoadHeader(_workspace.SelectedEntry);

        }

        private void OnWorkspacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibraryWorkspace.SelectedEntry))
                LoadHeader(_workspace.SelectedEntry);
        }

        private void LoadHeader(LibraryEntry? entry)
        {
            if (entry is null)
            {
                HeaderTitle = string.Empty;
                HeaderImagePath = null;
                HeaderAlbumsCountText = "0";
                HeaderTracksCountText = "0";
                HeaderTotalDurationText = "00:00:00";
                return;
            }

            HeaderTitle = entry.Title;
            HeaderImagePath = entry.ImagePath;
            HeaderAlbumsCountText = "...";
            HeaderTracksCountText = "...";
            HeaderTotalDurationText = "...";

            //_imageService.ClearCache();
            _ = LoadHeaderStatsDeferredAsync(entry);
        }

        private async Task LoadHeaderStatsDeferredAsync(LibraryEntry entry)
        {
            var requestId = Interlocked.Increment(ref _headerStatsRequestId);

            var stats = await Task.Run(() =>
            {
                var albumCount = _libraryCache.GetAlbumsForEntry(entry).Count;
                var trackIds = _libraryCache.GetTrackIdsForEntry(entry);
                var totalSeconds = 0L;

                foreach (var trackId in trackIds)
                {
                    totalSeconds += _libraryCache.GetTrackById(trackId)?.Duration ?? 0;
                }

                return (albumCount, tracksCount: trackIds.Length, totalSeconds);
            });

            if (requestId != Volatile.Read(ref _headerStatsRequestId))
            {
                return;
            }

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(() => ApplyHeaderStats(stats.albumCount, stats.tracksCount, stats.totalSeconds));
                return;
            }

            ApplyHeaderStats(stats.albumCount, stats.tracksCount, stats.totalSeconds);
        }

        #region Internal
        private void ApplyHeaderStats(int albumCount, int tracksCount, long totalSeconds)
        {
            HeaderAlbumsCountText = albumCount.ToString();
            HeaderTracksCountText = tracksCount.ToString();
            HeaderTotalDurationText = FormatHelpers.FormatDuration((int)totalSeconds);
        }
        #endregion

        #region Relay Commands
        [RelayCommand]
        private void PlayAllTracks()
        {
            var trackIds = TracksViewModel?.AllTrackIds;
            if (trackIds is null || trackIds.Count == 0)
                return;
            if (_musicPlayerService.IsShuffleEnabled)
                _musicPlayerService.ToggleShuffle();
            _musicPlayerService.SetQueue(trackIds);
            _musicPlayerService.PlayIndex(0);
        }
        [RelayCommand]
        private void ShuffleAllTracks()
        {
            var trackIds = TracksViewModel?.AllTrackIds;
            if (trackIds is null || trackIds.Count == 0)
                return;
            _musicPlayerService.SetQueue(trackIds);
            if (!_musicPlayerService.IsShuffleEnabled)
                _musicPlayerService.ToggleShuffle();
            _musicPlayerService.PlayIndex(0);

        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _workspace.PropertyChanged -= OnWorkspacePropertyChanged;
            TracksViewModel?.Dispose();
            AlbumEntriesViewModel?.Dispose();
        }
    }
}
