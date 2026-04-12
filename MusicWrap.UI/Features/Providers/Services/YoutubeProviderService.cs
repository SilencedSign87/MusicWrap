using MusicWrap.Core.Sources.Providers.Youtube;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Application;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicWrap.UI.Features.Providers.Services
{
    public interface IYoutubeProviderService
    {
        Task<int> ApplyCachedTracksAsync(CancellationToken cancellationToken = default);
    }
    public class YoutubeProviderService : IYoutubeProviderService
    {
        private readonly MusicLibrary _library;
        private readonly ILibraryIndexer _indexer;
        private readonly ILibraryRepository _repository;
        private readonly UserSettings _settings;
        public YoutubeProviderService(MusicLibrary library, ILibraryIndexer indexer, ILibraryRepository repository, UserSettings settings)
        {
            _library = library;
            _indexer = indexer;
            _repository = repository;
            _settings = settings;

        }
        public Task<int> ApplyCachedTracksAsync(CancellationToken cancellationToken = default)
        {
            int applied = 0;
            string cacheDir = Path.Combine(MusicWrapDirectories.CacheDirectory, "YoutubeAudio");
            if (!Directory.Exists(cacheDir)) 
                return Task.FromResult(0);
            if (string.IsNullOrWhiteSpace(_settings.YoutubeSettings.YoutubeLibraryRootPath)||!Directory.Exists(_settings.YoutubeSettings.YoutubeLibraryRootPath)) 
                return Task.FromResult(0);

            foreach(var track in _library.Tracks.Where(t=>t.Origin == TrackOrigin.Youtube && !string.IsNullOrWhiteSpace(t.ExternalId)))
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

            _repository.Save(_library);
            return Task.FromResult(applied);

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
            var id = track.ArtistIds.FirstOrDefault();
            var a = _library.Artists.FirstOrDefault(x => x.Id == id);
            return a?.Name ?? "Unknown Artist";
        }

        private string ResolveAlbumTitle(Track track)
        {
            var album = _library.Albums.FirstOrDefault(a => a.Id == track.AlbumId);
            return album?.Title ?? "Unknown Album";
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

