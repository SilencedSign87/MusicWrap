using System.Collections.ObjectModel;

namespace MusicWrap.Core.Queue
{
    public enum RepeatMode { None, RepeatAll, RepeatOne }
    public interface IQueueManager
    {
        ReadOnlyObservableCollection<PlaybackQueueItem> Items { get; }
        int CurrentIndex { get; }
        int CurrentPlaybackIndex { get; }
        PlaybackQueueItem? CurrentItem { get; }
        bool IsShuffleEnabled { get; }
        RepeatMode RepeatMode { get; }

        void Jump(int index);
        void JumpPlaybackIndex(int index);
        void AddNext(IEnumerable<PlaybackQueueItem> item);
        void AddLast(IEnumerable<PlaybackQueueItem> item);
        void RemoveAt(int index, int itemCount = 1);
        void Move(IEnumerable<int> fromIndex, int toIndex);
        void Clear();

        void SetShuffle(bool enabled);
        void SetRepeatMode(RepeatMode repeatMode);

        void Set(IEnumerable<PlaybackQueueItem> items, int startAt = 0);

        PlaybackQueueItem? Next();
        PlaybackQueueItem? Previous();
        PlaybackQueueItem? PeekNext();
        int[] GetPlaybackOrderIndices();
        void SetPlaybackOrder(IReadOnlyList<int> playbackOrderIndices);

        event EventHandler? QueueChanged;
        event EventHandler? CurrentChanged;
    }
    public class QueueManager : IQueueManager
    {
        private readonly ObservableCollection<PlaybackQueueItem> _internalItems = new();
        public ReadOnlyObservableCollection<PlaybackQueueItem> Items { get; }
        private int _currentIndex = -1;
        public int CurrentIndex => _currentIndex;
        public int CurrentPlaybackIndex => _currentIndex < 0
            ? -1
            : (_isShuffleEnabled ? _shuffleCursor : _currentIndex);
        public PlaybackQueueItem? CurrentItem => (_currentIndex >= 0 && _currentIndex < _internalItems.Count)
            ? _internalItems[_currentIndex] : null;
        private bool _isShuffleEnabled;
        public bool IsShuffleEnabled => _isShuffleEnabled;
        private RepeatMode _repeatMode = RepeatMode.None;
        public RepeatMode RepeatMode => _repeatMode;
        private List<int> _shuffleOrder = [];
        private int _shuffleCursor = 0;
        public event EventHandler? QueueChanged;
        public event EventHandler? CurrentChanged;

        public QueueManager()
        {
            Items = new ReadOnlyObservableCollection<PlaybackQueueItem>(_internalItems);
        }
        public void Jump(int index)
        {
            if (index < 0 || index >= _internalItems.Count)
                return;

            _currentIndex = index;
            _shuffleCursor = _isShuffleEnabled ? _shuffleOrder.IndexOf(index) : index;
            OnCurrentChanged();
        }

        public void JumpPlaybackIndex(int index)
        {
            int baseIndex = GetBaseIndexFromPlaybackIndex(index);
            if (baseIndex < 0) return;

            _currentIndex = baseIndex;
            _shuffleCursor = _isShuffleEnabled ? Math.Clamp(index, 0, _shuffleOrder.Count - 1) : baseIndex;
            OnCurrentChanged();
        }
        public void Set(IEnumerable<PlaybackQueueItem> items, int startAt = 0)
        {
            _internalItems.Clear();
            foreach (var item in items)
                _internalItems.Add(item);
            if (_internalItems.Count > 0)
                _currentIndex = Math.Clamp(startAt, 0, _internalItems.Count - 1);
            else
                _currentIndex = -1;
            ResetShuffle();
            OnQueueChanged();
            OnCurrentChanged();
        }
        public void AddNext(IEnumerable<PlaybackQueueItem> items)
        {
            var insertAt = (_currentIndex >= 0 && _currentIndex < _internalItems.Count)
                ? _currentIndex + 1
                : _internalItems.Count;
            foreach (var item in items)
                _internalItems.Insert(insertAt++, item);
            ResetShuffle();
            OnQueueChanged();
        }
        public void AddLast(IEnumerable<PlaybackQueueItem> items)
        {
            foreach (var item in items)
                _internalItems.Add(item);
            ResetShuffle();
            OnQueueChanged();
        }

        public void RemoveAt(int index, int itemCount = 1)
        {
            if (index < 0 || index >= _internalItems.Count)
                return;

            if (itemCount <= 0) return;
            int actualCount = Math.Min(itemCount, _internalItems.Count - index);

            for (int i = index + actualCount - 1; i >= index; i--)
                _internalItems.RemoveAt(i);

            // Recalculate currentIndex
            if (_currentIndex >= index && _currentIndex < index + actualCount)
            {
                // was removed
                _currentIndex = Math.Min(index, _internalItems.Count - 1);
            }
            else if (_currentIndex > index)
            {
                _currentIndex -= actualCount;
            }
            if (_internalItems.Count == 0)
                _currentIndex = -1;
            ResetShuffle();
            OnQueueChanged();
            OnCurrentChanged();
        }

        public void Move(IEnumerable<int> fromIndices, int toIndex)
        {
            var ordered = fromIndices.ToList();
            if (ordered.Count == 0) return;

            if (ordered.Any(i => i < 0 || i >= _internalItems.Count))
                return;

            var itemsToMove = ordered.Select(i => _internalItems[i]).ToList();

            foreach (var idx in ordered.Distinct().OrderByDescending(i => i))
                _internalItems.RemoveAt(idx);

            var removedBeforeTarget = ordered.Distinct().Count(i => i < toIndex);
            toIndex -= removedBeforeTarget;
            toIndex = Math.Clamp(toIndex, 0, _internalItems.Count);

            for (int i = 0; i < itemsToMove.Count; i++)
                _internalItems.Insert(toIndex + i, itemsToMove[i]);

            // Recalculate currentIndex
            if (ordered.Contains(_currentIndex))
            {
                var rel = ordered.IndexOf(_currentIndex);
                _currentIndex = toIndex + rel;
                OnCurrentChanged();
            }
            else
            {
                _currentIndex -= ordered.Distinct().Count(i => i < _currentIndex);
                if (_currentIndex < 0) _currentIndex = -1;
            }
            ResetShuffle();
            OnQueueChanged();
        }

        public void Clear()
        {
            _internalItems.Clear();
            _currentIndex = -1;
            ResetShuffle();
            OnQueueChanged();
            OnCurrentChanged();
        }

        public void SetShuffle(bool enabled)
        {
            if (_isShuffleEnabled == enabled) return;
            _isShuffleEnabled = enabled;
            ResetShuffle();
            OnQueueChanged();
        }

        public void SetRepeatMode(RepeatMode repeatMode)
        {
            if (_repeatMode == repeatMode) return;
            _repeatMode = repeatMode;
        }

        public PlaybackQueueItem? Next()
        {
            if (_internalItems.Count == 0) return null;

            if (_repeatMode == RepeatMode.RepeatOne && _currentIndex >= 0)
                return _internalItems[_currentIndex];

            if (_currentIndex < 0)
            {
                if (_isShuffleEnabled)
                {
                    if (_shuffleOrder.Count == 0) ResetShuffle();
                    _shuffleCursor = 0;
                    _currentIndex = _shuffleOrder[_shuffleCursor];
                }
                else
                {
                    _currentIndex = 0;
                }
                OnCurrentChanged();
                return _internalItems[_currentIndex];
            }
            int newIndex;
            if (_isShuffleEnabled)
            {
                if (_shuffleOrder.Count == 0) ResetShuffle();
                _shuffleCursor++;
                if (_shuffleCursor >= _shuffleOrder.Count)
                {
                    if (_repeatMode == RepeatMode.RepeatAll)
                    {
                        ResetShuffle();
                        _shuffleCursor = 0;
                    }
                    else
                    {
                        _shuffleCursor = _shuffleOrder.Count - 1;
                        return null;
                    }
                }
                newIndex = _shuffleOrder[_shuffleCursor];
            }
            else
            {
                newIndex = _currentIndex + 1;
                if (newIndex >= _internalItems.Count)
                {
                    if (_repeatMode == RepeatMode.RepeatAll)
                        newIndex = 0;
                    else
                        return null;
                }
            }
            _currentIndex = newIndex;
            OnCurrentChanged();
            return _internalItems[_currentIndex];
        }

        public PlaybackQueueItem? Previous()
        {
            if (_internalItems.Count == 0) return null;

            if (_repeatMode == RepeatMode.RepeatOne && _currentIndex >= 0)
                return _internalItems[_currentIndex];

            if (_currentIndex < 0)
            {
                if (_isShuffleEnabled)
                {
                    if (_shuffleOrder.Count == 0) ResetShuffle();
                    _shuffleCursor = _shuffleOrder.Count - 1;
                    _currentIndex = _shuffleOrder[_shuffleCursor];
                }
                else
                {
                    _currentIndex = _internalItems.Count - 1;
                }
                OnCurrentChanged();
                return _internalItems[_currentIndex];
            }
            int prevIndex;
            if (_isShuffleEnabled)
            {
                _shuffleCursor--;
                if (_shuffleCursor < 0)
                {
                    if (_repeatMode == RepeatMode.RepeatAll)
                    {
                        _shuffleCursor = _shuffleOrder.Count - 1;
                    }
                    else
                    {
                        _shuffleCursor = 0;
                        return null;
                    }
                }
                prevIndex = _shuffleOrder[_shuffleCursor];
            }
            else
            {
                prevIndex = _currentIndex - 1;
                if (prevIndex < 0)
                {
                    if (_repeatMode == RepeatMode.RepeatAll)
                        prevIndex = _internalItems.Count - 1;
                    else
                        return null;
                }
            }
            _currentIndex = prevIndex;
            OnCurrentChanged();
            return _internalItems[_currentIndex];
        }

        public PlaybackQueueItem? PeekNext()
        {
            if (_internalItems.Count == 0) return null;

            if (_repeatMode == RepeatMode.RepeatOne && _currentIndex >= 0)
                return _internalItems[_currentIndex];

            if (_currentIndex < 0)
            {
                if (_isShuffleEnabled)
                {
                    if (_shuffleOrder.Count == 0) ResetShuffle();
                    return _internalItems[_shuffleOrder[0]];
                }
                return _internalItems[0];
            }

            int nextIndex;

            if (_isShuffleEnabled)
            {
                if (_shuffleOrder.Count == 0) ResetShuffle();
                var nextCursor = _shuffleCursor + 1;
                if (nextCursor >= _shuffleOrder.Count)
                {
                    if (_repeatMode == RepeatMode.RepeatAll)
                    {
                        nextCursor = 0;
                    }
                    else
                    {
                        return null;
                    }
                }
                nextIndex = _shuffleOrder[nextCursor];
            }
            else
            {
                nextIndex = _currentIndex + 1;
                if (nextIndex >= _internalItems.Count)
                {
                    if (_repeatMode == RepeatMode.RepeatAll)
                        nextIndex = 0;
                    else
                        return null;
                }
            }

            return _internalItems[nextIndex];
        }

        public int[] GetPlaybackOrderIndices()
        {
            if (_internalItems.Count == 0) return Array.Empty<int>();
            if (!_isShuffleEnabled) return Enumerable.Range(0, _internalItems.Count).ToArray();
            return _shuffleOrder.Count == 0
                ? Enumerable.Range(0, _internalItems.Count).ToArray()
                : [.. _shuffleOrder];
        }

        public void SetPlaybackOrder(IReadOnlyList<int> playbackOrderIndices)
        {
            if (!_isShuffleEnabled) return;
            if (playbackOrderIndices.Count != _internalItems.Count) return;

            var seen = new bool[_internalItems.Count];
            foreach (var index in playbackOrderIndices)
            {
                if (index < 0 || index >= _internalItems.Count) return;
                if (seen[index]) return;
                seen[index] = true;
            }

            _shuffleOrder = playbackOrderIndices.ToList();
            _shuffleCursor = _currentIndex >= 0
                ? _shuffleOrder.IndexOf(_currentIndex)
                : 0;
            if (_shuffleCursor < 0) _shuffleCursor = 0;

            OnQueueChanged();
        }

        private void ResetShuffle()
        {
            if (!_isShuffleEnabled)
            {
                _shuffleOrder.Clear();
                _shuffleCursor = _currentIndex >= 0 ? _currentIndex : 0;
                return;
            }

            var rnd = new Random();

            _shuffleOrder = Enumerable.Range(0, _internalItems.Count).OrderBy(_ => rnd.Next()).ToList();
            _shuffleCursor = _currentIndex >= 0
                     ? _shuffleOrder.IndexOf(_currentIndex)
                     : 0;
            if (_shuffleCursor < 0) _shuffleCursor = 0;
        }
        private int GetBaseIndexFromPlaybackIndex(int playbackIndex)
        {
            if (playbackIndex < 0 || playbackIndex >= _internalItems.Count)
                return -1;
            if (!_isShuffleEnabled) return playbackIndex;
            if (_shuffleOrder.Count == 0) ResetShuffle();
            if (playbackIndex < 0 || playbackIndex >= _shuffleOrder.Count)
                return -1;
            return _shuffleOrder[playbackIndex];
        }
        private void OnQueueChanged() => QueueChanged?.Invoke(this, EventArgs.Empty);
        private void OnCurrentChanged() => CurrentChanged?.Invoke(this, EventArgs.Empty);
    }
}
