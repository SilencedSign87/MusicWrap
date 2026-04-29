using MusicWrap.Core.Services.Playback;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicWrap.UI.Services
{
    public sealed class TracksContextMenuService
    {
        private readonly IMusicPlayerService _musicPlayerService;

        public TracksContextMenuService(IMusicPlayerService musicPlayerService)
        {
            _musicPlayerService = musicPlayerService;
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
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            var currentQueue = _musicPlayerService.GetQueue() ?? [];
            var newQueue = new List<int>();
            bool inserted = false;

            foreach (var trackId in currentQueue)
            {
                newQueue.Add(trackId);
                if (!inserted && trackId == _musicPlayerService.CurrentTrackId)
                {
                    newQueue.AddRange(selectedTrackIds);
                    inserted = true;
                }
            }

            if (!inserted)
            {
                newQueue.AddRange(selectedTrackIds);
            }

            _musicPlayerService.SetQueue(newQueue, true);
        }

        public void AddToQueue(IReadOnlyList<int> selectedTrackIds)
        {
            foreach (var trackId in selectedTrackIds)
            {
                _musicPlayerService.AddToQueue(trackId);
            }
        }

        // Queue-specific behavior: keep queue items, move selected ones together and play from that block.
        public void PlayNowInQueue(IReadOnlyList<int> selectedTrackIds)
        {
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            var queue = _musicPlayerService.GetQueue();
            if (queue.Length == 0) return;

            var selectedCounts = new Dictionary<int, int>();
            foreach (var id in selectedTrackIds)
            {
                if (selectedCounts.TryGetValue(id, out var count))
                    selectedCounts[id] = count + 1;
                else
                    selectedCounts[id] = 1;
            }

            var selectedInQueueOrder = new List<int>(selectedTrackIds.Count);
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
            _musicPlayerService.SetQueue(filtered, false);
            _musicPlayerService.PlayIndex(insertionIndex);
        }

        // Queue-specific behavior: move selected items to play right after current track.
        public void PlayNextInQueue(IReadOnlyList<int> selectedTrackIds)
        {
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            var currentTrackId = _musicPlayerService.CurrentTrackId;
            var removeSet = new HashSet<int>(selectedTrackIds);
            var currentQueue = _musicPlayerService.GetQueue().Where(id => !removeSet.Contains(id)).ToList();

            int currentIndex = currentQueue.IndexOf(currentTrackId);
            if (currentIndex == -1)
            {
                return;
            }

            currentQueue.InsertRange(currentIndex + 1, selectedTrackIds);
            _musicPlayerService.SetQueue(currentQueue, true);
        }
    }
}


