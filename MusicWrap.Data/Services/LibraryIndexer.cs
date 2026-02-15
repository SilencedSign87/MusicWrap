using MusicWrap.Data.Library;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace MusicWrap.Data.Services
{
    public interface ILibraryIndexer
    {
        void IndexFileAsync(string filePath);
    }
    public class LibraryIndexer : ILibraryIndexer
    {
        private readonly MusicLibrary _library;
        private readonly object _lock = new();

        public LibraryIndexer(MusicLibrary library)
        {
            _library = library;
        }

        public void IndexFileAsync(string filePath)
        {

            using var tagFile = TagLib.File.Create(filePath);

            var LastModifiedUtc = File.GetLastWriteTimeUtc(filePath);
            var fileSize = new FileInfo(filePath).Length;

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

            // Genre
            int[] genreIds = [];
            if (tagFile.Tag.Genres.Length > 0)
            {
                int[] Genres = [];
                foreach (var genre in tagFile.Tag.Genres)
                {
                    var genreId = GetOrCreateGenre(genre);
                    genreIds = [.. genreIds, genreId];
                }

            }

            // Track artist (con fallback a album artist)
            int[] trackArtists = [];
            if (tagFile.Tag.Performers.Length > 0)
            {
                foreach (var performer in tagFile.Tag.Performers)
                {
                    var artistId = GetOrCreateArtist(performer);
                    trackArtists = [.. trackArtists, artistId];
                }
            }

            // Album artist (con fallback a track artist)
            int[] albumArtists = [];
            if (tagFile.Tag.AlbumArtists.Length > 0)
            {
                foreach (var performer in tagFile.Tag.AlbumArtists)
                {
                    var artistId = GetOrCreateArtist(performer);
                    albumArtists = [.. albumArtists, artistId];
                }
            }

            // Fallbacks: asegurar que ambos tengan al menos un artista
            if (trackArtists.Length == 0 && albumArtists.Length > 0)
            {
                // Track sin artistas -> usar album artists
                trackArtists = albumArtists;
            }
            else if (albumArtists.Length == 0 && trackArtists.Length > 0)
            {
                // Album sin artistas -> usar track artists
                albumArtists = trackArtists;
            }
            else if (trackArtists.Length == 0 && albumArtists.Length == 0)
            {
                // Ninguno tiene artistas -> usar "Unknown Artist"
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
                };
                _library.Tracks.Add(track);
            }

        }

        #region  Internal
        private int GetOrCreateArtist(string artistName)
        {
            // Normalizar nombre vacío (no debería llegar aquí, pero por seguridad)
            if (string.IsNullOrWhiteSpace(artistName))
                artistName = "Unknown Artist";

            lock (_lock)
            {
                var artist = _library.Artists.FirstOrDefault(a => string.Equals(a.Name, artistName, StringComparison.OrdinalIgnoreCase));

                if (artist != null)
                {
                    return artist.Id;
                }
                else
                {
                    var newArtist = new Artist
                    {
                        Id = _library.GenerateArtistId(),
                        Name = artistName
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
                    _ => ".bin"
                };
                var filename = fingerprint.GetHashCode().ToString("X8") + ext;
                var fullPath = Path.Combine(AppPaths.CoversDir, filename);
                if (!File.Exists(fullPath))
                    File.WriteAllBytes(fullPath, imageBytes);

                var (domminantColor, foregroundColor) = ExtractColorsFromImage(imageBytes);

                var asset = new CoverAsset
                {
                    Id = _library.GenerateCoverId(),
                    FileName = filename,
                    Fingerprint = fingerprint,
                    DominantColorHex = domminantColor,
                    ForegroundColorHex = foregroundColor
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

        public static (string dominantColor, string foregroundColor) ExtractColorsFromImage(byte[] imageBytes)
        {
            try
            {
                using var image = Image.Load<Rgba32>(imageBytes);

                // Reducir tamaño para rendimiento
                image.Mutate(x => x.Resize(64, 64));

                var colorScores = new Dictionary<Rgba32, double>();

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

                            // Convertir a HSV
                            var hsv = ColorSpaceConverter.ToHsv(pixel);

                            // Ignorar colores casi grises
                            if (hsv.S < 0.15f)
                                continue;

                            var quantized = QuantizeColor(pixel);

                            // Score ponderado
                            double score =
                                hsv.S *      // saturación
                                hsv.V;       // brillo

                            if (colorScores.ContainsKey(quantized))
                                colorScores[quantized] += score;
                            else
                                colorScores[quantized] = score;
                        }
                    }
                });

                Rgba32 dominant;

                // Si encontramos colores saturados
                if (colorScores.Any())
                {
                    dominant = colorScores
                        .OrderByDescending(x => x.Value)
                        .First().Key;
                }
                else
                {
                    // Fallback: usar el más frecuente
                    dominant = GetMostFrequentColor(image);
                }

                // Forzar saturación mínima
                dominant = BoostSaturation(dominant, 0.4f);

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

            float s = hsv.S < minSaturation ? minSaturation : hsv.S;
            float v = MathF.Min(1f, hsv.V * 1.05f);

            var adjustedHsv = new Hsv(hsv.H, s, v);

            var rgb = ColorSpaceConverter.ToRgb(adjustedHsv);

            return new Rgba32(
                (byte)(rgb.R * 255),
                (byte)(rgb.G * 255),
                (byte)(rgb.B * 255),
                255);
        }
        private static bool IsValidColor(Rgba32 color)
        {
            // Filtrar colores muy oscuros o transparentes
            return color.A > 25 && (color.R > 15 || color.G > 15 || color.B > 15);
        }
        private static Rgba32 GetMostFrequentColor(Image<Rgba32> image)
        {
            var counts = new Dictionary<Rgba32, int>();

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);

                    for (int x = 0; x < row.Length; x++)
                    {
                        var q = QuantizeColor(row[x]);

                        counts[q] = counts.TryGetValue(q, out var c)
                            ? c + 1
                            : 1;
                    }
                }
            });

            return counts
                .OrderByDescending(x => x.Value)
                .First()
                .Key;
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

        #endregion
    }
}
