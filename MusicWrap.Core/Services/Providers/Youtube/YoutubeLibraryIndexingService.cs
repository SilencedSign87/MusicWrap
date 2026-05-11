using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using System.Globalization;
using System.Net.Http;
using TagLib;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace MusicWrap.Core.Services.Providers.Youtube
{

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
        Task<YoutubeIndexingTrackResult> IndexResolvedTrackAsync(YoutubeIndexingRequest request, string sourceAudioPath, CancellationToken cancellationToken = default);
        void Persist();
    }

    public sealed class YoutubeLibraryIndexingService : IYoutubeLibraryIndexingService
    {
        private static readonly HttpClient _httpClient = new();

        private readonly ILibraryIndexer _libraryIndexer;
        private readonly ILibraryRepository _libraryRepository;
        private readonly MusicLibrary _library;
        private readonly UserSettings _userSettings;
        private readonly ILogger<YoutubeLibraryIndexingService> _logger;

        public YoutubeLibraryIndexingService(
            ILibraryIndexer libraryIndexer,
            ILibraryRepository libraryRepository,
            ILogger<YoutubeLibraryIndexingService> logger,
            MusicLibrary library,
            UserSettings userSettings)
        {
            _logger = logger;
            _libraryIndexer = libraryIndexer;
            _libraryRepository = libraryRepository;
            _library = library;
            _userSettings = userSettings;
        }

        public async Task<YoutubeIndexingTrackResult> IndexResolvedTrackAsync(YoutubeIndexingRequest request, string sourceAudioPath, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return new YoutubeIndexingTrackResult { Success = false, Error = "Invalid input data." };
            }

            if (string.IsNullOrWhiteSpace(request.ExternalId) || string.IsNullOrWhiteSpace(sourceAudioPath) || !IOFile.Exists(sourceAudioPath))
            {
                return new YoutubeIndexingTrackResult { Success = false, Error = "Invalid input data." };
            }

            if (IsAlreadyIndexedWithLocalFile(request.ExternalId, out string existingPath))
            {
                _logger.LogInformation("Skipping Youtube indexing for ExternalId {ExternalId} because file already exists at {ExistingPath}", request.ExternalId, existingPath);
                return new YoutubeIndexingTrackResult { Success = true };
            }

            string? destinationPath = null;

            try
            {
                string resolvedArtistName = ResolveArtistName(request);
                string destinationExtension = ResolveAudioExtension(sourceAudioPath);
                int durationSeconds = ResolveDurationSeconds(sourceAudioPath, request.Duration, request.ExternalId);

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
                    DurationSeconds = durationSeconds,
                    ThumbnailBytes = coverBytes,
                    ThumbnailMimeType = coverMimeType
                }, updateExistingMetadata: true);

                string outputRoot = ResolveOutputRoot();
                IODirectory.CreateDirectory(outputRoot);

                destinationPath = BuildDestinationPath(request, outputRoot, destinationExtension);
                string? destinationDirectory = IOPath.GetDirectoryName(destinationPath);
                if (string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    return new YoutubeIndexingTrackResult { Success = false, Error = "Invalid destination path." };
                }

                IODirectory.CreateDirectory(destinationDirectory);
                IOFile.Copy(sourceAudioPath, destinationPath, overwrite: true);

                ApplyMetadataToAudio(destinationPath, request, resolvedArtistName, coverBytes, coverMimeType);

                bool attached = _libraryIndexer.TryAttachExternalTrackLocalFile(new ExternalTrackLocalFileRequest
                {
                    Origin = TrackOrigin.Youtube,
                    ExternalId = request.ExternalId,
                    FilePath = destinationPath,
                    PreferExistingArtistAlbumMatch = true
                }, out _);

                if (!attached)
                {
                    CleanupFilesOnFailure(request.ExternalId, sourceAudioPath, destinationPath);
                }
                else
                {
                    // Remove staging cache once the final file has been tagged and indexed.
                    TryDeleteFile(sourceAudioPath, "source", request.ExternalId);
                }

                return new YoutubeIndexingTrackResult
                {
                    Success = attached,
                    Error = attached ? null : "Failed to attach local file to external track."
                };
            }
            catch (Exception ex)
            {
                CleanupFilesOnFailure(request.ExternalId, sourceAudioPath, destinationPath);
                _logger.LogError(ex, "Error indexing Youtube track with ExternalId {ExternalId}", request.ExternalId);
                return new YoutubeIndexingTrackResult { Success = false, Error = ex.Message };
            }
        }

        public void Persist()
        {
            _libraryRepository.Save(_library);
        }

        private static void ApplyMetadataToAudio(string filePath, YoutubeIndexingRequest request, string resolvedArtistName, byte[]? coverBytes, string? coverMimeType)
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

        private bool IsAlreadyIndexedWithLocalFile(string externalId, out string existingPath)
        {
            existingPath = string.Empty;

            var existing = _library.Tracks.FirstOrDefault(t =>
                t.Origin == TrackOrigin.Youtube &&
                string.Equals(t.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));

            if (existing is null || string.IsNullOrWhiteSpace(existing.FilePath))
            {
                return false;
            }

            if (!IOFile.Exists(existing.FilePath))
            {
                return false;
            }

            existingPath = existing.FilePath;
            return true;
        }

        private void CleanupFilesOnFailure(string externalId, string sourceAudioPath, string? destinationPath)
        {
            TryDeleteFile(sourceAudioPath, "source", externalId);

            if (!string.IsNullOrWhiteSpace(destinationPath))
            {
                TryDeleteFile(destinationPath, "destination", externalId);
            }
        }

        private void TryDeleteFile(string filePath, string role, string externalId)
        {
            try
            {
                if (IOFile.Exists(filePath))
                {
                    IOFile.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete {Role} file {FilePath} after indexing error for ExternalId {ExternalId}", role, filePath, externalId);
            }
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

                var existingArtist = _library.Tracks
                    .SelectMany(t => t.AlbumArtists.Length > 0 ? t.AlbumArtists : t.Artists)
                    .FirstOrDefault(a => NormalizeArtistKey(a) == key);

                if (!string.IsNullOrWhiteSpace(existingArtist))
                {
                    return existingArtist;
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

            duration = duration.Trim();

            if (TimeSpan.TryParseExact(duration,
                [@"m\:ss", @"mm\:ss", @"h\:mm\:ss", @"hh\:mm\:ss"],
                CultureInfo.InvariantCulture,
                out var exactParsed))
            {
                return Math.Max(0, (int)exactParsed.TotalSeconds);
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

        private int ResolveDurationSeconds(string sourceAudioPath, string? requestDuration, string externalId)
        {
            //int? fileDuration = TryReadDurationSecondsFromAudio(sourceAudioPath, externalId);
            int requestedDuration = ParseDurationSeconds(requestDuration);
            //if (fileDuration is > 0)
            //{
            //    if (requestedDuration > 0 && Math.Abs(requestedDuration - fileDuration.Value) >= 5)
            //    {
            //        _logger.LogInformation(
            //            "Using file duration {FileDurationSeconds}s instead of request duration {RequestDurationSeconds}s for Youtube ExternalId {ExternalId}",
            //            fileDuration.Value,
            //            requestedDuration,
            //            externalId);
            //    }

            //    return fileDuration.Value;
            //}

            return requestedDuration;
        }

        private int? TryReadDurationSecondsFromAudio(string filePath, string externalId)
        {
            try
            {
                using var taggedFile = TagLib.File.Create(filePath);
                int durationSeconds = Math.Max(0, (int)taggedFile.Properties.Duration.TotalSeconds);
                return durationSeconds;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to read real audio duration from {FilePath} for Youtube ExternalId {ExternalId}", filePath, externalId);
                return null;
            }
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download cover image from {Url}", candidateUrl);
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

        private string BuildDestinationPath(YoutubeIndexingRequest request, string outputRoot, string audioExtension)
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
            string normalizedExtension = string.IsNullOrWhiteSpace(audioExtension)
                ? ".flac"
                : audioExtension.StartsWith('.') ? audioExtension : $".{audioExtension}";

            if (IOPath.HasExtension(relativePath))
            {
                relativePath = IOPath.ChangeExtension(relativePath, normalizedExtension);
            }
            else
            {
                relativePath += normalizedExtension;
            }

            return IOPath.Combine(outputRoot, relativePath);
        }

        private static string ResolveAudioExtension(string filePath)
        {
            string extension = IOPath.GetExtension(filePath);
            return string.IsNullOrWhiteSpace(extension) ? ".flac" : extension;
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
}
