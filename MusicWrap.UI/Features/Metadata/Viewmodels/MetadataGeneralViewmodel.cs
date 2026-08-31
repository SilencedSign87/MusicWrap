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
using System.IO;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataGeneralViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        public static string MultipleValuesString = "Mixed Values";

        private readonly MetadataEditorWorkspace _workspace;
        private readonly ILibraryService _libraryService;
        private readonly TrackActionService _trackActionService;
        private readonly ILogger _logger;

        private int _loadVersion;

        [ObservableProperty]
        public partial bool IsLoading { get; set; }
        [ObservableProperty]
        public partial string Filepath { get; set; } = string.Empty;
        public ObservableCollection<FileData> Rows { get; } = [];

        public MetadataGeneralViewmodel(MetadataEditorWorkspace workspace, ILibraryService libraryService, TrackActionService trackActionService, ILogger<MetadataGeneralViewmodel> logger)
        {
            _workspace = workspace;
            _libraryService = libraryService;
            _trackActionService = trackActionService;
            _logger = logger;
        }

        public void Load()
        {
            int version = ++_loadVersion;
            Rows.Clear();
            Filepath = MultipleValuesString;
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
                foreach (var row in result.Rows)
                    Rows.Add(row);
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
        private (string Filepath, IReadOnlyList<FileData> Rows) BuildData(IReadOnlyList<int> trackIds)
        {
            var tracks = trackIds
                .Select(_libraryService.GetTrackById)
                .Where(t => t is not null && t.Origin == TrackOrigin.Local)
                .Cast<Track>()
                .ToList();
            if (tracks.Count == 0)
                return (MultipleValuesString, []);

            var perTrack = new IReadOnlyList<FileData>?[tracks.Count];
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) };
            Parallel.ForEach(Enumerable.Range(0, tracks.Count), options, i =>
            {
                perTrack[i] = ReadTrack(tracks[i]);
            });
            var readable = perTrack.Where(rows => rows is not null).Cast<IReadOnlyList<FileData>>().ToList();
            if (readable.Count == 0)
                return (MultipleValuesString, []);

            string filepath = tracks.Count == 1 ? tracks[0].Path : MultipleValuesString;
            var rows = FileDataBuilder.Merge(readable);
            return (filepath, rows);
        }
        private static IReadOnlyList<FileData>? ReadTrack(Track track)
        {
            try
            {
                using var filetag = TagLib.File.Create(track.Path);
                return FileDataBuilder.Build(filetag, track.Path);
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand]
        private void ShowInExplorer() => _trackActionService.ShowInFileExplorer(_workspace.TrackIds);
    }
    public sealed record FileData(string Label, string[] Values)
    {
        public bool IsMultiple => Values.Length > 1;
    }
    public static class FileDataBuilder
    {
        public static IReadOnlyList<FileData> Build(TagLib.File file, string filepath)
        {
            var rows = new List<FileData>();
            AddFileInfoRows(rows, filepath);
            if (file.Properties is { } props)
            {
                AddRow(rows, "Duration", FormatHelpers.FormatDuration(props.Duration));
                AddRow(rows, "Codec", props.Codecs.Select(c => c.Description).ToArray());
                AddRow(rows, "Bitrate", props.AudioBitrate > 0 ? FormatHelpers.FormatBitrate(props.AudioBitrate) : null);
                AddRow(rows, "Sample Rate", props.AudioSampleRate > 0 ? FormatHelpers.FormatSampleRate(props.AudioSampleRate) : null);
                AddRow(rows, "Bit Depth", props.BitsPerSample > 0 ? FormatHelpers.FormatBitDepth(props.BitsPerSample) : null);
                AddRow(rows, "Channels", props.AudioChannels > 0 ? FormatHelpers.FormatChannels(props.AudioChannels) : null);
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
        private static void AddFileInfoRows(List<FileData> rows, string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists)
                    return;
                AddRow(rows, "File Path", info.FullName);
                AddRow(rows, "File Size", FormatHelpers.FormatFileSize(info.Length));
                AddRow(rows, "Last Modified", FormatHelpers.FormatDateTime(info.LastWriteTime));
            }
            catch
            {

            }
        }

    }
}
