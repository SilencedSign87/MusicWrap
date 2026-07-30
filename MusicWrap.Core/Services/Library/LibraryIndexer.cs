using MusicWrap.Core.Services.Images;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.Core.Services.Library
{
    public interface ILibraryIndexer
    {
        Task IndexFileAsync(string filePath, CancellationToken ct = default);
        ExternalTrackIndexResult IndexExternalTrack(ExternalTrackIndexRequest request);
        ExternalTrackIndexResult UpsertExternalTrack(ExternalTrackIndexRequest request, bool updateExistingMetadata);
        bool TryAttachExternalTrackLocalFile(ExternalTrackLocalFileRequest request, out int trackId);
    }
    public class LibraryIndexer : ILibraryIndexer
    {
        private static readonly string[] preferredCoverBaseNames = [
            "cover",
            "folder",
            "front",
            "album",
            "artwork",
            "art"
            ];
        private static readonly string[] suportedCoverExtensions = [
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".bmp",
            ];

        private readonly MusicLibrary _library;
        private readonly Lock _lock = new();

        private readonly Dictionary<(long Size, long Ticks), Track> _fingerprint;
        private readonly ImageProcessor _imageProcessor;

        public LibraryIndexer(MusicLibrary library, ImageProcessor imageProcessor)
        {
            _library = library;
            _imageProcessor = imageProcessor;

            _fingerprint = new Dictionary<(long Size, long Ticks), Track>(_library.Tracks.Count);
            lock (_lock)
            {
                foreach (var t in _library.Tracks)
                {
                    _fingerprint[(t.FileSize, t.LastWriteTime)] = t;
                }
            }
        }

        public Task IndexFileAsync(string filePath, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(filePath);
                var LastModifiedUtc = System.IO.File.GetLastWriteTimeUtc(filePath);
                var fileSize = fileInfo.Length;

                // Check if track already exists
                var existingTrack = FindExistingTrack(fileSize, LastModifiedUtc);
                if (existingTrack != null)
                {
                    // Update path if changed
                    if (!string.Equals(existingTrack.Path, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        lock (_lock)
                        {
                            existingTrack.Path = filePath;
                        }
                    }
                    return;
                }

                using var tagFile = TagLib.File.Create(filePath);

                int[] genreIds = [];
                if (tagFile.Tag.Genres.Length > 0)
                {
                    foreach (var genre in tagFile.Tag.Genres)
                    {
                        var genreNames = genre.Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var genreName in genreNames)
                        {
                            var trimmedGenre = genreName.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmedGenre))
                            {
                                var genreId = GetOrCreateGenre(trimmedGenre);
                                genreIds = [.. genreIds, genreId];
                            }
                        }
                    }
                }

                // Track artist
                int[] trackArtists = [];
                if (tagFile.Tag.Performers.Length > 0)
                {
                    var artistsNames = tagFile.Tag.Performers
                        //.SelectMany(p => p.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(name => name.Trim())
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToArray();

                    foreach (var performer in artistsNames)
                    {
                        var artistId = GetOrCreateArtist(performer);
                        trackArtists = [.. trackArtists, artistId];
                    }
                }

                // Album artist
                int[] albumArtists = [];
                if (tagFile.Tag.AlbumArtists.Length > 0)
                {
                    var albumArtistNames = tagFile.Tag.AlbumArtists
                        //.SelectMany(p => p.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(name => name.Trim())
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToArray();

                    foreach (var performer in albumArtistNames)
                    {
                        var artistId = GetOrCreateArtist(performer);
                        albumArtists = [.. albumArtists, artistId];
                    }
                }

                if (trackArtists.Length == 0 && albumArtists.Length > 0)
                {
                    trackArtists = albumArtists;
                }
                else if (albumArtists.Length == 0 && trackArtists.Length > 0)
                {
                    albumArtists = trackArtists;
                }
                else if (trackArtists.Length == 0 && albumArtists.Length == 0)
                {
                    var unknownArtistId = GetOrCreateArtist("Unknown Artist");
                    trackArtists = [unknownArtistId];
                    albumArtists = [unknownArtistId];
                }

                // Cover
                int coverId = 0;
                var picture = tagFile.Tag.Pictures?.FirstOrDefault();
                if (picture is not null && picture.Data?.Data is { Length: > 0 } bytes)
                {
                    coverId = GetOrCreateCoverAsset(bytes, picture.MimeType);
                }
                else if (TryGetExternalCover(filePath, out var externalCoverBytes, out var externalMimeType))
                {
                    coverId = GetOrCreateCoverAsset(externalCoverBytes, externalMimeType);
                }

                // Album
                int albumId = 0;
                string albumName = tagFile.Tag.Album ?? tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(filePath);
                albumId = GetOrCreateAlbum(
                    albumName,
                    albumArtists,
                    trackArtists,
                    (int)tagFile.Tag.Year,
                    coverId
                );

                // Track
                lock (_lock)
                {

                    var track = new Track
                    {
                        Id = _library.GenerateTrackId(),
                        Path = filePath,
                        Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(filePath),
                        ArtistIds = trackArtists,
                        AlbumId = albumId,
                        Duration = (int)tagFile.Properties.Duration.TotalSeconds,
                        FileSize = fileSize,
                        CoverId = coverId,
                        LastWriteTime = LastModifiedUtc.Ticks,
                        Disk = (int)tagFile.Tag.Disc,
                        TrackNumber = (int)tagFile.Tag.Track,
                        GenreIds = genreIds,
                        SamplingRate = tagFile.Properties.AudioSampleRate,
                        Bitrate = tagFile.Properties.AudioBitrate,
                        Channels = tagFile.Properties.AudioChannels,
                        BitDeph = tagFile.Properties.BitsPerSample
                    };
                    _library.Tracks.Add(track);
                    _fingerprint[(track.FileSize, track.LastWriteTime)] = track;
                }
            }, ct);

        }

        public ExternalTrackIndexResult IndexExternalTrack(ExternalTrackIndexRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.SourceUri)) throw new ArgumentException("SourceUri is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.ExternalId)) throw new ArgumentException("ExternalId is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("Title is required.", nameof(request));

            lock (_lock)
            {
                var existing = _library.Tracks.FirstOrDefault(t =>
                    t.Origin == request.Origin &&
                    string.Equals(t.ExternalId, request.ExternalId, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    return new ExternalTrackIndexResult
                    {
                        Created = false,
                        TrackId = existing.Id,
                        CoverId = existing.CoverId
                    };
                }

                int[] artistIds = [];
                var artistsNames = request.ArtistName.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var artistName in artistsNames)
                {
                    var artistId = GetOrCreateArtist(artistName);
                    artistIds = [.. artistIds, artistId];
                }

                int coverId = 0;
                if (request.ThumbnailBytes is { Length: > 0 } &&
                    !string.IsNullOrWhiteSpace(request.ThumbnailMimeType))
                {
                    coverId = GetOrCreateCoverAsset(request.ThumbnailBytes, request.ThumbnailMimeType!);
                }

                int albumId = GetOrCreateAlbum(
                    string.IsNullOrWhiteSpace(request.AlbumName) ? "Unknown Album" : request.AlbumName,
                    artistIds,
                    artistIds,
                    request.Year,
                    coverId);

                var track = new Track
                {
                    Id = _library.GenerateTrackId(),
                    Path = string.Empty,
                    Title = request.Title.Trim(),
                    ArtistIds = artistIds,
                    AlbumId = albumId,
                    GenreIds = [],
                    Duration = Math.Max(0, request.DurationSeconds),
                    FileSize = 0,
                    LastWriteTime = DateTime.UtcNow.Ticks,
                    Disk = 0,
                    TrackNumber = 0,
                    SamplingRate = 0,
                    Bitrate = 0,
                    Channels = 0,
                    BitDeph = 0,
                    SourceUri = request.SourceUri.Trim(),
                    ExternalId = request.ExternalId.Trim(),
                    Origin = request.Origin,
                    CoverId = coverId
                };

                _library.Tracks.Add(track);

                return new ExternalTrackIndexResult
                {
                    Created = true,
                    TrackId = track.Id,
                    CoverId = coverId
                };
            }
        }

        public ExternalTrackIndexResult UpsertExternalTrack(ExternalTrackIndexRequest request, bool updateExistingMetadata)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            lock (_lock)
            {
                var existing = _library.Tracks.FirstOrDefault(t =>
                                    t.Origin == request.Origin &&
                                    string.Equals(t.ExternalId, request.ExternalId, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    return IndexExternalTrack(request);
                }

                if (!updateExistingMetadata)
                {
                    return new ExternalTrackIndexResult
                    {
                        Created = false,
                        TrackId = existing.Id,
                        CoverId = existing.CoverId
                    };
                }

                int[] artistIds = [];
                var artistsNames = request.ArtistName.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var artistName in artistsNames)
                {
                    var artistId = GetOrCreateArtist(artistName);
                    artistIds = [.. artistIds, artistId];
                }

                int albumId = GetOrCreateAlbum(
                    string.IsNullOrWhiteSpace(request.AlbumName) ? "Unknown Album" : request.AlbumName,
                    artistIds,
                    artistIds,
                    request.Year,
                    existing.CoverId);

                existing.Title = request.Title.Trim();
                existing.ArtistIds = artistIds;
                existing.AlbumId = albumId;
                if (request.DurationSeconds > 0)
                {
                    existing.Duration = request.DurationSeconds;
                }

                return new ExternalTrackIndexResult
                {
                    Created = false,
                    TrackId = existing.Id,
                    CoverId = existing.CoverId
                };

            }
        }

        public bool TryAttachExternalTrackLocalFile(ExternalTrackLocalFileRequest request, out int trackId)
        {
            trackId = 0;
            if (request is null || string.IsNullOrWhiteSpace(request.ExternalId) || string.IsNullOrWhiteSpace(request.FilePath))
                return false;
            if (!System.IO.File.Exists(request.FilePath))
                return false;

            lock (_lock)
            {
                var track = _library.Tracks.FirstOrDefault(t =>
                    t.Origin == request.Origin &&
                    string.Equals(t.ExternalId, request.ExternalId, StringComparison.OrdinalIgnoreCase));

                if (track is null) return false;

                track.Path = request.FilePath;

                try
                {
                    using var tagFile = TagLib.File.Create(request.FilePath);
                    track.SamplingRate = tagFile.Properties.AudioSampleRate;
                    track.Bitrate = tagFile.Properties.AudioBitrate;
                    track.Channels = tagFile.Properties.AudioChannels;
                    track.BitDeph = tagFile.Properties.BitsPerSample;
                    if (track.Duration <= 0)
                    {
                        track.Duration = (int)tagFile.Properties.Duration.TotalSeconds;
                    }
                    track.FileSize = new FileInfo(request.FilePath).Length;
                    track.LastWriteTime = System.IO.File.GetLastWriteTimeUtc(request.FilePath).Ticks;

                }
                catch
                {

                }
                trackId = track.Id;
                return true;
            }
        }

        #region  Internal
        private static string NormalizeKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var chars = value
                .Trim()
                .ToLowerInvariant()
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray();

            return new string(chars);
        }
        private int GetOrCreateArtist(string artistName)
        {
            // Normalizar nombre vacío (no debería llegar aquí, pero por seguridad)
            if (string.IsNullOrWhiteSpace(artistName))
                artistName = "Unknown Artist";

            string normalized = NormalizeKey(artistName);

            lock (_lock)
            {
                var artist = _library.Artists.FirstOrDefault(a => NormalizeKey(a.Name) == normalized);

                if (artist != null)
                {
                    return artist.Id;
                }
                else
                {
                    var newArtist = new Artist
                    {
                        Id = _library.GenerateArtistId(),
                        Name = artistName.Trim()
                    };
                    _library.Artists.Add(newArtist);
                    return newArtist.Id;
                }
            }
        }
        private int GetOrCreateAlbum(string albumName, int[] albumArtistIds, int[] trackArtistIds, int year, int coverId)
        {
            if (string.IsNullOrWhiteSpace(albumName)) albumName = "Unknown Album";

            int[] preferredArtistIds = (albumArtistIds is { Length: > 0 }) ? albumArtistIds : trackArtistIds ?? [];

            lock (_lock)
            {
                var album = _library.Albums.FirstOrDefault(a =>
                                string.Equals(a.Title, albumName, StringComparison.OrdinalIgnoreCase) &&
                                a.ArtistIds.SequenceEqual(preferredArtistIds)
                                );

                if (album != null)
                {
                    if (album.CoverId == 0 && coverId != 0)
                    {
                        album.CoverId = coverId;
                    }
                    return album.Id;
                }
                else
                {
                    var newAlbum = new Album
                    {
                        Id = _library.GenerateAlbumId(),
                        Title = albumName,
                        ArtistIds = preferredArtistIds,
                        Year = year,
                        CoverId = coverId
                    };
                    _library.Albums.Add(newAlbum);
                    return newAlbum.Id;
                }

            }
        }

        private int GetOrCreateCoverAsset(byte[] imageBytes, string mimeType)
        {
            if (imageBytes is null || imageBytes.Length == 0) return 0;

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(imageBytes);
            var fingerprint = Convert.ToHexString(hashBytes);

            string baseFileName;

            lock (_lock)
            {
                var existing = _library.CoverAssets
                .FirstOrDefault(c => string.Equals(c.Fingerprint, fingerprint, StringComparison.Ordinal));

                if (existing is not null) return existing.Id;

                baseFileName = fingerprint.GetHashCode().ToString("X8") + ".png"; // always save as PNG for consistency
            }

            var colors = _imageProcessor.ProcessPipeline(imageBytes, baseFileName);

            lock (_lock)
            {
                var existing = _library.CoverAssets.FirstOrDefault(c => string.Equals(c.Fingerprint, fingerprint, StringComparison.Ordinal));

                if (existing is not null) return existing.Id;

                var asset = new CoverAsset
                {
                    Id = _library.GenerateCoverId(),
                    FileName = baseFileName,
                    Fingerprint = fingerprint,
                    DominantColorHex = colors.DominantColorHex,
                    DominantForegroundHex = colors.DominantForegroundHex,
                    HighlightColorHex = colors.HighlightColorHex,
                    HighlightForegroundHex = colors.HighlightForegroundHex
                };

                _library.CoverAssets.Add(asset);
                return asset.Id;
            }
        }

        private int GetOrCreateGenre(string genreName)
        {
            if (string.IsNullOrWhiteSpace(genreName)) genreName = "Unknown Genre";
            lock (_lock)
            {
                var genre = _library.Genres.FirstOrDefault(g => string.Equals(g.Name, genreName, StringComparison.OrdinalIgnoreCase));
                if (genre != null)
                {
                    return genre.Id;
                }
                else
                {
                    var newGenre = new Genre
                    {
                        Id = _library.GenerateGenreId(),
                        Name = genreName
                    };
                    _library.Genres.Add(newGenre);
                    return newGenre.Id;
                }
            }

        }

        private Track? FindExistingTrack(long fileSize, DateTime lastModifiedUtc)
        {
            long ticks = lastModifiedUtc.Ticks;
            lock (_lock)
            {
                return _fingerprint.TryGetValue((fileSize, ticks), out var t) ? t : null;

            }
        }

        private static bool TryGetExternalCover(string audioFilePath, out byte[] imageBytes, out string mimeType)
        {
            imageBytes = [];
            mimeType = "application/octet-stream";

            var directory = Path.GetDirectoryName(audioFilePath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return false;
            }
            const long maxCoverSizeBytes = 20 * 1024 * 1024; // 20 MB
            try
            {
                var bestCandidate = Directory
                    .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(path => suportedCoverExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                    .Select(path => new FileInfo(path))
                    .Where(file => file.Exists && file.Length > 0 && file.Length <= maxCoverSizeBytes)
                    .OrderBy(file => GetCoverNamePriority(Path.GetFileNameWithoutExtension(file.Name)))
                    .ThenBy(file => file.Length)
                    .FirstOrDefault();

                if (bestCandidate is null) return false;

                imageBytes = System.IO.File.ReadAllBytes(bestCandidate.FullName);
                mimeType = GetMimeTypeFromExtension(bestCandidate.Extension);

                return imageBytes.Length > 0;

            }
            catch
            {
                return false;
            }
        }

        private static int GetCoverNamePriority(string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
                return int.MaxValue;

            var name = baseName.Trim().ToLowerInvariant();

            for (int i = 0; i < preferredCoverBaseNames.Length; i++)
            {
                if (name.Equals(preferredCoverBaseNames[i], StringComparison.Ordinal))
                    return i;

                if (name.StartsWith(preferredCoverBaseNames[i], StringComparison.Ordinal))
                    return i + 10;
            }

            return int.MaxValue;
        }
        private static string GetMimeTypeFromExtension(string extension) => extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };

        #endregion
    }
    public sealed class ExternalTrackIndexRequest
    {
        public required TrackOrigin Origin { get; init; } = TrackOrigin.Youtube;
        public required string SourceUri { get; init; }
        public required string ExternalId { get; init; }

        public required string Title { get; init; }
        public string ArtistName { get; init; } = "Unknown Artist";
        public string AlbumName { get; init; } = "Unknown Album";

        public int Year { get; init; } = 0;
        public int DurationSeconds { get; init; } = 0;

        public byte[]? ThumbnailBytes { get; init; }
        public string? ThumbnailMimeType { get; init; }
    }

    public sealed class ExternalTrackLocalFileRequest
    {
        public required TrackOrigin Origin { get; init; } = TrackOrigin.Youtube;
        public required string ExternalId { get; init; }
        public required string FilePath { get; init; }
        public bool PreferExistingArtistAlbumMatch { get; init; } = true;
    }

    public sealed class ExternalTrackIndexResult
    {
        public bool Created { get; init; }
        public int TrackId { get; init; }
        public int CoverId { get; init; }
    }
}
