using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Library;

namespace MusicWrap.UI.Features.Playback.ViewModels
{
    public partial class QueueViewModel : ObservableObject
    {

        [ObservableProperty]
        private ObservableCollection<TrackRowItem> queueTrackRows = [];

        [ObservableProperty]
        private ObservableCollection<QueueData> queueDataList = [];

        private Dictionary<int, BitmapImage?> _albumArtByCoverId = [];

        private const int AlbumArtCacheLimit = 256;
        private readonly LinkedList<int> _albumArtLru = [];
        private readonly Dictionary<int, LinkedListNode<int>> _albumArtNodeByCoverId = [];

        // Services
        private IMusicPlayerService _player = null!;
        private ILibraryService _libraryCache = null!;
        private readonly IImageService _imageService;

        public QueueViewModel(IMusicPlayerService player, ILibraryService libraryCache, IImageService imageService)
        {
            _libraryCache = libraryCache;
            _player = player;
            _imageService = imageService;

            QueueDataList = [];

            var currentQueue = _player.GetPlaybackOrder();
            LoadQueueData();

            _player.QueueChanged += _player_QueueChanged;
        }

        private void _player_QueueChanged(object? sender, int[] e)
        {
            LoadQueueData();
        }

        private void LoadQueueData()
        {
            var orderedTrackIds = _player.GetPlaybackOrderTracks();

            var validTracks = orderedTrackIds
                .Where(id => _libraryCache.GetTrackById(id) is not null)
                .ToArray();
            var rowItems = _libraryCache.TrackIdsToTrackRowItems(validTracks);
            QueueTrackRows = new ObservableCollection<TrackRowItem>(rowItems);

            int currentPlaybackIndex = _player.CurrentplaybackIndex;

            while (QueueDataList.Count > rowItems.Count)
                QueueDataList.RemoveAt(QueueDataList.Count - 1);

            for (int i = 0; i < rowItems.Count; i++)
            {
                var rowItem = rowItems[i];
                var track = _libraryCache.GetTrackById(rowItem.Id);
                if (track is null) continue;

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

                if (row.TrackId != rowItem.Id)
                {
                    row.TrackId = rowItem.Id;
                    row.Title = rowItem.Title;
                    row.ArtistString = rowItem.ArtistNames;
                    row.DurationString = rowItem.DurationText;
                    row.AlbumArt = GetAlbumArt(track.CoverId, rowItem.CoverAssetPath);
                }

                row.IsPlaying = (i == currentPlaybackIndex);
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
           _player.ReorderTrackById(request.SourceTrackId, request.TargetTrackId, request.PlaceAfterTarget);
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

            var queue = _player.GetPlaybackOrder();
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
            var baseQueue = _player.GetQueue();
            for (int i = 0; i < queue.Length; i++)
            {
                var id = baseQueue[queue[i]];
                if (!selectedCounts.TryGetValue(id, out var count) || count == 0)
                    continue;

                if (firstSelectedIndex < 0)
                    firstSelectedIndex = i;

                selectedInQueueOrder.Add(id);
                if (count == 1) selectedCounts.Remove(id);
                else selectedCounts[id] = count - 1;
            }

            if (selectedInQueueOrder.Count == 0) return;

            int anchorTrackId = firstSelectedIndex > 0 ? baseQueue[queue[firstSelectedIndex - 1]] : int.MinValue;

            var removeCounts = new Dictionary<int, int>();
            foreach (var id in selectedInQueueOrder)
            {
                if (removeCounts.TryGetValue(id, out var count))
                    removeCounts[id] = count + 1;
                else
                    removeCounts[id] = 1;
            }
            var filtered = new List<int>(queue.Length - selectedInQueueOrder.Count);
            foreach (var idx in queue)
            {
                var id = baseQueue[idx];
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

        private BitmapImage? GetAlbumArt(int coverId, string? coverPath)
        {
            if (coverId == 0) return _imageService.GetDefaultImage(42);

            if (_albumArtByCoverId.TryGetValue(coverId, out var cachedImage))
            {
                TouchAlbumArt(coverId);
                return cachedImage;
            }

            var img = _imageService.LoadForSize(coverPath, 42);
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



