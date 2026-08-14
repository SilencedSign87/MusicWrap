using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Helpers;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.Services;
using MusicWrap.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media.Imaging;
using static MusicWrap.UI.Features.Metadata.Viewmodels.FileDataBuilder;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataGeneralViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        public static string MultipleValuesString = "-- Multiple Values --";

        private readonly MetadataEditorWorkspace _workspace;
        private readonly ILibraryService _libraryService;
        private readonly IwindowsImageService _imageService;
        private readonly TrackActionService _trackActionService;
        private readonly ILogger _logger;

        private int _loadVersion;

        [ObservableProperty]
        public partial bool IsLoading { get; set; }
        [ObservableProperty]
        public partial string Filepath { get; set; } = string.Empty;
        [ObservableProperty]
        public partial bool HasMultipleImages { get; set; }
        public string MultipleImagesPlaceholder => MultipleValuesString;
        public ObservableCollection<BitmapImage> Images { get; } = [];
        public ObservableCollection<FileData> Rows { get; } = [];

        public MetadataGeneralViewmodel(MetadataEditorWorkspace workspace, ILibraryService libraryService, IwindowsImageService imageService, TrackActionService trackActionService, ILogger<MetadataGeneralViewmodel> logger)
        {
            _workspace = workspace;
            _libraryService = libraryService;
            _imageService = imageService;
            _trackActionService = trackActionService;
            _logger = logger;
        }

        public void Load()
        {
            int version = ++_loadVersion;

            Images.Clear();
            Rows.Clear();
            Filepath = MultipleValuesString;
            HasMultipleImages = false;
            IsLoading = true;

            var trackIds = _workspace.TrackIds.ToList();

            _ = LoadCoreAsync(trackIds, version);
        }

        private async Task LoadCoreAsync(IReadOnlyList<int> trackIds, int version)
        {
            try
            {
                var result = await Task.Run(() => BuildData(trackIds));
                if (version != _loadVersion)
                    return;
                Filepath = result.Filepath;
                HasMultipleImages = result.HasMultipleImages;
                foreach (var row in result.Rows)
                    Rows.Add(row);
                foreach (var image in result.Images)
                    Images.Add(image);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading metadata.");
            }
            finally
            {
                if (version == _loadVersion)
                    IsLoading = false;
            }
        }
        private (string Filepath, bool HasMultipleImages, IReadOnlyList<FileData> Rows, IReadOnlyList<BitmapImage> Images) BuildData(IReadOnlyList<int> trackIds)
        {
            var tracks = trackIds
                .Select(_libraryService.GetTrackById)
                .Where(t => t is not null && t.Origin == TrackOrigin.Local)
                .Cast<Track>()
                .ToList();
            if (tracks.Count == 0)
                return (MultipleValuesString, false, [], []);
            // Lectura paralela de archivos, escribiendo por índice para preservar el orden
            var perTrack = new (IReadOnlyList<FileData> Rows, List<byte[]> ImageBytes)?[tracks.Count];
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) };
            Parallel.ForEach(Enumerable.Range(0, tracks.Count), options, i =>
            {
                perTrack[i] = ReadTrack(tracks[i]);
            });
            var readable = perTrack.Where(r => r is not null).Select(r => r!.Value).ToList();
            if (readable.Count == 0)
                return (MultipleValuesString, false, [], []);
            string filepath = tracks.Count == 1 ? tracks[0].Path : MultipleValuesString;
            var rows = FileDataBuilder.Merge(readable.Select(r => r.Rows).ToList());
            var firstBytes = readable[0].ImageBytes;
            bool imagesShared = readable.All(r => ImageCollectionsEqual(r.ImageBytes, firstBytes));
            var images = new List<BitmapImage>();
            if (imagesShared)
            {
                foreach (var bytes in firstBytes)
                    if (_imageService.LoadFromBytes(bytes) is { } image)
                        images.Add(image);
                if (images.Count == 0 && _imageService.GetDefaultImage(120) is { } fallback)
                    images.Add(fallback);
            }
            return (filepath, !imagesShared, rows, images);
        }
        private static (IReadOnlyList<FileData> Rows, List<byte[]> ImageBytes)? ReadTrack(Track track)
        {
            try
            {
                using var filetag = TagLib.File.Create(track.Path);
                var imageBytes = filetag.Tag.Pictures
                    .Where(p => p?.Data?.Data is { Length: > 0 })
                    .Select(p => p.Data.Data)
                    .ToList();
                return (FileDataBuilder.Build(filetag), imageBytes);
            }
            catch
            {
                return null;
            }
        }

        private static bool ImageCollectionsEqual(List<byte[]> a, List<byte[]> b)
           => a.Count == b.Count && a.Zip(b).All(pair => BytesEqual(pair.First, pair.Second));
        private static bool BytesEqual(byte[] a, byte[] b)
            => a.Length == b.Length && a.AsSpan().SequenceEqual(b);

        [RelayCommand]
        private void ShowInExplorer() => _trackActionService.ShowInFileExplorer(_workspace.TrackIds);
    }
    public sealed record FileData(string Label, string[] Values)
    {
        public bool IsMultiple => Values.Length > 1;
    }
    public static class FileDataBuilder
    {
        public static IReadOnlyList<FileData> Build(TagLib.File file)
        {
            var rows = new List<FileData>();
            if (file.Tag is { } tag)
            {
                AddRow(rows, "Title", tag.Title);
                AddRow(rows, "Album", tag.Album);
                AddRow(rows, "Album Artist", tag.AlbumArtists);
                AddRow(rows, "Artist", tag.Performers);
                AddRow(rows, "Composer", tag.Composers);
                AddRow(rows, "Publisher", tag.Publisher);
                AddRow(rows, "Copyright", tag.Copyright);
                AddRow(rows, "Conductor", tag.Conductor);
                AddRow(rows, "Genre", tag.Genres);
                AddRow(rows, "Year", tag.Year is 0 ? null : tag.Year.ToString(CultureInfo.InvariantCulture));
                AddRow(rows, "Track", tag.Track is 0 ? null : $"{tag.Track}/{tag.TrackCount}");
                AddRow(rows, "Disc", tag.Disc is 0 ? null : $"{tag.Disc}/{tag.DiscCount}");
                AddRow(rows, "BPM", tag.BeatsPerMinute is 0 ? null : tag.BeatsPerMinute.ToString());
                AddRow(rows, "Comment", tag.Comment);
                AddRow(rows, "Lyrics", tag.Lyrics);
            }
            if (file.Properties is { } props)
            {
                AddRow(rows, "Duration", FormatHelpers.FormatDuration(props.Duration));
                AddRow(rows, "Codec", props.Codecs.Select(c => c.Description).ToArray());
                AddRow(rows, "Bitrate", props.AudioBitrate > 0 ? $"{props.AudioBitrate} kbps" : null);
                AddRow(rows, "Sample Rate", props.AudioSampleRate > 0 ? $"{props.AudioSampleRate} Hz" : null);
                AddRow(rows, "Bit Depth", props.BitsPerSample > 0 ? $"{props.BitsPerSample} bit" : null);
                AddRow(rows, "Channels", props.AudioChannels > 0 ? props.AudioChannels.ToString() : null);
            }
            AddRow(rows, "Health", file.PossiblyCorrupt ? "Possibly Corrupt" : null);

            return rows;
        }
        public static List<FileData> Merge(IReadOnlyList<IReadOnlyList<FileData>> perTrackRows)
        {
            if (perTrackRows.Count == 0)
                return [];
            if (perTrackRows.Count == 1)
                return [.. perTrackRows[0]];
            var result = new List<FileData>();

            var orderedLabels = new List<string>();
            foreach (var rows in perTrackRows)
                foreach (var row in rows)
                    if (!orderedLabels.Contains(row.Label))
                        orderedLabels.Add(row.Label);
            foreach (var label in orderedLabels)
            {
                var perFile = perTrackRows
                    .Select(rows => rows.FirstOrDefault(r => r.Label == label))
                    .ToList();
                bool missingAnywhere = perFile.Any(r => r is null);
                bool allEqual = !missingAnywhere &&
                    perFile.Skip(1).All(r => r!.Values.SequenceEqual(perFile[0]!.Values));
                result.Add(allEqual ? perFile[0]! : new FileData(label, [MetadataGeneralViewmodel.MultipleValuesString]));
            }
            return result;
        }
        private static void AddRow(List<FileData> rows, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            rows.Add(new FileData(label, [value]));
        }
        private static void AddRow(List<FileData> rows, string label, IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
                return;
            var cleaned = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            if (cleaned.Length > 0)
                rows.Add(new FileData(label, cleaned));
        }

    }
}
