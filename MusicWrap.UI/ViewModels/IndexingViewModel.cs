using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Core.Services.Providers.Youtube;
using MusicWrap.UI.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using System.Windows;

namespace MusicWrap.UI.ViewModels;

/// <summary>
/// ViewModel for the indexing window, manages batch track indexing with metadata editing.
/// </summary>
public sealed partial class IndexingViewModel : ObservableObject
{
    public const string VariousValuesMarker = "-- various values --";

    private readonly Dictionary<string, StagedArtworkNode> _artworksByUrl = new(StringComparer.OrdinalIgnoreCase);
    private readonly IYoutubeIndexingWorkflowService _youtubeIndexingWorkflowService;
    private readonly ILibraryCacheService _libraryCacheService;
    private bool _isUpdatingSelectionEditors;

    public IndexingViewModel(
        IYoutubeIndexingWorkflowService youtubeIndexingWorkflowService,
        ILibraryCacheService libraryCacheService)
    {
        _youtubeIndexingWorkflowService = youtubeIndexingWorkflowService;
        _libraryCacheService = libraryCacheService;
    }

    [ObservableProperty]
    private ObservableCollection<StagedTrackNode> stagedTracks = [];

    [ObservableProperty]
    private ObservableCollection<StagedArtworkNode> stagedArtworks = [];
    
    [ObservableProperty]
    private ObservableCollection<TemporaryArtist> temporaryArtists = [];
    
    [ObservableProperty]
    private ObservableCollection<TemporaryAlbum> temporaryAlbums = [];
    
    [ObservableProperty]
    private StagedTrackNode? selectedTrack;

    [ObservableProperty]
    private ObservableCollection<StagedTrackNode> selectedTracks = [];

    [ObservableProperty]
    private bool hasTrackSelection;

    [ObservableProperty]
    private string selectedTitleValue = string.Empty;

    [ObservableProperty]
    private string selectedArtistValue = string.Empty;

    [ObservableProperty]
    private string selectedAlbumValue = string.Empty;

    [ObservableProperty]
    private string selectedGenreValue = string.Empty;

    [ObservableProperty]
    private string selectedTrackNumberValue = string.Empty;

    [ObservableProperty]
    private string selectedDiscNumberValue = string.Empty;

    [ObservableProperty]
    private string selectedYearValue = string.Empty;

    [ObservableProperty]
    private string selectedDurationValue = string.Empty;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    private string saveStatusMessage = string.Empty;

    [ObservableProperty]
    private double indexingProgressValue;

    [ObservableProperty]
    private double indexingProgressMaximum = 1;
    
    partial void OnSelectedTrackChanged(StagedTrackNode? value)
    {
        
    }

    partial void OnSelectedTitleValueChanged(string value)
    {
        if (_isUpdatingSelectionEditors || value == VariousValuesMarker)
        {
            return;
        }

        foreach (var track in SelectedTracks)
        {
            track.Title = value;
        }
    }

    partial void OnSelectedArtistValueChanged(string value)
    {
        if (_isUpdatingSelectionEditors || value == VariousValuesMarker)
        {
            return;
        }

        foreach (var track in SelectedTracks)
        {
            track.Artist = value;
        }

        RebuildTemporaryEntities();
    }

    partial void OnSelectedAlbumValueChanged(string value)
    {
        if (_isUpdatingSelectionEditors || value == VariousValuesMarker)
        {
            return;
        }

        foreach (var track in SelectedTracks)
        {
            track.Album = value;
        }

        RebuildTemporaryEntities();
    }

    partial void OnSelectedGenreValueChanged(string value)
    {
        if (_isUpdatingSelectionEditors || value == VariousValuesMarker)
        {
            return;
        }

        foreach (var track in SelectedTracks)
        {
            track.Genre = value;
        }

        RebuildTemporaryEntities();
    }

    partial void OnSelectedTrackNumberValueChanged(string value)
    {
        if (_isUpdatingSelectionEditors || value == VariousValuesMarker)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var track in SelectedTracks)
            {
                track.TrackNumber = 0;
            }

            return;
        }

        if (!int.TryParse(value, out int parsedTrackNumber))
        {
            return;
        }

        foreach (var track in SelectedTracks)
        {
            track.TrackNumber = parsedTrackNumber;
        }
    }

    partial void OnSelectedDiscNumberValueChanged(string value)
    {
        if (_isUpdatingSelectionEditors || value == VariousValuesMarker)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var track in SelectedTracks)
            {
                track.DiscNumber = 0;
            }

            return;
        }

        if (!int.TryParse(value, out int parsedDiscNumber))
        {
            return;
        }

        foreach (var track in SelectedTracks)
        {
            track.DiscNumber = parsedDiscNumber;
        }
    }

    partial void OnSelectedYearValueChanged(string value)
    {
        if (_isUpdatingSelectionEditors || value == VariousValuesMarker)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var track in SelectedTracks)
            {
                track.Year = 0;
            }

            RebuildTemporaryEntities();
            return;
        }

        if (!int.TryParse(value, out int parsedYear))
        {
            return;
        }

        foreach (var track in SelectedTracks)
        {
            track.Year = parsedYear;
        }

        RebuildTemporaryEntities();
    }

    public void UpdateSelectedTracks(IEnumerable<StagedTrackNode> tracks)
    {
        var selected = tracks.ToArray();

        SelectedTracks = new ObservableCollection<StagedTrackNode>(selected);
        SelectedTrack = selected.FirstOrDefault();
        HasTrackSelection = selected.Length > 0;

        UpdateSelectionEditors();
    }
    
    /// <summary>
    /// Add a new temporary artist.
    /// </summary>
    [RelayCommand]
    private void AddTemporaryArtist(string? name = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var artist = new TemporaryArtist
        {
            Id = id,
            Name = name ?? "New Artist"
        };
        TemporaryArtists.Add(artist);
    }
    
    /// <summary>
    /// Add a new temporary album.
    /// </summary>
    private void AddTemporaryAlbum(string? name = null, string? artistName = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var album = new TemporaryAlbum
        {
            Id = id,
            Name = name ?? "New Album",
            Artist = artistName ?? string.Empty
        };
        TemporaryAlbums.Add(album);
    }
    
    /// <summary>
    /// Update all tracks using an artist name to a new artist name.
    /// </summary>
    private void UpdateArtistOnAllTracks(string oldArtistName, string newArtistName)
    {
        foreach (var track in StagedTracks)
        {
            if (track.Artist == oldArtistName)
            {
                track.Artist = newArtistName;
            }
        }
    }
    
    /// <summary>
    /// Update all tracks using an album to update album metadata.
    /// </summary>
    [RelayCommand]
    private void SyncAlbumToTracks(TemporaryAlbum album)
    {
        foreach (var track in StagedTracks)
        {
            if (track.Album == album.Name)
            {
                track.Artist = album.Artist;
                // Could also sync year/genre if needed
            }
        }
    }
    
    /// <summary>
    /// Batch add staged tracks (e.g., from YouTube search results).
    /// </summary>
    [RelayCommand]
    private void AddStagedTracks(IEnumerable<StagedTrackNode> tracks)
    {
        foreach (var track in tracks)
        {
            StagedTracks.Add(track);
        }

        SaveAllStagedTracksCommand.NotifyCanExecuteChanged();
    }

    public bool TryAddStagedTrack(StagedTrackNode track)
    {
        if (StagedTracks.Any(t => t.ExternalId.Equals(track.ExternalId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        AttachArtwork(track);

        StagedTracks.Add(track);
        EnsureTemporaryEntities(track);

        if (SelectedTrack is null)
        {
            SelectedTrack = track;
        }

        SaveAllStagedTracksCommand.NotifyCanExecuteChanged();

        return true;
    }
    
    /// <summary>
    /// Remove a track from staging.
    /// </summary>
    [RelayCommand]
    private void RemoveStagedTrack(StagedTrackNode track)
    {
        if (track is null)
        {
            return;
        }

        DetachArtworkIfUnused(track);
        StagedTracks.Remove(track);
        if (SelectedTracks.Any(s => ReferenceEquals(s, track)))
        {
            UpdateSelectedTracks(SelectedTracks.Where(s => !ReferenceEquals(s, track)));
        }
        else if (SelectedTrack == track)
        {
            UpdateSelectedTracks(StagedTracks.Take(1));
        }

        SaveAllStagedTracksCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveSelectedStagedTracks()
    {
        var toRemove = SelectedTracks.ToArray();
        foreach (var track in toRemove)
        {
            RemoveStagedTrack(track);
        }
    }
    
    /// <summary>
    /// Remove a temporary artist and update tracks.
    /// </summary>
    [RelayCommand]
    private void RemoveTemporaryArtist(TemporaryArtist artist)
    {
        TemporaryArtists.Remove(artist);
        
        // Clear artist reference from tracks
        foreach (var track in StagedTracks)
        {
            if (track.Artist == artist.Name)
            {
                track.Artist = string.Empty;
            }
        }
    }
    
    /// <summary>
    /// Remove a temporary album and update tracks.
    /// </summary>
    [RelayCommand]
    private void RemoveTemporaryAlbum(TemporaryAlbum album)
    {
        TemporaryAlbums.Remove(album);
        
        // Clear album reference from tracks
        foreach (var track in StagedTracks)
        {
            if (track.Album == album.Name)
            {
                track.Album = string.Empty;
            }
        }
    }

    [RelayCommand]
    private void ClearAllStagedTracks()
    {
        ResetStagingState(clearStatusMessage: true);
    }

    private void ResetStagingState(bool clearStatusMessage)
    {
        StagedTracks.Clear();
        StagedArtworks.Clear();
        _artworksByUrl.Clear();
        TemporaryArtists.Clear();
        TemporaryAlbums.Clear();
        SelectedTrack = null;
        SelectedTracks.Clear();
        HasTrackSelection = false;
        SelectedTitleValue = string.Empty;
        SelectedArtistValue = string.Empty;
        SelectedAlbumValue = string.Empty;
        SelectedGenreValue = string.Empty;
        SelectedTrackNumberValue = string.Empty;
        SelectedDiscNumberValue = string.Empty;
        SelectedYearValue = string.Empty;
        SelectedDurationValue = string.Empty;
        IndexingProgressValue = 0;
        IndexingProgressMaximum = 1;
        if (clearStatusMessage)
        {
            SaveStatusMessage = string.Empty;
        }

        SaveAllStagedTracksCommand.NotifyCanExecuteChanged();
    }

    private bool CanSaveAllStagedTracks() => !IsSaving && StagedTracks.Count > 0;

    [RelayCommand(CanExecute = nameof(CanSaveAllStagedTracks))]
    private async Task SaveAllStagedTracksAsync()
    {
        if (StagedTracks.Count == 0)
        {
            SaveStatusMessage = "No hay tracks para guardar.";
            return;
        }

        IsSaving = true;
        SaveStatusMessage = "indexing...";
        SaveAllStagedTracksCommand.NotifyCanExecuteChanged();

        var stagedTracksToSave = StagedTracks.ToArray();
        // Progress now accounts for 2 steps per track (downloading + indexing)
        IndexingProgressMaximum = Math.Max(1, stagedTracksToSave.Length * 2);
        IndexingProgressValue = 0;

        int saved = 0;
        int failed = 0;

        try
        {
            var requests = stagedTracksToSave
                .Select(track => new YoutubeIndexingRequest
                {
                    ExternalId = track.ExternalId,
                    Title = track.Title,
                    Artist = track.Artist,
                    ArtistCandidates = string.IsNullOrWhiteSpace(track.Artist) ? [] : [track.Artist],
                    Album = track.Album,
                    Genre = track.Genre,
                    TrackNumber = track.TrackNumber,
                    DiscNumber = track.DiscNumber,
                    Year = track.Year,
                    Duration = track.Duration,
                    ThumbnailHighResUrl = track.ThumbnailHighResUrl,
                    ThumbnailUrl = track.ThumbnailUrl
                })
                .ToArray();

            var batchResult = await _youtubeIndexingWorkflowService.IndexTracksAsync(
                requests,
                // Legacy progress callback (for backward compatibility)
                onProgress: (processed, total) =>
                {
                    // Simple counting: 1 to total
                },
                // Detailed progress callback with track and phase information
                onDetailedProgress: progress =>
                {
                    IndexingProgressMaximum = progress.TotalTracks * 2;
                    // Calculate step count: track index * 2 + phase offset
                    int stepOffset = progress.Phase.Equals("indexing", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    IndexingProgressValue = (progress.CurrentTrackIndex - 1) * 2 + stepOffset + 1;
                    // Display detailed status message
                    SaveStatusMessage = progress.StatusMessage;
                });

            saved = batchResult.Saved;
            failed = batchResult.Failed;

            _libraryCacheService.InvalidateCache();
            _libraryCacheService.SaveToDisk();

            if (saved > 0)
            {
                ResetStagingState(clearStatusMessage: false);
            }

            SaveStatusMessage = failed == 0
                ? "indexed successfully"
                : $"indexed with {failed} errors";
        }
        catch (YoutubeStagingException ex) when (ex.IsFfmpegConfigurationError)
        {
            SaveStatusMessage = "ffmpeg no configurado";
            MessageBox.Show(
                ex.Message,
                "MusicWrap - ffmpeg",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (YoutubeStagingException ex)
        {
            SaveStatusMessage = "indexing failed";
            MessageBox.Show(
                ex.Message,
                "MusicWrap - Indexing error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
            SaveAllStagedTracksCommand.NotifyCanExecuteChanged();
        }
    }

    private void UpdateSelectionEditors()
    {
        _isUpdatingSelectionEditors = true;
        try
        {
            if (SelectedTracks.Count == 0)
            {
                SelectedTitleValue = string.Empty;
                SelectedArtistValue = string.Empty;
                SelectedAlbumValue = string.Empty;
                SelectedGenreValue = string.Empty;
                SelectedTrackNumberValue = string.Empty;
                SelectedDiscNumberValue = string.Empty;
                SelectedYearValue = string.Empty;
                SelectedDurationValue = string.Empty;
                return;
            }

            SelectedTitleValue = GetCommonOrVarious(SelectedTracks.Select(t => t.Title));
            SelectedArtistValue = GetCommonOrVarious(SelectedTracks.Select(t => t.Artist));
            SelectedAlbumValue = GetCommonOrVarious(SelectedTracks.Select(t => t.Album));
            SelectedGenreValue = GetCommonOrVarious(SelectedTracks.Select(t => t.Genre));
            SelectedTrackNumberValue = GetCommonIntOrVarious(SelectedTracks.Select(t => t.TrackNumber), emptyWhenZero: true);
            SelectedDiscNumberValue = GetCommonIntOrVarious(SelectedTracks.Select(t => t.DiscNumber), emptyWhenZero: true);
            SelectedYearValue = GetCommonIntOrVarious(SelectedTracks.Select(t => t.Year), emptyWhenZero: true);
            SelectedDurationValue = GetCommonOrVarious(SelectedTracks.Select(t => t.Duration));
        }
        finally
        {
            _isUpdatingSelectionEditors = false;
        }
    }

    private static string GetCommonOrVarious(IEnumerable<string?> values)
    {
        var normalizedValues = values
            .Select(v => v?.Trim() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalizedValues.Length == 1
            ? normalizedValues[0]
            : VariousValuesMarker;
    }

    private static string GetCommonIntOrVarious(IEnumerable<int> values, bool emptyWhenZero)
    {
        var distinctValues = values.Distinct().ToArray();
        if (distinctValues.Length != 1)
        {
            return VariousValuesMarker;
        }

        int value = distinctValues[0];
        if (emptyWhenZero && value <= 0)
        {
            return string.Empty;
        }

        return value.ToString();
    }

    private void RebuildTemporaryEntities()
    {
        TemporaryArtists.Clear();
        TemporaryAlbums.Clear();

        foreach (var track in StagedTracks)
        {
            EnsureTemporaryEntities(track);
        }
    }

    private void AttachArtwork(StagedTrackNode track)
    {
        var url = track.ThumbnailUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            track.ArtworkId = null;
            return;
        }

        if (_artworksByUrl.TryGetValue(url, out var existingArtwork))
        {
            track.ArtworkId = existingArtwork.Id;
            track.ThumbnailUrl = existingArtwork.SourceUrl;
            return;
        }

        var artwork = new StagedArtworkNode
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceUrl = url
        };

        _artworksByUrl[url] = artwork;
        StagedArtworks.Add(artwork);

        track.ArtworkId = artwork.Id;
        track.ThumbnailUrl = artwork.SourceUrl;
    }

    private void DetachArtworkIfUnused(StagedTrackNode track)
    {
        if (string.IsNullOrWhiteSpace(track.ArtworkId) || string.IsNullOrWhiteSpace(track.ThumbnailUrl))
        {
            return;
        }

        bool usedByOthers = StagedTracks
            .Any(t => !ReferenceEquals(t, track)
                && string.Equals(t.ArtworkId, track.ArtworkId, StringComparison.Ordinal));

        if (usedByOthers)
        {
            return;
        }

        string url = track.ThumbnailUrl;
        _artworksByUrl.Remove(url);

        var artwork = StagedArtworks.FirstOrDefault(a => a.Id == track.ArtworkId);
        if (artwork is not null)
        {
            StagedArtworks.Remove(artwork);
        }
    }

    private void EnsureTemporaryEntities(StagedTrackNode track)
    {
        if (!string.IsNullOrWhiteSpace(track.Artist)
            && !TemporaryArtists.Any(a => a.Name.Equals(track.Artist, StringComparison.OrdinalIgnoreCase)))
        {
            TemporaryArtists.Add(new TemporaryArtist
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = track.Artist
            });
        }

        if (!string.IsNullOrWhiteSpace(track.Album)
            && !TemporaryAlbums.Any(a => a.Name.Equals(track.Album, StringComparison.OrdinalIgnoreCase)))
        {
            TemporaryAlbums.Add(new TemporaryAlbum
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = track.Album,
                Artist = track.Artist,
                Year = track.Year,
                Genre = track.Genre
            });
        }
    }
}


