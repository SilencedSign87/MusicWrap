using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.ViewModels.Library
{
    public partial class QueueViewModel : ObservableObject
    {

        [ObservableProperty]
        private ObservableCollection<TrackRowItem> queueTrackRows = [];

        [ObservableProperty]
        private ObservableCollection<QueueData> queueDataList = [];

        // Lookups
        private Dictionary<int, Track> _trackById = [];
        private Dictionary<int, string> _albumTitleById = [];
        private Dictionary<int, CoverAsset> _coverById = [];
        private Dictionary<int, string> _artistNameById = [];
        private Dictionary<int, BitmapImage?> _albumArtByCoverId = [];

        private const int AlbumArtCacheLimit = 256;
        private readonly LinkedList<int> _albumArtLru = [];
        private readonly Dictionary<int, LinkedListNode<int>> _albumArtNodeByCoverId = [];

        // Services
        private IMusicPlayerService _player = null!;
        private MusicLibrary _library = null!;

        public QueueViewModel(IMusicPlayerService player, MusicLibrary library)
        {
            _library = library;
            _player = player;

            QueueDataList = [];
            RefreshLookups();

            var currentQueue = _player.GetQueue();
            LoadQueueData(currentQueue);

            _player.QueueChanged += _player_QueueChanged;
        }

        private void _player_QueueChanged(object? sender, int[] e)
        {
            RefreshLookups();
            LoadQueueData(e);
        }

        private void LoadQueueData(int[] currentQueue)
        {
            var validQueue = currentQueue.Where(id => _trackById.ContainsKey(id)).ToArray();
            QueueTrackRows.Clear();

            while (QueueDataList.Count > validQueue.Length)
                QueueDataList.RemoveAt(QueueDataList.Count - 1);

            for (int i = 0; i < validQueue.Length; i++)
            {
                var trackId = validQueue[i];
                var track = _trackById[trackId];
                var coverPath = track.CoverId != 0 && _coverById.TryGetValue(track.CoverId, out var cover)
                    ? Path.Combine(ImageHelper.BaseCoverPath, cover.FileName)
                    : null;

                QueueTrackRows.Add(new TrackRowItem
                {
                    Id = trackId,
                    ListIndex = i + 1,
                    DiskNumber = track.Disk,
                    TrackNumber = track.TrackNumber,
                    Title = track.Title,
                    ArtistNames = BuildArtistString(track.ArtistIds),
                    AlbumName = _albumTitleById.TryGetValue(track.AlbumId, out var albumTitle) ? albumTitle : "Unknown Album",
                    DurationText = TimeSpan.FromSeconds(track.Duration).ToString(@"m\:ss"),
                    CoverAssetPath = coverPath
                });

                // Build legacy QueueData for compatibility
                QueueData row;

                if (i < QueueDataList.Count)
                {
                    row = QueueDataList[i];
                }
                else
                {
                    row = new QueueData();
                    QueueDataList.Add(row);
                }

                // recalculate metadata
                if (row.TrackId != trackId)
                {
                    row.TrackId = trackId;
                    row.Title = track.Title;
                    row.ArtistString = BuildArtistString(track.ArtistIds);
                    row.DurationString = TimeSpan.FromSeconds(track.Duration).ToString(@"m\:ss");
                    row.AlbumArt = GetAlbumArt(track.CoverId);
                }
                row.IsPlaying = trackId == _player.CurrentTrackId;
            }
        }
        [RelayCommand]
        public void RemoveSelectedTracksFromQueue(List<int> trackIDs)
        {
            var removeSet = new HashSet<int>(trackIDs);
            var currentQueue = _player.GetQueue();
            var filtered = currentQueue.Where(id => !removeSet.Contains(id)).ToList();
            if (removeSet.Contains(_player.CurrentTrackId))
            {
                _player.SetSilentIndex(-1); // prevent auto-advance to next track
                _player.Stop();
            }
            _player.SetQueue(filtered, true);
        }

        [RelayCommand]
        private void ReorderTrack(TrackReorderRequest request)
        {
            var currentQueue = _player.GetQueue();
            if (currentQueue.Length == 0)
            {
                return;
            }

            var sourceIndex = Array.IndexOf(currentQueue, request.SourceTrackId);
            var targetIndex = Array.IndexOf(currentQueue, request.TargetTrackId);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            {
                return;
            }

            var queue = currentQueue.ToList();
            var movedTrackId = queue[sourceIndex];
            queue.RemoveAt(sourceIndex);

            if (sourceIndex < targetIndex)
            {
                targetIndex--;
            }

            var insertIndex = request.PlaceAfterTarget ? targetIndex + 1 : targetIndex;
            insertIndex = Math.Clamp(insertIndex, 0, queue.Count);
            queue.Insert(insertIndex, movedTrackId);

            _player.SetQueue(queue, true);
        }

        [RelayCommand]
        private void ActivateTrack(int trackID)
        {
            _player.PlayTrack(trackID);
        }

        public void PlayTrack(int trackID)
        {
            PlayTracks(new List<int> { trackID });
        }
        public void PlayTracks(List<int> trackIds)
        {
            if (trackIds is null || trackIds.Count == 0) return;

            var queue = _player.GetQueue();
            if (queue.Length == 0) return;

            var selectedCounts = new Dictionary<int, int>();
            foreach (var id in trackIds)
            {
                if (selectedCounts.TryGetValue(id, out var count))
                    selectedCounts[id] = count + 1;
                else
                    selectedCounts[id] = 1;
            }
            var selectedInQueueOrder = new List<int>(trackIds.Count);
            int firstSelectedIndex = -1;
            for (int i = 0; i < queue.Length; i++)
            {
                var id = queue[i];
                if (!selectedCounts.TryGetValue(id, out var count) || count == 0)
                    continue;

                if (firstSelectedIndex < 0)
                    firstSelectedIndex = i;

                selectedInQueueOrder.Add(id);
                if (count == 1) selectedCounts.Remove(id);
                else selectedCounts[id] = count - 1;
            }

            if (selectedInQueueOrder.Count == 0) return;

            int anchorTrackId = firstSelectedIndex > 0 ? queue[firstSelectedIndex - 1] : int.MinValue;

            var removeCounts = new Dictionary<int, int>();
            foreach (var id in selectedInQueueOrder)
            {
                if (removeCounts.TryGetValue(id, out var count))
                    removeCounts[id] = count + 1;
                else
                    removeCounts[id] = 1;
            }
            var filtered = new List<int>(queue.Length - selectedInQueueOrder.Count);
            foreach (var id in queue)
            {
                if (removeCounts.TryGetValue(id, out var count) && count > 0)
                {
                    if (count == 1) removeCounts.Remove(id);
                    else removeCounts[id] = count - 1;
                    continue;
                }

                filtered.Add(id);

            }

            int insertionIndex = 0;
            if (firstSelectedIndex > 0)
            {
                int anchorIndex = filtered.IndexOf(anchorTrackId);
                insertionIndex = anchorIndex >= 0 ? anchorIndex + 1 : 0;
            }

            filtered.InsertRange(insertionIndex, selectedInQueueOrder);
            _player.SetQueue(filtered, false);
            _player.PlayIndex(insertionIndex);
        }

        public void SetSelectedTracksToPlayNext(List<int> trackIDs)
        {
            var currentTrackId = _player.CurrentTrackId;
            var removeSet = new HashSet<int>(trackIDs);
            var currentQueue = _player.GetQueue().Where(id => !removeSet.Contains(id)).ToList();

            int currentIndex = currentQueue.IndexOf(currentTrackId);
            if (currentIndex == -1) return;

            currentQueue.InsertRange(currentIndex + 1, trackIDs);
            _player.SetQueue(currentQueue, true);
        }

        private string BuildArtistString(int[] artistIds)
        {
            var names = artistIds.Select(id => _artistNameById.TryGetValue(id, out var name) ? name : "Unknown Artist");
            return string.Join(", ", names);
        }
        private BitmapImage? GetAlbumArt(int coverId)
        {
            if (coverId == 0) return ImageHelper.GetDefaultAlbumImage(42);

            if (_albumArtByCoverId.TryGetValue(coverId, out var cachedImage))
            {
                TouchAlbumArt(coverId);
                return cachedImage;
            }

            string? path = null;
            if (_coverById.TryGetValue(coverId, out var cover))
            {
                path = Path.Combine(ImageHelper.BaseCoverPath, cover.FileName);
            }

            var img = ImageHelper.LoadThumbnail(path, "album", 42);
            _albumArtByCoverId[coverId] = img;
            TouchAlbumArt(coverId);
            TrimAlbumArtCache();

            return img;
        }
        private void TouchAlbumArt(int coverId)
        {
            if (_albumArtNodeByCoverId.TryGetValue(coverId, out var node))
            {
                _albumArtLru.Remove(node);
                _albumArtLru.AddLast(node);
                return;
            }

            var newNode = new LinkedListNode<int>(coverId);
            _albumArtLru.AddLast(newNode);
            _albumArtNodeByCoverId[coverId] = newNode;
        }

        private void TrimAlbumArtCache()
        {
            while (_albumArtByCoverId.Count > AlbumArtCacheLimit)
            {
                var oldest = _albumArtLru.First;
                if (oldest == null)
                    break;

                int coverId = oldest.Value;
                _albumArtLru.RemoveFirst();
                _albumArtNodeByCoverId.Remove(coverId);
                _albumArtByCoverId.Remove(coverId);
            }
        }

        private void RefreshLookups()
        {
            var tracks = _library.Tracks.ToArray();
            var albums = _library.Albums.ToArray();
            var covers = _library.CoverAssets.ToArray();
            var artists = _library.Artists.ToArray();

            _trackById = tracks.ToDictionary(t => t.Id);
            _albumTitleById = albums.ToDictionary(a => a.Id, a => a.Title);
            _coverById = covers.ToDictionary(c => c.Id);
            _artistNameById = artists.ToDictionary(a => a.Id, a => a.Name);
        }

    }

    public partial class QueueData : ObservableObject
    {
        [ObservableProperty] private int trackId;
        [ObservableProperty] private BitmapImage? albumArt;
        [ObservableProperty] private string title = string.Empty;
        [ObservableProperty] private string artistString = string.Empty;
        [ObservableProperty] private string durationString = "0:00";
        [ObservableProperty] private bool isPlaying = false;
    }
}
