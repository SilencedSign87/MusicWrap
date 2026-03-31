namespace MusicWrap.Core.Sources.Contracts;

public interface IYoutubeSearchService
{
    Task<IReadOnlyList<YoutubeSearchItem>> SearchAsync(string query, YoutubeSearchKind kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YoutubeDetailGroup>> GetDetailsAsync(YoutubeSearchItem selectedItem, YoutubeSearchKind kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YoutubeDetailTrack>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default);
}

public enum YoutubeSearchKind
{
    Artists = 0,
    Album = 1,
    Song = 2,
    Video = 3,
    Featured = 4,
    CommunityPlaylist = 5
}

public sealed class YoutubeSearchItem
{
    public required YoutubeSearchKind Kind { get; init; }
    public required string Id { get; init; }
    public string? BrowseId { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string ThumbnailHighResUrl { get; init; } = string.Empty;
}

public sealed class YoutubeDetailGroup
{
    public required string GroupId { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public string ArtistName { get; init; } = string.Empty;
    public string GroupType { get; init; } = string.Empty;
    public int? ReleaseYear { get; init; }
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string ThumbnailHighResUrl { get; init; } = string.Empty;
    public required IReadOnlyList<YoutubeDetailTrack> Tracks { get; init; }
}

public sealed class YoutubeDetailTrack
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
}