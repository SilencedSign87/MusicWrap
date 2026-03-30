using MusicWrap.Core.Sources.Contracts;
using System.Net.Http;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models;
using YouTubeMusicAPI.Models.Search;

namespace MusicWrap.Core.Sources.Providers.Youtube;

public sealed class YoutubeSearchService : IYoutubeSearchService
{
    private readonly YouTubeMusicClient _ytmClient = new("US", null, null, null, null, new HttpClient());

    public async Task<IReadOnlyList<YoutubeSearchItem>> SearchAsync(string query, YoutubeSearchKind kind, CancellationToken cancellationToken = default)
    {
        query = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var urlResult = await TryResolveUrlSearchAsync(query, kind, cancellationToken);
        if (urlResult is not null)
        {
            return urlResult;
        }

        return kind switch
        {
            YoutubeSearchKind.Artists => await SearchArtistsAsync(query, cancellationToken),
            YoutubeSearchKind.Album => await SearchAlbumsAsync(query, cancellationToken),
            YoutubeSearchKind.Song => await SearchTracksAsync(query, cancellationToken),
            YoutubeSearchKind.Video => await SearchVideosAsync(query, cancellationToken),
            YoutubeSearchKind.Featured => await SearchFeaturedPlaylistsAsync(query, cancellationToken),
            YoutubeSearchKind.CommunityPlaylist => await SearchCommunityPlaylistsAsync(query, cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>?> TryResolveUrlSearchAsync(string query, YoutubeSearchKind kind, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(query, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!IsYoutubeHost(uri.Host))
        {
            return null;
        }

        string? artistId = TryGetArtistIdFromPath(uri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(artistId))
        {
            if (kind != YoutubeSearchKind.Artists)
            {
                return [];
            }

            return await TryResolveArtistFromIdAsync(artistId, cancellationToken);
        }

        string? playlistId = GetQueryParameter(uri.Query, "list");
        string? videoId = GetQueryParameter(uri.Query, "v");

        if (!string.IsNullOrWhiteSpace(videoId))
        {
            if (kind is not (YoutubeSearchKind.Song or YoutubeSearchKind.Video))
            {
                return [];
            }

            return await TryResolveSongVideoFromIdAsync(videoId, kind, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(playlistId))
        {
            return kind switch
            {
                YoutubeSearchKind.Album => await TryResolveAlbumFromIdAsync(playlistId, cancellationToken),
                YoutubeSearchKind.Featured => await TryResolveCommunityPlaylistFromIdAsync(playlistId, YoutubeSearchKind.Featured, cancellationToken),
                YoutubeSearchKind.CommunityPlaylist => await TryResolveCommunityPlaylistFromIdAsync(playlistId, YoutubeSearchKind.CommunityPlaylist, cancellationToken),
                _ => []
            };
        }

        return [];
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> TryResolveArtistFromIdAsync(string artistId, CancellationToken cancellationToken)
    {
        try
        {
            var info = await _ytmClient.GetArtistInfoAsync(artistId, cancellationToken);
            return
            [
                new YoutubeSearchItem
                {
                    Kind = YoutubeSearchKind.Artists,
                    Id = info.Id,
                    BrowseId = null,
                    Title = info.Name,
                    Subtitle = info.SubscribersInfo ?? string.Empty,
                    ThumbnailUrl = SelectThumbnailUrl(info.Thumbnails)
                }
            ];
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> TryResolveAlbumFromIdAsync(string albumId, CancellationToken cancellationToken)
    {
        try
        {
            var browseId = await _ytmClient.GetAlbumBrowseIdAsync(albumId, cancellationToken);
            var info = await _ytmClient.GetAlbumInfoAsync(browseId, cancellationToken);

            return
            [
                new YoutubeSearchItem
                {
                    Kind = YoutubeSearchKind.Album,
                    Id = albumId,
                    BrowseId = browseId,
                    Title = info.Name,
                    Subtitle = BuildAlbumInfoSubtitle(info),
                    ThumbnailUrl = SelectThumbnailUrl(info.Thumbnails)
                }
            ];
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> TryResolveCommunityPlaylistFromIdAsync(string playlistId, YoutubeSearchKind kind, CancellationToken cancellationToken)
    {
        try
        {
            var browseId = _ytmClient.GetCommunityPlaylistBrowseId(playlistId);
            if (string.IsNullOrWhiteSpace(browseId))
            {
                return [];
            }

            var info = await _ytmClient.GetCommunityPlaylistInfoAsync(browseId, cancellationToken);
            return
            [
                new YoutubeSearchItem
                {
                    Kind = kind,
                    Id = info.Id,
                    BrowseId = browseId,
                    Title = info.Name,
                    Subtitle = BuildPlaylistSubtitle(info),
                    ThumbnailUrl = SelectThumbnailUrl(info.Thumbnails)
                }
            ];
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> TryResolveSongVideoFromIdAsync(string videoId, YoutubeSearchKind kind, CancellationToken cancellationToken)
    {
        try
        {
            var info = await _ytmClient.GetSongVideoInfoAsync(videoId, cancellationToken);
            var subtitle = BuildSongVideoSubtitle(info);

            return
            [
                new YoutubeSearchItem
                {
                    Kind = kind,
                    Id = info.Id,
                    BrowseId = info.BrowseId,
                    Title = info.Name,
                    Subtitle = subtitle,
                    ThumbnailUrl = SelectThumbnailUrl(info.Thumbnails)
                }
            ];
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<YoutubeDetailGroup>> GetDetailsAsync(YoutubeSearchItem selectedItem, YoutubeSearchKind kind, CancellationToken cancellationToken = default)
    {
        if (selectedItem is null)
        {
            return [];
        }

        switch (kind)
        {
            case YoutubeSearchKind.Artists:
                return await BuildArtistGroupsAsync(selectedItem, cancellationToken);

            case YoutubeSearchKind.Album:
                return await BuildAlbumGroupAsync(selectedItem, cancellationToken);

            case YoutubeSearchKind.Featured:
            case YoutubeSearchKind.CommunityPlaylist:
                return await BuildPlaylistGroupAsync(selectedItem, cancellationToken);

            case YoutubeSearchKind.Song:
            case YoutubeSearchKind.Video:
            default:
                return BuildSingleGroup(selectedItem);
        }
    }

    public async Task<IReadOnlyList<YoutubeDetailTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            return [];
        }

        try
        {
            var browseId = _ytmClient.GetCommunityPlaylistBrowseId(playlistId);
            if (string.IsNullOrWhiteSpace(browseId))
            {
                return [];
            }

            //var songs = await _ytmClient.GetCommunityPlaylistSongsAsync(browseId, cancellationToken);
            var songs = await _ytmClient.GetCommunityPlaylistSongsAsync(browseId).FetchItemsAsync(0, 200, cancellationToken);
            return songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Name))
                .Take(200)
                .Select(s => new YoutubeDetailTrack
                {
                    Id = s.Id,
                    Title = s.Name,
                    Subtitle = BuildSongSubtitle(s)
                })
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<YoutubeDetailTrack>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(albumId))
        {
            return [];
        }

        try
        {
            var browseId = await _ytmClient.GetAlbumBrowseIdAsync(albumId, cancellationToken);
            var albumInfo = await _ytmClient.GetAlbumInfoAsync(browseId, cancellationToken);
            return albumInfo.Songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Name))
                .OrderBy(s => s.SongNumber ?? int.MaxValue)
                .Take(100)
                .Select(s => new YoutubeDetailTrack
                {
                    Id = s.Id,
                    Title = s.Name,
                    Subtitle = FormatAlbumSongSubtitle(s)
                })
                .ToArray();
        }
        catch
        {
            return [];
        }
    }



    private async Task<IReadOnlyList<YoutubeSearchItem>> SearchArtistsAsync(string query, CancellationToken cancellationToken)
    {
        var all = await _ytmClient.SearchAsync(query, SearchCategory.Artists)
            .FetchItemsAsync(0, 40, cancellationToken);

        var artists = all
            .OfType<ArtistSearchResult>()
            .Select(a => new YoutubeSearchItem
            {
                Kind = YoutubeSearchKind.Artists,
                Id = a.Id ?? string.Empty,
                BrowseId = null,
                Title = a.Name,
                Subtitle = a.PopularityInfo,
                ThumbnailUrl = SelectThumbnailUrl(a.Thumbnails)
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .ToList();

        if (artists.Count == 0)
        {
            return [];
        }

        int bestMatchIndex = FindBestArtistIndex(artists, query);
        if (bestMatchIndex > 0)
        {
            var bestMatch = artists[bestMatchIndex];
            artists.RemoveAt(bestMatchIndex);
            artists.Insert(0, bestMatch);
        }

        var seedArtist = artists[0];
        try
        {
            var info = await _ytmClient.GetArtistInfoAsync(seedArtist.Id, cancellationToken);
            if (info is not null)
            {
                var existingIds = new HashSet<string>(artists.Select(a => a.Id), StringComparer.Ordinal);
                var relatedArtists = info.Related
                    .Where(r => !string.IsNullOrWhiteSpace(r.Id) && !string.IsNullOrWhiteSpace(r.Name))
                    .Where(r => !existingIds.Contains(r.Id))
                    .Select(r => new YoutubeSearchItem
                    {
                        Kind = YoutubeSearchKind.Artists,
                        Id = r.Id,
                        BrowseId = null,
                        Title = r.Name,
                        Subtitle = string.IsNullOrWhiteSpace(r.SubscribersInfo)
                            ? "Related artist"
                            : r.SubscribersInfo,
                        ThumbnailUrl = SelectThumbnailUrl(r.Thumbnails)
                    })
                    .ToArray();

                artists.AddRange(relatedArtists);
            }
        }
        catch
        {
            // Keep base search results if related artists request fails.
        }

        return artists
            .ToArray();
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> SearchAlbumsAsync(string query, CancellationToken cancellationToken)
    {
        var all = await _ytmClient.SearchAsync(query, SearchCategory.Albums)
            .FetchItemsAsync(0, 60, cancellationToken);

        return all
            .OfType<AlbumSearchResult>()
            .Select(a => new YoutubeSearchItem
            {
                Kind = YoutubeSearchKind.Album,
                Id = a.Id ?? string.Empty,
                BrowseId = null,
                Title = a.Name,
                Subtitle = BuildAlbumSubtitle(a),
                ThumbnailUrl = SelectThumbnailUrl(a.Thumbnails)
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .ToArray();
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> SearchTracksAsync(string query, CancellationToken cancellationToken)
    {
        var all = await _ytmClient.SearchAsync(query, SearchCategory.Songs)
            .FetchItemsAsync(0, 100, cancellationToken);

        return all
            .OfType<SongSearchResult>()
            .Select(s => new YoutubeSearchItem
            {
                Kind = YoutubeSearchKind.Song,
                Id = s.Id ?? string.Empty,
                Title = s.Name,
                Subtitle = BuildSongSubtitle(s),
                ThumbnailUrl = SelectThumbnailUrl(s.Thumbnails)
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .ToArray();
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> SearchVideosAsync(string query, CancellationToken cancellationToken)
    {
        var all = await _ytmClient.SearchAsync(query, SearchCategory.Videos)
            .FetchItemsAsync(0, 100, cancellationToken);

        return all
            .OfType<VideoSearchResult>()
            .Select(v => new YoutubeSearchItem
            {
                Kind = YoutubeSearchKind.Video,
                Id = v.Id ?? string.Empty,
                Title = v.Name,
                Subtitle = JoinArtists(v.Artists),
                ThumbnailUrl = SelectThumbnailUrl(v.Thumbnails)
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .ToArray();
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> SearchFeaturedPlaylistsAsync(string query, CancellationToken cancellationToken)
    {
        var all = await _ytmClient.SearchAsync(query, SearchCategory.CommunityPlaylists)
            .FetchItemsAsync(0, 100, cancellationToken);

        return all
            .OfType<YouTubeMusicAPI.Models.Search.CommunityPlaylistSearchResult>()
            .Select(p => new YoutubeSearchItem
            {
                Kind = YoutubeSearchKind.Featured,
                Id = p.Id ?? string.Empty,
                Title = p.Name,
                Subtitle = p.Creator?.Name ?? p.ViewsInfo ?? string.Empty,
                ThumbnailUrl = SelectThumbnailUrl(p.Thumbnails)
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .ToArray();
    }

    private async Task<IReadOnlyList<YoutubeSearchItem>> SearchCommunityPlaylistsAsync(string query, CancellationToken cancellationToken)
    {
        var all = await _ytmClient.SearchAsync(query, SearchCategory.CommunityPlaylists)
            .FetchItemsAsync(0, 100, cancellationToken);

        return all
            .OfType<YouTubeMusicAPI.Models.Search.CommunityPlaylistSearchResult>()
            .Select(p => new YoutubeSearchItem
            {
                Kind = YoutubeSearchKind.CommunityPlaylist,
                Id = p.Id ?? string.Empty,
                Title = p.Name,
                Subtitle = p.Creator?.Name ?? p.ViewsInfo ?? string.Empty,
                ThumbnailUrl = SelectThumbnailUrl(p.Thumbnails)
            })
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .ToArray();
    }

    private static IReadOnlyList<YoutubeDetailGroup> BuildSingleGroup(YoutubeSearchItem selectedItem)
    {
        return
        [
            new YoutubeDetailGroup
            {
                GroupId = selectedItem.Id,
                Title = selectedItem.Title,
                Subtitle = selectedItem.Subtitle,
                ThumbnailUrl = selectedItem.ThumbnailUrl,
                Tracks =
                [
                    new YoutubeDetailTrack
                    {
                        Id = selectedItem.Id,
                        Title = selectedItem.Title,
                        Subtitle = selectedItem.Subtitle
                    }
                ]
            }
        ];
    }

    private async Task<IReadOnlyList<YoutubeDetailGroup>> BuildArtistGroupsAsync(YoutubeSearchItem artistItem, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artistItem.Id))
        {
            return [];
        }

        var artistInfo = await _ytmClient.GetArtistInfoAsync(artistItem.Id, cancellationToken);
        if (artistInfo is null)
        {
            return [];
        }

        var groups = new List<YoutubeDetailGroup>();

        var albumGroups = artistInfo.Albums
            .Where(a => !string.IsNullOrWhiteSpace(a.Id) && !string.IsNullOrWhiteSpace(a.Name))
            .Take(24)
            .Select(a => new YoutubeDetailGroup
            {
                GroupId = $"album::{a.Id}",
                Title = a.Name,
                Subtitle = BuildArtistAlbumSubtitle(artistItem.Title, a),
                ThumbnailUrl = SelectThumbnailUrl(a.Thumbnails),
                Tracks = []
            })
            .ToArray();
        groups.AddRange(albumGroups);

        var artistTracks = artistInfo.Songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Name))
            .DistinctBy(s => s.Id)
            .Take(200)
            .Select(s => new YoutubeDetailTrack
            {
                Id = s.Id,
                Title = s.Name,
                Subtitle = BuildArtistSongSubtitle(s)
            })
            .ToArray();

        if (artistTracks.Length > 0)
        {
            groups.Add(new YoutubeDetailGroup
            {
                GroupId = $"tracks::{artistInfo.Id}",
                Title = "Tracks",
                Subtitle = artistItem.Title,
                ThumbnailUrl = artistItem.ThumbnailUrl,
                Tracks = artistTracks
            });
        }

        var artistVideos = artistInfo.Videos
            .Where(v => !string.IsNullOrWhiteSpace(v.Id) && !string.IsNullOrWhiteSpace(v.Name))
            .DistinctBy(v => v.Id)
            .Take(120)
            .ToArray();

        if (artistVideos.Length > 0)
        {
            foreach (var video in artistVideos)
            {
                groups.Add(new YoutubeDetailGroup
                {
                    GroupId = $"video::{video.Id}",
                    Title = video.Name,
                    Subtitle = BuildArtistVideoSubtitle(video),
                    ThumbnailUrl = SelectThumbnailUrl(video.Thumbnails),
                    Tracks =
                    [
                        new YoutubeDetailTrack
                        {
                            Id = video.Id,
                            Title = video.Name,
                            Subtitle = BuildArtistVideoSubtitle(video)
                        }
                    ]
                });
            }
        }

        return groups;
    }

    private async Task<IReadOnlyList<YoutubeDetailGroup>> BuildAlbumGroupAsync(YoutubeSearchItem albumItem, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(albumItem.Id))
        {
            return
            [
                new YoutubeDetailGroup
                {
                    GroupId = albumItem.Id,
                    Title = albumItem.Title,
                    Subtitle = albumItem.Subtitle,
                    ThumbnailUrl = albumItem.ThumbnailUrl,
                    Tracks = []
                }
            ];
        }

        try
        {
            var browseId = await _ytmClient.GetAlbumBrowseIdAsync(albumItem.Id, cancellationToken);
            var albumInfo = await _ytmClient.GetAlbumInfoAsync(browseId, cancellationToken);
            var tracks = await GetAlbumTracksAsync(albumItem.Id, cancellationToken);

            return
            [
                new YoutubeDetailGroup
                {
                    GroupId = albumInfo.Id,
                    Title = albumInfo.Name,
                    Subtitle = BuildAlbumInfoSubtitle(albumInfo),
                    ThumbnailUrl = SelectThumbnailUrl(albumInfo.Thumbnails),
                    Tracks = tracks
                }
            ];
        }
        catch
        {
            // Fallback to the selected item if album details endpoint fails.
        }

        return
        [
            new YoutubeDetailGroup
            {
                GroupId = albumItem.Id,
                Title = albumItem.Title,
                Subtitle = albumItem.Subtitle,
                ThumbnailUrl = albumItem.ThumbnailUrl,
                Tracks = []
            }
        ];
    }

    private async Task<IReadOnlyList<YoutubeDetailGroup>> BuildPlaylistGroupAsync(YoutubeSearchItem playlistItem, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playlistItem.Id))
        {
            return
            [
                new YoutubeDetailGroup
                {
                    GroupId = playlistItem.Id,
                    Title = playlistItem.Title,
                    Subtitle = playlistItem.Subtitle,
                    ThumbnailUrl = playlistItem.ThumbnailUrl,
                    Tracks = []
                }
            ];
        }

        try
        {
            var browseId = _ytmClient.GetCommunityPlaylistBrowseId(playlistItem.Id);
            if (string.IsNullOrWhiteSpace(browseId))
            {
                return
                [
                    new YoutubeDetailGroup
                    {
                        GroupId = playlistItem.Id,
                        Title = playlistItem.Title,
                        Subtitle = playlistItem.Subtitle,
                        ThumbnailUrl = playlistItem.ThumbnailUrl,
                        Tracks = []
                    }
                ];
            }

            var playlistInfo = await _ytmClient.GetCommunityPlaylistInfoAsync(browseId, cancellationToken);
            var playlistTracks = await GetPlaylistTracksAsync(playlistItem.Id, cancellationToken);

            return
            [
                new YoutubeDetailGroup
                {
                    GroupId = playlistInfo.Id,
                    Title = playlistInfo.Name,
                    Subtitle = BuildPlaylistSubtitle(playlistInfo),
                    ThumbnailUrl = SelectThumbnailUrl(playlistInfo.Thumbnails),
                    Tracks = playlistTracks
                }
            ];
        }
        catch
        {
            // Fallback to the selected item if playlist details endpoint fails.
        }

        return
        [
            new YoutubeDetailGroup
            {
                GroupId = playlistItem.Id,
                Title = playlistItem.Title,
                Subtitle = playlistItem.Subtitle,
                ThumbnailUrl = playlistItem.ThumbnailUrl,
                Tracks = []
            }
        ];
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

    private static string? TryGetArtistIdFromPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return null;
        }

        var segments = absolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        if (segments[0].Equals("channel", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.UnescapeDataString(segments[1]);
        }

        return null;
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

        var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var split = pair.Split('=', 2);
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

    private static string BuildSongVideoSubtitle(YouTubeMusicAPI.Models.Info.SongVideoInfo info)
    {
        var artists = JoinArtists(info.Artists);
        var albumName = info.Album?.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(artists) && !string.IsNullOrWhiteSpace(albumName))
        {
            return $"{artists} - {albumName}";
        }

        if (!string.IsNullOrWhiteSpace(artists))
        {
            return artists;
        }

        if (!string.IsNullOrWhiteSpace(albumName))
        {
            return albumName;
        }

        return FormatDuration(info.Duration);
    }

    private static string BuildAlbumSubtitle(AlbumSearchResult album)
    {
        string artists = JoinArtists(album.Artists);
        string year = album.ReleaseYear > 0 ? album.ReleaseYear.ToString() : string.Empty;

        if (!string.IsNullOrWhiteSpace(artists) && !string.IsNullOrWhiteSpace(year))
        {
            return $"{artists} - {year}";
        }

        return !string.IsNullOrWhiteSpace(artists) ? artists : year;
    }

    private static string BuildSongSubtitle(SongSearchResult song)
    {
        string artists = JoinArtists(song.Artists);
        string albumName = song.Album?.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(artists) && !string.IsNullOrWhiteSpace(albumName))
        {
            return $"{artists} - {albumName}";
        }

        return !string.IsNullOrWhiteSpace(artists) ? artists : albumName;
    }

    private static string BuildSongSubtitle(YouTubeMusicAPI.Models.Info.CommunityPlaylistSong song)
    {
        string artists = JoinArtists(song.Artists);
        string albumName = song.Album?.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(artists) && !string.IsNullOrWhiteSpace(albumName))
        {
            return $"{artists} - {albumName}";
        }

        if (!string.IsNullOrWhiteSpace(artists))
        {
            return artists;
        }

        if (!string.IsNullOrWhiteSpace(albumName))
        {
            return albumName;
        }

        return FormatDuration(song.Duration);
    }

    private static string BuildPlaylistSubtitle(YouTubeMusicAPI.Models.Info.CommunityPlaylistInfo playlist)
    {
        var creator = playlist.Creator?.Name ?? string.Empty;
        var viewsInfo = playlist.ViewsInfo ?? string.Empty;
        var songCount = playlist.SongCount > 0 ? $"{playlist.SongCount} songs" : string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(creator))
        {
            parts.Add(creator);
        }

        if (!string.IsNullOrWhiteSpace(songCount))
        {
            parts.Add(songCount);
        }

        if (!string.IsNullOrWhiteSpace(viewsInfo))
        {
            parts.Add(viewsInfo);
        }

        return string.Join(" - ", parts);
    }

    private static int FindBestArtistIndex(IReadOnlyList<YoutubeSearchItem> artists, string query)
    {
        if (artists.Count == 0)
        {
            return -1;
        }

        for (int i = 0; i < artists.Count; i++)
        {
            if (artists[i].Title.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        for (int i = 0; i < artists.Count; i++)
        {
            if (artists[i].Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        for (int i = 0; i < artists.Count; i++)
        {
            if (artists[i].Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static string JoinArtists(IEnumerable<NamedEntity> artists)
    {
        return string.Join(", ", artists.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
    }

    private static string BuildArtistSongSubtitle(YouTubeMusicAPI.Models.Info.ArtistSong song)
    {
        var artists = JoinArtists(song.Artists);
        var albumName = song.Album?.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(artists) && !string.IsNullOrWhiteSpace(albumName))
        {
            return $"{artists} - {albumName}";
        }

        if (!string.IsNullOrWhiteSpace(artists))
        {
            return artists;
        }

        if (!string.IsNullOrWhiteSpace(albumName))
        {
            return albumName;
        }

        return song.Playsinfo ?? string.Empty;
    }

    private static string BuildArtistVideoSubtitle(YouTubeMusicAPI.Models.Info.ArtistVideo video)
    {
        var artists = JoinArtists(video.Artists);
        if (!string.IsNullOrWhiteSpace(artists) && !string.IsNullOrWhiteSpace(video.ViewsInfo))
        {
            return $"{artists} - {video.ViewsInfo}";
        }

        if (!string.IsNullOrWhiteSpace(artists))
        {
            return artists;
        }

        return video.ViewsInfo ?? string.Empty;
    }

    private static string BuildArtistAlbumSubtitle(string artistName, YouTubeMusicAPI.Models.Info.ArtistAlbum album)
    {
        string type = album.IsSingle ? "Single" : album.IsEp ? "EP" : "Album";
        string year = album.ReleaseYear > 0 ? album.ReleaseYear.ToString() : string.Empty;

        if (!string.IsNullOrWhiteSpace(year))
        {
            return $"{artistName} - {type} - {year}";
        }

        return $"{artistName} - {type}";
    }

    private static string BuildAlbumInfoSubtitle(YouTubeMusicAPI.Models.Info.AlbumInfo albumInfo)
    {
        var artists = JoinArtists(albumInfo.Artists);
        var type = albumInfo.IsSingle ? "Single" : albumInfo.IsEp ? "EP" : "Album";
        var year = albumInfo.ReleaseYear > 0 ? albumInfo.ReleaseYear.ToString() : string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(artists))
        {
            parts.Add(artists);
        }

        parts.Add(type);

        if (!string.IsNullOrWhiteSpace(year))
        {
            parts.Add(year);
        }

        return string.Join(" - ", parts);
    }

    private static string FormatAlbumSongSubtitle(YouTubeMusicAPI.Models.Info.AlbumSong song)
    {
        string duration = FormatDuration(song.Duration);
        if (song.SongNumber is int number && !string.IsNullOrWhiteSpace(duration))
        {
            return $"#{number} - {duration}";
        }

        if (song.SongNumber is int onlyNumber)
        {
            return $"#{onlyNumber}";
        }

        return duration;
    }

    private static string SelectThumbnailUrl(IEnumerable<Thumbnail> thumbnails)
    {
        return thumbnails
            .OrderByDescending(t => t.Width * t.Height)
            .Select(t => t.Url)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return string.Empty;
        }

        return duration.TotalHours >= 1
            ? duration.ToString("h\\:mm\\:ss")
            : duration.ToString("m\\:ss");
    }
}
