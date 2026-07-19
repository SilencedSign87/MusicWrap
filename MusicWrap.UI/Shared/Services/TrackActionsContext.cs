using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using System.Diagnostics;
using System.IO;

namespace MusicWrap.UI.Services
{
    public sealed class TrackActionService
    {
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly ILibraryService _libraryService;
        private readonly IEditMetadataService _editMetadataService;

        public TrackActionService(IMusicPlayerService musicPlayerService, IEditMetadataService editMetadataService, ILibraryService libraryService)
        {
            _musicPlayerService = musicPlayerService;
            _editMetadataService = editMetadataService;
            _libraryService = libraryService;
        }

        public void PlayNow(IReadOnlyList<int> selectedTrackIds, IReadOnlyList<int>? contextTrackIds = null)
        {
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            var queue = contextTrackIds is { Count: > 0 }
                ? contextTrackIds.ToList()
                : selectedTrackIds.ToList();

            _musicPlayerService.SetQueue(queue);
            _musicPlayerService.PlayTrack(selectedTrackIds[0]);
        }

        public void PlayNext(IReadOnlyList<int> selectedTrackIds, IReadOnlyList<int>? contextTrackIds = null)
        {
            _musicPlayerService.AddToNextInQueue(selectedTrackIds);
        }

        public void AddToQueue(IReadOnlyList<int> selectedTrackIds)
        {
           _musicPlayerService.AddToQueue(selectedTrackIds);
        }

        public void PlayNowInQueue(IReadOnlyList<int> selectedTrackIds)
        {
            if (selectedTrackIds.Count == 0) return;
            var indices = _musicPlayerService.GetPlaybackIndices(selectedTrackIds);
            _musicPlayerService.PlayIndex(indices);

        }

        // Queue-specific behavior: move selected items to play right after current track.
        public void PlayNextInQueue(IReadOnlyList<int> selectedTrackIds)
        {
            if (selectedTrackIds.Count == 0) return;
            var indices = _musicPlayerService.GetPlaybackIndices(selectedTrackIds);
            _musicPlayerService.AddIndicesToNext(indices);
        }
        public void EditMetadata(IReadOnlyList<int> selectedTrackIds)
        {
            if (selectedTrackIds.Count == 0)
            {
                return;
            }
            _editMetadataService.OpenMetadataWindow(selectedTrackIds.ToList());
        }
        public void ShowInFileExplorer(IReadOnlyList<int> selectedTrackIds)
        {
            if (selectedTrackIds.Count == 0) return;

            var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var tracks = _libraryService.GetTrackById(selectedTrackIds);

            foreach (var track in tracks)
            {
                if (!string.IsNullOrEmpty(track.Path) && File.Exists(track.Path))
                {
                    var dir = Path.GetDirectoryName(track.Path);
                    if (dir != null && !folders.ContainsKey(dir))
                        folders[dir] = track.Path;
                }
            }

            foreach (var filePath in folders.Values)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
            }

        }
    }
}


