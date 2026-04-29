using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using TagLib;

namespace MusicWrap.Core.Services.Library
{

    public interface ILibraryIndexer
    {
        void IndexFileAsync(string filePath);
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

        private const int SmallCoverSize = 64;
        private const int MediumCoverSize = 180;
        private const int LargeCoverSize = 360;
        private const int BlurCoverSize = 512;

        private readonly MusicLibrary _library;
        private readonly object _lock = new();

        public LibraryIndexer(MusicLibrary library)
        {
            _library = library;
        }

        public void IndexFileAsync(string filePath)
        {
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
                var artistsNames = tagFile.Tag.Performers.SelectMany(p => p.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
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
                var albumArtistNames = tagFile.Tag.AlbumArtists.SelectMany(p => p.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
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
            }

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

            const int segmentSize = 32;
            var length = imageBytes.Length;

            var headSize = Math.Min(segmentSize, length);
            var tailSize = Math.Min(segmentSize, length - headSize);

            var buffer = new byte[headSize + tailSize];
            if (headSize > 0)
                Array.Copy(imageBytes, 0, buffer, 0, headSize);
            if (tailSize > 0)
                Array.Copy(imageBytes, length - tailSize, buffer, headSize, tailSize);

            var core = Convert.ToBase64String(buffer);
            var fingerprint = $"{length}:{core}";

            string baseFileName;

            lock (_lock)
            {
                var existing = _library.CoverAssets
                .FirstOrDefault(c => string.Equals(c.Fingerprint, fingerprint, StringComparison.Ordinal));

                if (existing is not null) return existing.Id;

                var ext = mimeType switch
                {
                    "image/jpeg" or "image/jpg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => ".jpeg"
                };

                baseFileName = fingerprint.GetHashCode().ToString("X8") + ext;
            }

            var variants = EnsureImageVariants(imageBytes, baseFileName);

            lock (_lock)
            {
                var existing = _library.CoverAssets
                    .FirstOrDefault(c => string.Equals(c.Fingerprint, fingerprint, StringComparison.Ordinal));

                if (existing is not null) return existing.Id;

                var asset = new CoverAsset
                {
                    Id = _library.GenerateCoverId(),
                    FileName = variants.MainFileName,
                    Fingerprint = fingerprint,
                    DominantColorHex = variants.DominantColorHex,
                    ForegroundColorHex = variants.ForegroundColorHex,
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
            long lastModifiedTicks = lastModifiedUtc.Ticks;
            lock (_lock)
            {
                return _library.Tracks.FirstOrDefault(t => t.FileSize == fileSize && t.LastWriteTime == lastModifiedTicks);
            }
        }

        private ImageVariantsResult EnsureImageVariants(byte[] imageBytes, string baseFileName)
        {
            var result = new ImageVariantsResult
            {
                MainFileName = baseFileName,
                SmallFileName = baseFileName,
                MediumFileName = baseFileName,
                LargeFileName = baseFileName,
                BlurFileName = baseFileName
            };

            try
            {
                Directory.CreateDirectory(MusicWrapDirectories.CoverDirectory);
                Directory.CreateDirectory(MusicWrapDirectories.SmallImageDirectory);
                Directory.CreateDirectory(MusicWrapDirectories.MediumImageDirectory);
                Directory.CreateDirectory(MusicWrapDirectories.LargeImageDirectory);
                Directory.CreateDirectory(MusicWrapDirectories.BlurImageDirectory);

                // save original image
                var mainPath = Path.Combine(MusicWrapDirectories.CoverDirectory, baseFileName);
                if (!Directory.Exists(mainPath))
                {
                    System.IO.File.WriteAllBytes(mainPath, imageBytes);
                }

                // decode image
                using var sourceImage = Image.Load<Rgba32>(imageBytes);

                (result.DominantColorHex, result.ForegroundColorHex) = ExtractColorsFromImage(imageBytes);

                // save variants
                var smallPath = Path.Combine(MusicWrapDirectories.SmallImageDirectory, baseFileName);
                SaveImageVariant(sourceImage, smallPath, SmallCoverSize);

                var mediumPath = Path.Combine(MusicWrapDirectories.MediumImageDirectory, baseFileName);
                SaveImageVariant(sourceImage, mediumPath, MediumCoverSize);

                var largePath = Path.Combine(MusicWrapDirectories.LargeImageDirectory, baseFileName);
                SaveImageVariant(sourceImage, largePath, LargeCoverSize);

                var blurPath = Path.Combine(MusicWrapDirectories.BlurImageDirectory, baseFileName);
                SaveBlurVariant(sourceImage, blurPath, result.DominantColorHex);

                return result;
            }
            catch { return result; }
        }
        private static void SaveImageVariant(Image<Rgba32> sourceImage, string outputPath, int maxSize)
        {
            try
            {
                if (System.IO.File.Exists(outputPath))
                    return;

                using var variant = sourceImage.Clone();
                variant.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxSize, maxSize),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3,
                }));

                var encoder = GetImageEncoder(outputPath);
                variant.Save(outputPath, encoder);
            }
            catch { }
        }
        private static IImageEncoder GetImageEncoder(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".png" => new SixLabors.ImageSharp.Formats.Png.PngEncoder(),
                ".webp" => new SixLabors.ImageSharp.Formats.Webp.WebpEncoder(),
                _ => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                {
                    Quality = 90,
                    ColorType = SixLabors.ImageSharp.Formats.Jpeg.JpegEncodingColor.YCbCrRatio444
                }
            };
        }

        private static void SaveBlurVariant(Image<Rgba32> sourceImage, string outputPath, string dominantColorHex)
        {
            try
            {
                if (System.IO.File.Exists(outputPath))
                    return;

                var dominantColor = new Rgba32(
                    Convert.ToByte(dominantColorHex.Substring(1, 2), 16),
                    Convert.ToByte(dominantColorHex.Substring(3, 2), 16),
                    Convert.ToByte(dominantColorHex.Substring(5, 2), 16),
                    255);
                using var blurImage = sourceImage.Clone();

                blurImage.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(BlurCoverSize, BlurCoverSize),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3,
                })
                .GaussianBlur(32f));

                using var dominantLayer = new Image<Rgba32>(blurImage.Width, blurImage.Height, dominantColor);

                const float domFactor = 0.8f;
                const float imgFactor = 1f - domFactor;

                blurImage.ProcessPixelRows(dominantLayer, (imgAccesor, domAccesor) =>
                {
                    for (int y = 0; y < blurImage.Height; y++)
                    {
                        var imgRow = imgAccesor.GetRowSpan(y);
                        var domRow = domAccesor.GetRowSpan(y);
                        for (int x = 0; x < imgRow.Length; x++)
                        {
                            var imgPixel = imgRow[x];
                            var domPixel = domRow[x];

                            // blend
                            imgRow[x] = new Rgba32(
                                (byte)((domPixel.R * domFactor) + (imgPixel.R * imgFactor)),
                                (byte)((domPixel.G * domFactor) + (imgPixel.G * imgFactor)),
                                (byte)((domPixel.B * domFactor) + (imgPixel.B * imgFactor)),
                                255);
                        }
                    }
                });

                AddNoiseGrain(blurImage, 0.08f, 3);

                blurImage.Mutate(x => x.Brightness(0.98f).Saturate(0.9f).GaussianBlur(1.2f));

                blurImage.SaveAsJpeg(outputPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                {
                    Quality = 99,
                    ColorType = SixLabors.ImageSharp.Formats.Jpeg.JpegEncodingColor.YCbCrRatio444
                });

            }
            catch { }
        }

        private static void AddNoiseGrain(Image<Rgba32> image, float intensity, int grainSize)
        {
            var random = new Random(42);
            int width = image.Width;
            int height = image.Height;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        if (random.NextSingle() > (1f - intensity))
                        {
                            var pixel = row[x];

                            int noise = random.Next(-grainSize, grainSize + 1);

                            byte r = (byte)Math.Clamp(pixel.R + noise, 0, 255);
                            byte g = (byte)Math.Clamp(pixel.G + noise, 0, 255);
                            byte b = (byte)Math.Clamp(pixel.B + noise, 0, 255);

                            row[x] = new Rgba32(r, g, b, 255);
                        }
                    }
                }
            });
        }

        public static (string dominantColor, string foregroundColor) ExtractColorsFromImage(byte[] imageBytes)
        {
            try
            {
                using var image = Image.Load<Rgba32>(imageBytes);
                image.Mutate(x => x.Resize(128, 128));

                var counts = new Dictionary<Rgba32, int>();
                int validPixels = 0;

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            var pixel = row[x];
                            if (!IsValidColor(pixel))
                                continue;

                            var q = QuantizeColor(pixel);
                            counts[q] = counts.TryGetValue(q, out var c) ? c + 1 : 1;
                            validPixels++;
                        }
                    }
                });

                if (validPixels == 0 || counts.Count == 0)
                    return ("#404040", "#FFFFFF");

                int minCount = Math.Max(1, (int)(validPixels * 0.008)); // 0.8%
                var filtered = counts.Where(kv => kv.Value >= minCount).ToList();
                if (filtered.Count == 0)
                    filtered = counts.ToList();

                var dominant = filtered
                    .OrderByDescending(kv => kv.Value)
                    .ThenByDescending(kv =>
                    {
                        var hsv = ColorSpaceConverter.ToHsv(kv.Key);
                        return hsv.S * 0.7f + hsv.V * 0.3f;
                    })
                    .First()
                    .Key;

                dominant = BoostSaturation(dominant, 0.14f);

                string bg = ToHex(dominant);
                string fg = GetContrastColor(dominant);
                return (bg, fg);
            }
            catch
            {
                return ("#404040", "#FFFFFF");
            }
        }
        private static Rgba32 QuantizeColor(Rgba32 pixel)
        {
            const int factor = 32;

            return new Rgba32(
                (byte)((pixel.R / factor) * factor),
                (byte)((pixel.G / factor) * factor),
                (byte)((pixel.B / factor) * factor),
                255);
        }

        private static Rgba32 BoostSaturation(Rgba32 color, float minSaturation)
        {
            var hsv = ColorSpaceConverter.ToHsv(color);

            if (hsv.S < 0.06f) return color;
            if (hsv.V > 0.92f && hsv.S < 0.18f) return color;

            float s = hsv.S;
            if (s < minSaturation)
            {
                s = s + (minSaturation - s) * 0.35f;
            }

            float v = MathF.Min(1f, hsv.V * 1.01f);

            var adjusted = new Hsv(hsv.H, s, v);
            var rgb = ColorSpaceConverter.ToRgb(adjusted);

            return new Rgba32(
                (byte)(rgb.R * 255),
                (byte)(rgb.G * 255),
                (byte)(rgb.B * 255),
                255);
        }
        private static bool IsValidColor(Rgba32 color)
        {
            return color.A > 25;
        }
        private static string GetContrastColor(Rgba32 bg)
        {
            double r = bg.R / 255.0;
            double g = bg.G / 255.0;
            double b = bg.B / 255.0;

            double luminance =
                0.2126 * r +
                0.7152 * g +
                0.0722 * b;

            return luminance > 0.5
                ? "#000000"
                : "#FFFFFF";
        }
        private static string ToHex(Rgba32 color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
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

        #region Private Classes
        private sealed class ImageVariantsResult
        {
            public string MainFileName { get; set; } = string.Empty;
            public string SmallFileName { get; set; } = string.Empty;
            public string MediumFileName { get; set; } = string.Empty;
            public string LargeFileName { get; set; } = string.Empty;
            public string BlurFileName { get; set; } = string.Empty;
            public string DominantColorHex { get; set; } = "#808080";
            public string ForegroundColorHex { get; set; } = "#FFFFFF";
        }
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
