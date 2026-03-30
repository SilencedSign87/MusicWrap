using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Sources.Contracts;
using System.Collections.ObjectModel;

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
                Details.Add(new YoutubeDetailGroupNode
                {
                    GroupId = detail.GroupId,
                    Title = detail.Title,
                    Subtitle = detail.Subtitle,
                    ThumbnailUrl = detail.ThumbnailUrl,
                    Tracks = new ObservableCollection<YoutubeDetailTrackNode>(
                        detail.Tracks.Select(t => new YoutubeDetailTrackNode
                        {
                            Id = t.Id,
                            Title = t.Title,
                            Subtitle = t.Subtitle
                        }))
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
                albumGroup.Tracks.Add(new YoutubeDetailTrackNode
                {
                    Id = track.Id,
                    Title = track.Title,
                    Subtitle = track.Subtitle
                });
            }
        }
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
    public string ThumbnailUrl { get; init; } = string.Empty;
    public required ObservableCollection<YoutubeDetailTrackNode> Tracks { get; init; }
}

public sealed class YoutubeDetailTrackNode
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
}
