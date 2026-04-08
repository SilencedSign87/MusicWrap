using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.UI.Models;
using MusicWrap.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace MusicWrap.UI.ViewModels.Providers;

public sealed partial class YoutubeProviderViewModel : ObservableObject
{
    private readonly IYoutubeSearchService _searchService;
    private CancellationTokenSource? _detailsLoadCts;
    private int _detailsLoadVersion;
    private bool _suppressSearchOnKindChange;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _currentQuery = string.Empty;
    [ObservableProperty] private string _emptyStateText = "Type a query in CommandPalette and press Enter.";
    [ObservableProperty] private YoutubeSearchKind _selectedKind = YoutubeSearchKind.Artists;

    public ObservableCollection<YoutubeSearchKindOption> SearchKinds { get; } =
    [
        new YoutubeSearchKindOption(YoutubeSearchKind.Artists, "Artist"),
        new YoutubeSearchKindOption(YoutubeSearchKind.Album, "Album"),
        new YoutubeSearchKindOption(YoutubeSearchKind.Song, "Song"),
        new YoutubeSearchKindOption(YoutubeSearchKind.Video, "Video"),
        new YoutubeSearchKindOption(YoutubeSearchKind.Featured, "Featured"),
        new YoutubeSearchKindOption(YoutubeSearchKind.CommunityPlaylist, "Community Playlist")
    ];

    public ObservableCollection<YoutubeSearchLeafNode> SearchResults { get; } = [];
    public ObservableCollection<YoutubeDetailGroupNode> Details { get; } = [];

    public YoutubeProviderViewModel(IYoutubeSearchService searchService)
    {
        _searchService = searchService;
    }

    partial void OnSelectedKindChanged(YoutubeSearchKind value)
    {
        if (_suppressSearchOnKindChange)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(CurrentQuery))
        {
            _ = SearchAsync(CurrentQuery);
        }
    }

    public async Task SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        CancelDetailsLoad();

        query = query?.Trim() ?? string.Empty;
        CurrentQuery = query;

        var inferredKind = TryInferKindFromUrl(query, SelectedKind);
        if (inferredKind.HasValue && inferredKind.Value != SelectedKind)
        {
            _suppressSearchOnKindChange = true;
            try
            {
                SelectedKind = inferredKind.Value;
            }
            finally
            {
                _suppressSearchOnKindChange = false;
            }
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResults.Clear();
            Details.Clear();
            EmptyStateText = "Type a query in CommandPalette and press Enter.";
            return;
        }

        IsLoading = true;
        try
        {
            var items = await _searchService.SearchAsync(query, SelectedKind, cancellationToken);

            SearchResults.Clear();
            foreach (var item in items)
            {
                SearchResults.Add(new YoutubeSearchLeafNode(item));
            }

            Details.Clear();
            EmptyStateText = SearchResults.Count == 0
                ? "No results."
                : "Select an item to open details.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectItemAsync(YoutubeSearchLeafNode? selectedItem, CancellationToken cancellationToken = default)
    {
        CancelDetailsLoad();

        if (selectedItem is null)
        {
            Details.Clear();
            EmptyStateText = "Select an item to open details.";
            return;
        }

        _detailsLoadVersion++;
        int loadVersion = _detailsLoadVersion;
        _detailsLoadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var detailsToken = _detailsLoadCts.Token;

        IsLoading = true;
        try
        {
            var detailItems = await _searchService.GetDetailsAsync(selectedItem.Item, SelectedKind, detailsToken);

            Details.Clear();
            foreach (var detail in detailItems)
            {
                var trackNodes = detail.Tracks
                    .Select((t, idx) =>
                    {
                        var (subtitle, duration) = SplitSubtitleAndDuration(t.Subtitle);
                        string effectiveDuration = !string.IsNullOrWhiteSpace(t.Duration) ? t.Duration : duration;
                        return new YoutubeDetailTrackNode
                        {
                            Id = t.Id,
                            Index = idx + 1,
                            Title = t.Title,
                            Artist = t.Artist,
                            Album = t.Album,
                            Genre = t.Genre,
                            Subtitle = subtitle,
                            Duration = effectiveDuration
                        };
                    });

                Details.Add(new YoutubeDetailGroupNode
                {
                    GroupId = detail.GroupId,
                    Title = detail.Title,
                    Subtitle = detail.Subtitle,
                    ArtistName = detail.ArtistName,
                    GroupType = detail.GroupType,
                    ReleaseYear = detail.ReleaseYear,
                    ThumbnailUrl = detail.ThumbnailUrl,
                    ThumbnailHighResUrl = detail.ThumbnailHighResUrl,
                    Tracks = new ObservableCollection<YoutubeDetailTrackNode>(trackNodes)
                });
            }

            EmptyStateText = Details.Count == 0 ? "No details found." : string.Empty;

            if (SelectedKind == YoutubeSearchKind.Artists)
            {
                _ = HydrateArtistAlbumsProgressivelyAsync(loadVersion, detailsToken);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearResults()
    {
        CancelDetailsLoad();
        SearchResults.Clear();
        Details.Clear();
        EmptyStateText = "Type a query in CommandPalette and press Enter.";
    }

    public bool AddTrackToIndexing(YoutubeDetailTrackNode? track, YoutubeDetailGroupNode? group = null)
    {
        if (track is null)
        {
            return false;
        }

        return AddTracksToIndexing([track], group) > 0;
    }

    public int AddTracksToIndexing(IEnumerable<YoutubeDetailTrackNode>? tracks, YoutubeDetailGroupNode? group = null)
    {
        if (tracks is null)
        {
            return 0;
        }

        var indexingViewModel = App.Services.GetRequiredService<IndexingViewModel>();
        int added = 0;

        foreach (var track in tracks)
        {
            // Note: YoutubeDetailTrackNode contains all data from YoutubeDetailTrack
            // We pass it directly to BuildStagedTrack to avoid creating intermediate model
            var stagedTrack = BuildStagedTrack(track, group, indexingViewModel.StagedTracks.Count + 1);
            if (indexingViewModel.TryAddStagedTrack(stagedTrack))
            {
                added++;
            }
        }

        return added;
    }

    public int AddGroupToIndexing(YoutubeDetailGroupNode? group)
    {
        if (group is null || group.Tracks.Count == 0)
        {
            return 0;
        }

        return AddTracksToIndexing(group.Tracks, group);
    }

    private async Task HydrateArtistAlbumsProgressivelyAsync(int loadVersion, CancellationToken cancellationToken)
    {
        var albumGroups = Details
            .Where(d => d.GroupId.StartsWith("album::", StringComparison.Ordinal))
            .ToArray();

        foreach (var albumGroup in albumGroups)
        {
            if (loadVersion != _detailsLoadVersion || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var albumId = albumGroup.GroupId["album::".Length..];
            if (string.IsNullOrWhiteSpace(albumId))
            {
                continue;
            }

            IReadOnlyList<YoutubeDetailTrack> tracks;
            try
            {
                tracks = await _searchService.GetAlbumTracksAsync(albumId, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (loadVersion != _detailsLoadVersion || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            albumGroup.Tracks.Clear();
            foreach (var track in tracks)
            {
                var (subtitle, duration) = SplitSubtitleAndDuration(track.Subtitle);
                string effectiveDuration = !string.IsNullOrWhiteSpace(track.Duration) ? track.Duration : duration;
                albumGroup.Tracks.Add(new YoutubeDetailTrackNode
                {
                    Id = track.Id,
                    Index = albumGroup.Tracks.Count + 1,
                    Title = track.Title,
                    Artist = track.Artist,
                    Album = track.Album,
                    Genre = track.Genre,
                    Subtitle = subtitle,
                    Duration = effectiveDuration
                });
            }
        }
    }

    private static (string Subtitle, string Duration) SplitSubtitleAndDuration(string subtitle)
    {
        if (string.IsNullOrWhiteSpace(subtitle))
        {
            return (string.Empty, string.Empty);
        }

        string text = subtitle.Trim();

        var onlyDuration = Regex.Match(text, @"^(?:#?\d+\s*-\s*)?(\d{1,2}:\d{2}(?::\d{2})?)$");
        if (onlyDuration.Success)
        {
            return (string.Empty, onlyDuration.Groups[1].Value);
        }

        var endDuration = Regex.Match(text, @"^(.*?)(?:\s*[•-]\s*)(\d{1,2}:\d{2}(?::\d{2})?)$");
        if (endDuration.Success)
        {
            string cleanSubtitle = endDuration.Groups[1].Value.Trim();
            string duration = endDuration.Groups[2].Value;
            return (cleanSubtitle, duration);
        }

        return (text, string.Empty);
    }

    private static StagedTrackNode BuildStagedTrack(YoutubeDetailTrackNode track, YoutubeDetailGroupNode? group, int index)
    {
        string artist = !string.IsNullOrWhiteSpace(track.Artist)
            ? track.Artist
            : (!string.IsNullOrWhiteSpace(group?.ArtistName) ? group.ArtistName : ExtractArtist(track.Subtitle, group?.Subtitle));

        string album = !string.IsNullOrWhiteSpace(track.Album)
            ? track.Album
            : (group?.GroupType.Equals("Album", StringComparison.OrdinalIgnoreCase) == true ? group.Title : string.Empty);

        return new StagedTrackNode
        {
            ExternalId = track.Id,
            Index = index,
            Title = track.Title,
            Artist = artist,
            Album = album,
            Genre = track.Genre,
            Year = group?.ReleaseYear ?? 0,
            Duration = track.Duration,
            ThumbnailUrl = group?.ThumbnailUrl ?? string.Empty,
            ThumbnailHighResUrl = group?.ThumbnailHighResUrl ?? group?.ThumbnailUrl ?? string.Empty,
            TrackNumber = track.Index,
            DiscNumber = 1
        };
    }

    private static string ExtractArtist(string trackSubtitle, string? groupSubtitle)
    {
        if (!string.IsNullOrWhiteSpace(trackSubtitle))
        {
            var separatorIndex = trackSubtitle.IndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                return trackSubtitle[..separatorIndex].Trim();
            }

            return trackSubtitle.Trim();
        }

        return groupSubtitle?.Trim() ?? string.Empty;
    }

    private void CancelDetailsLoad()
    {
        if (_detailsLoadCts is null)
        {
            return;
        }

        _detailsLoadCts.Cancel();
        _detailsLoadCts.Dispose();
        _detailsLoadCts = null;
    }

    private static YoutubeSearchKind? TryInferKindFromUrl(string query, YoutubeSearchKind currentKind)
    {
        if (!Uri.TryCreate(query, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!IsYoutubeHost(uri.Host))
        {
            return null;
        }

        var path = uri.AbsolutePath ?? string.Empty;
        if (path.StartsWith("/channel/", StringComparison.OrdinalIgnoreCase))
        {
            return YoutubeSearchKind.Artists;
        }

        string? videoId = GetQueryParameter(uri.Query, "v");
        if (!string.IsNullOrWhiteSpace(videoId))
        {
            if (currentKind == YoutubeSearchKind.Video)
            {
                return YoutubeSearchKind.Video;
            }

            return YoutubeSearchKind.Song;
        }

        string? listId = GetQueryParameter(uri.Query, "list");
        if (!string.IsNullOrWhiteSpace(listId))
        {
            if (listId.StartsWith("OLAK", StringComparison.OrdinalIgnoreCase))
            {
                return YoutubeSearchKind.Album;
            }

            if (currentKind == YoutubeSearchKind.Featured)
            {
                return YoutubeSearchKind.Featured;
            }

            return YoutubeSearchKind.CommunityPlaylist;
        }

        return null;
    }

    private static bool IsYoutubeHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetQueryParameter(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var parts = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var split = part.Split('=', 2);
            if (split.Length == 0)
            {
                continue;
            }

            if (!split[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (split.Length == 1)
            {
                return string.Empty;
            }

            return Uri.UnescapeDataString(split[1]);
        }

        return null;
    }
}

public sealed class YoutubeSearchKindOption
{
    public YoutubeSearchKind Value { get; }
    public string Label { get; }

    public YoutubeSearchKindOption(YoutubeSearchKind value, string label)
    {
        Value = value;
        Label = label;
    }
}

public sealed class YoutubeSearchLeafNode
{
    public YoutubeSearchItem Item { get; }

    public string Id => Item.Id;
    public string Title => Item.Title;
    public string Subtitle => Item.Subtitle;
    public string ThumbnailUrl => Item.ThumbnailUrl;

    public YoutubeSearchLeafNode(YoutubeSearchItem item)
    {
        Item = item;
    }
}

public sealed class YoutubeDetailGroupNode
{
    public required string GroupId { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public string ArtistName { get; init; } = string.Empty;
    public string GroupType { get; init; } = string.Empty;
    public int? ReleaseYear { get; init; }
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string ThumbnailHighResUrl { get; init; } = string.Empty;
    public required ObservableCollection<YoutubeDetailTrackNode> Tracks { get; init; }

    public string SubtitleArtistPart => ArtistName;

    public string SubtitleTypePart
    {
        get
        {
            if (string.IsNullOrWhiteSpace(GroupType))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(ArtistName)
                ? GroupType
                : $" • {GroupType}";
        }
    }

    public string SubtitleYearPart
    {
        get
        {
            if (ReleaseYear is null || ReleaseYear <= 0)
            {
                return string.Empty;
            }

            bool hasPrevious = !string.IsNullOrWhiteSpace(ArtistName)
                || !string.IsNullOrWhiteSpace(GroupType);

            return hasPrevious
                ? $" • {ReleaseYear.Value}"
                : ReleaseYear.Value.ToString();
        }
    }
}

public sealed class YoutubeDetailTrackNode
{
    public required string Id { get; init; }
    public required int Index { get; init; }
    public required string Title { get; init; }
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string TitleWithSubtitle => string.IsNullOrWhiteSpace(Subtitle) ? Title : $"{Title}  •  {Subtitle}";
}
