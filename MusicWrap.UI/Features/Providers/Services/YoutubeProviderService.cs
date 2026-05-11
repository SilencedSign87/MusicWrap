using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using System.IO;

namespace MusicWrap.UI.Features.Providers.Services
{
    public interface IYoutubeProviderService
    {
        Task<int> ApplyCachedTracksAsync(CancellationToken cancellationToken = default);
    }
    public class YoutubeProviderService : IYoutubeProviderService
    {
        private readonly ILibraryService _libraryService;
        private readonly ILibraryIndexer _indexer;
        private readonly MusicLibrary _library;
        private readonly UserSettings _settings;
        private readonly ISaveCoordinator _saveOrchestration;
        public YoutubeProviderService(ILibraryService libraryService, ILibraryIndexer indexer, MusicLibrary library, UserSettings settings, ISaveCoordinator saveOrchestration)
        {
            _libraryService = libraryService;
            _indexer = indexer;
            _library = library;
            _settings = settings;
            _saveOrchestration = saveOrchestration;

        }
        public async Task<int> ApplyCachedTracksAsync(CancellationToken cancellationToken = default)
        {
            int applied = 0;
            string cacheDir = Path.Combine(MusicWrapDirectories.CacheDirectory, "YoutubeAudio");
            if (!Directory.Exists(cacheDir))
                return 0;
            if (string.IsNullOrWhiteSpace(_settings.YoutubeSettings.YoutubeLibraryRootPath) || !Directory.Exists(_settings.YoutubeSettings.YoutubeLibraryRootPath))
                return 0;

            foreach (var track in _library.Tracks.Where(t => t.Origin == TrackOrigin.Youtube && !string.IsNullOrWhiteSpace(t.ExternalId)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? cacheFile = ResolveCachedAudioPath(cacheDir, track.ExternalId!);
                if (!File.Exists(cacheFile))
                    continue;

                string extension = Path.GetExtension(cacheFile);
                string destPath = BuildDestinationPath(track, extension);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                if (!File.Exists(destPath))
                {
                    File.Move(cacheFile, destPath);
                }

                bool ok = _indexer.TryAttachExternalTrackLocalFile(new ExternalTrackLocalFileRequest
                {
                    Origin = TrackOrigin.Youtube,
                    ExternalId = track.ExternalId!,
                    FilePath = destPath,
                    PreferExistingArtistAlbumMatch = true
                }, out _);

                if (ok) applied++;
            }

            //_repository.Save(library);
            _saveOrchestration.Enqueue(SaveKind.Library);
            return await Task.FromResult(applied);

        }

        private string BuildDestinationPath(Track track, string extension)
        {
            string artist = ResolveArtistName(track);
            string album = ResolveAlbumTitle(track);
            string title = string.IsNullOrWhiteSpace(track.Title) ? (track.ExternalId ?? "Unknown Track") : track.Title;
            string num = track.TrackNumber > 0 ? track.TrackNumber.ToString("D2") : "00";

            string template = string.IsNullOrWhiteSpace(_settings.YoutubeSettings.YoutubePathTemplate)
                ? "{artist}/{album}/{trackNumber} - {title}"
                : _settings.YoutubeSettings.YoutubePathTemplate;

            string rel = template
                .Replace("{artist}", Sanitize(artist), StringComparison.OrdinalIgnoreCase)
                .Replace("{album}", Sanitize(album), StringComparison.OrdinalIgnoreCase)
                .Replace("{track}", Sanitize(title), StringComparison.OrdinalIgnoreCase)
                .Replace("{title}", Sanitize(title), StringComparison.OrdinalIgnoreCase)
                .Replace("{trackNumber}", num, StringComparison.OrdinalIgnoreCase);

            rel = rel.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

            string normalizedExtension = string.IsNullOrWhiteSpace(extension)
                ? ".flac"
                : extension.StartsWith('.') ? extension : $".{extension}";

            if (Path.HasExtension(rel))
            {
                rel = Path.ChangeExtension(rel, normalizedExtension);
            }
            else
            {
                rel += normalizedExtension;
            }

            return Path.Combine(_settings.YoutubeSettings.YoutubeLibraryRootPath, rel);
        }

        private static string? ResolveCachedAudioPath(string cacheDir, string externalId)
        {
            return Directory.EnumerateFiles(cacheDir, $"{externalId}.*", SearchOption.TopDirectoryOnly)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}{externalId}.src.", StringComparison.OrdinalIgnoreCase)
                    && !Path.GetFileName(path).Contains(".src.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private string ResolveArtistName(Track track)
        {
            return track.Artists.FirstOrDefault() ?? "Unknown Artist";
        }

        private string ResolveAlbumTitle(Track track)
        {
            return track.AlbumName ?? "Unknown Album";
        }

        private static string Sanitize(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            var s = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(s) ? "Unknown" : s;
        }


    }
}

