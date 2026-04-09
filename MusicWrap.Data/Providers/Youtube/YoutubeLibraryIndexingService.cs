using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Application;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using System.Net.Http;
using TagLib;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOPath = System.IO.Path;

namespace MusicWrap.Data.Providers.Youtube;

public sealed class YoutubeIndexingRequest
{
    public required string ExternalId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public int TrackNumber { get; init; }
    public int DiscNumber { get; init; }
    public int Year { get; init; }
    public string Duration { get; init; } = string.Empty;
    public IReadOnlyList<string> ArtistCandidates { get; init; } = [];
    public string ThumbnailHighResUrl { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
}

public sealed class YoutubeIndexingTrackResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public interface IYoutubeLibraryIndexingService
{
    Task<YoutubeIndexingTrackResult> IndexResolvedTrackAsync(YoutubeIndexingRequest request, string sourceFlacPath, CancellationToken cancellationToken = default);
    void Persist();
}

public sealed class YoutubeLibraryIndexingService : IYoutubeLibraryIndexingService
{
    private static readonly HttpClient _httpClient = new();

    private readonly ILibraryIndexer _libraryIndexer;
    private readonly ILibraryRepository _libraryRepository;
    private readonly MusicLibrary _library;
    private readonly UserSettings _userSettings;

    public YoutubeLibraryIndexingService(
        ILibraryIndexer libraryIndexer,
        ILibraryRepository libraryRepository,
        MusicLibrary library,
        UserSettings userSettings)
    {
        _libraryIndexer = libraryIndexer;
        _libraryRepository = libraryRepository;
        _library = library;
        _userSettings = userSettings;
    }

    public async Task<YoutubeIndexingTrackResult> IndexResolvedTrackAsync(YoutubeIndexingRequest request, string sourceFlacPath, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return new YoutubeIndexingTrackResult { Success = false, Error = "Invalid input data." };
        }

        if (string.IsNullOrWhiteSpace(request.ExternalId) || string.IsNullOrWhiteSpace(sourceFlacPath) || !IOFile.Exists(sourceFlacPath))
        {
            return new YoutubeIndexingTrackResult { Success = false, Error = "Invalid input data." };
        }

        try
        {
            string resolvedArtistName = ResolveArtistName(request);

            var (coverBytes, coverMimeType) = await TryDownloadCoverAsync(request.ThumbnailHighResUrl, request.ThumbnailUrl, cancellationToken).ConfigureAwait(false);

            _libraryIndexer.UpsertExternalTrack(new ExternalTrackIndexRequest
            {
                Origin = TrackOrigin.Youtube,
                SourceUri = $"https://music.youtube.com/watch?v={request.ExternalId}",
                ExternalId = request.ExternalId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? request.ExternalId : request.Title,
                ArtistName = string.IsNullOrWhiteSpace(resolvedArtistName) ? "Unknown Artist" : resolvedArtistName,
                AlbumName = string.IsNullOrWhiteSpace(request.Album) ? "Unknown Album" : request.Album,
                Year = Math.Max(0, request.Year),
                DurationSeconds = ParseDurationSeconds(request.Duration),
                ThumbnailBytes = coverBytes,
                ThumbnailMimeType = coverMimeType
            }, updateExistingMetadata: true);

            string outputRoot = ResolveOutputRoot();
            IODirectory.CreateDirectory(outputRoot);

            string destinationPath = BuildDestinationPath(request, outputRoot);
            string? destinationDirectory = IOPath.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                return new YoutubeIndexingTrackResult { Success = false, Error = "Invalid destination path." };
            }

            IODirectory.CreateDirectory(destinationDirectory);
            IOFile.Copy(sourceFlacPath, destinationPath, overwrite: true);

            ApplyMetadataToFlac(destinationPath, request, resolvedArtistName, coverBytes, coverMimeType);

            bool attached = _libraryIndexer.TryAttachExternalTrackLocalFile(new ExternalTrackLocalFileRequest
            {
                Origin = TrackOrigin.Youtube,
                ExternalId = request.ExternalId,
                FilePath = destinationPath,
                PreferExistingArtistAlbumMatch = true
            }, out _);

            return new YoutubeIndexingTrackResult
            {
                Success = attached,
                Error = attached ? null : "Failed to attach local file to external track."
            };
        }
        catch (Exception ex)
        {
            return new YoutubeIndexingTrackResult { Success = false, Error = ex.Message };
        }
    }

    public void Persist()
    {
        _libraryRepository.Save(_library);
    }

    private static void ApplyMetadataToFlac(string filePath, YoutubeIndexingRequest request, string resolvedArtistName, byte[]? coverBytes, string? coverMimeType)
    {
        using var taggedFile = TagLib.File.Create(filePath);
        var tag = taggedFile.Tag;

        tag.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title;
        tag.Performers = string.IsNullOrWhiteSpace(resolvedArtistName) ? [] : [resolvedArtistName];
        tag.Album = string.IsNullOrWhiteSpace(request.Album) ? null : request.Album;
        tag.Genres = string.IsNullOrWhiteSpace(request.Genre) ? [] : [request.Genre];
        tag.Track = (uint)Math.Max(0, request.TrackNumber);
        tag.Disc = (uint)Math.Max(0, request.DiscNumber);
        tag.Year = (uint)Math.Max(0, request.Year);

        if (coverBytes is { Length: > 0 })
        {
            var picture = new Picture(new ByteVector(coverBytes))
            {
                Type = PictureType.FrontCover,
                MimeType = string.IsNullOrWhiteSpace(coverMimeType) ? "image/jpeg" : coverMimeType,
                Description = "Cover"
            };
            tag.Pictures = [picture];
        }

        taggedFile.Save();
    }

    private string ResolveArtistName(YoutubeIndexingRequest request)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Artist))
        {
            candidates.Add(request.Artist);
        }

        foreach (var candidate in request.ArtistCandidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate)
                && !candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(candidate);
            }
        }

        foreach (var candidate in candidates)
        {
            var key = NormalizeArtistKey(candidate);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var existing = _library.Artists.FirstOrDefault(a => NormalizeArtistKey(a.Name) == key);
            if (existing is not null)
            {
                return existing.Name;
            }
        }

        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "Unknown Artist";
    }

    private static string NormalizeArtistKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c))
            .ToArray();

        return new string(chars);
    }

    private static int ParseDurationSeconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return 0;
        }

        if (TimeSpan.TryParse(duration, out var parsed))
        {
            return Math.Max(0, (int)parsed.TotalSeconds);
        }

        var parts = duration.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out int mm)
            && int.TryParse(parts[1], out int ss))
        {
            return Math.Max(0, mm * 60 + ss);
        }

        if (parts.Length == 3
            && int.TryParse(parts[0], out int hh)
            && int.TryParse(parts[1], out mm)
            && int.TryParse(parts[2], out ss))
        {
            return Math.Max(0, hh * 3600 + mm * 60 + ss);
        }

        return 0;
    }

    private async Task<(byte[]? bytes, string? mimeType)> TryDownloadCoverAsync(string? preferredHighResUrl, string? fallbackThumbnailUrl, CancellationToken cancellationToken)
    {
        var candidateUrls = BuildThumbnailCandidateUrls(preferredHighResUrl, fallbackThumbnailUrl);
        if (candidateUrls.Count == 0)
        {
            return (null, null);
        }

        foreach (var candidateUrl in candidateUrls)
        {
            try
            {
                using var response = await _httpClient.GetAsync(candidateUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                if (bytes.Length == 0)
                {
                    continue;
                }

                string mimeType = response.Content.Headers.ContentType?.MediaType
                    ?? InferImageMimeType(candidateUrl);

                return (bytes, mimeType);
            }
            catch
            {
                // Try next candidate URL.
            }
        }

        return (null, null);
    }

    private static List<string> BuildThumbnailCandidateUrls(string? preferredHighResUrl, string? fallbackUrl)
    {
        var urls = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredHighResUrl))
        {
            urls.Add(preferredHighResUrl);
        }

        if (!string.IsNullOrWhiteSpace(fallbackUrl)
            && !urls.Contains(fallbackUrl, StringComparer.OrdinalIgnoreCase))
        {
            urls.Add(fallbackUrl);
        }

        return urls;
    }

    private string ResolveOutputRoot()
    {
        if (!string.IsNullOrWhiteSpace(_userSettings.YoutubeSettings.YoutubeLibraryRootPath))
        {
            return _userSettings.YoutubeSettings.YoutubeLibraryRootPath;
        }

        return IOPath.Combine(MusicWrapDirectories.LibraryDirectory, "Youtube");
    }

    private string BuildDestinationPath(YoutubeIndexingRequest request, string outputRoot)
    {
        string artist = string.IsNullOrWhiteSpace(request.Artist) ? "Unknown Artist" : request.Artist;
        string album = string.IsNullOrWhiteSpace(request.Album) ? "Unknown Album" : request.Album;
        string title = string.IsNullOrWhiteSpace(request.Title) ? request.ExternalId : request.Title;
        string trackNumber = request.TrackNumber > 0 ? request.TrackNumber.ToString("D2") : "00";

        string template = string.IsNullOrWhiteSpace(_userSettings.YoutubeSettings.YoutubePathTemplate)
            ? "{artist}/{album}/{trackNumber} - {title}"
            : _userSettings.YoutubeSettings.YoutubePathTemplate;

        string relativePath = template
            .Replace("{artist}", SanitizePathSegment(artist), StringComparison.OrdinalIgnoreCase)
            .Replace("{album}", SanitizePathSegment(album), StringComparison.OrdinalIgnoreCase)
            .Replace("{track}", SanitizePathSegment(title), StringComparison.OrdinalIgnoreCase)
            .Replace("{title}", SanitizePathSegment(title), StringComparison.OrdinalIgnoreCase)
            .Replace("{trackNumber}", trackNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{disc}", Math.Max(0, request.DiscNumber).ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{year}", Math.Max(0, request.Year).ToString(), StringComparison.OrdinalIgnoreCase);

        relativePath = relativePath.Replace('\\', IOPath.DirectorySeparatorChar).Replace('/', IOPath.DirectorySeparatorChar);
        if (!relativePath.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
        {
            relativePath += ".flac";
        }

        return IOPath.Combine(outputRoot, relativePath);
    }

    private static string InferImageMimeType(string url)
    {
        if (url.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image/webp";
        }

        return "image/jpeg";
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = IOPath.GetInvalidFileNameChars();
        var chars = value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        var normalized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "Unknown" : normalized;
    }
}
