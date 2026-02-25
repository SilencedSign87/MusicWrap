using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core;
using MusicWrap.Data;
using MusicWrap.UI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.ViewModels.Library
{
    public partial class QueueViewModel : ObservableObject
    {

        [ObservableProperty]
        private List<QueueData> queueDataList;

        private IMusicPlayerService _player = null!;
        private MusicLibrary _library = null!;
        public QueueViewModel(IMusicPlayerService player, MusicLibrary library)
        {
            _library = library;
            _player = player;

            QueueDataList = [];

            var currentQueue = _player.GetQueue();
            LoadQueueData(currentQueue);

            _player.QueueChanged += _player_QueueChanged;
        }

        private void _player_QueueChanged(object? sender, int[] e)
        {
            LoadQueueData(e);
        }

        private void LoadQueueData(int[] currentQueue)
        {
            QueueDataList = TrackIdsToQueueData(currentQueue);
        }
        public void RemoveSelectedTracksFromQueue(List<int> trackIDs)
        {
            var currentTrackId = _player.CurrentTrackId;
            if (trackIDs.Contains(currentTrackId))
            {
                _player.Stop();
            }
            var currentQueue = _player.GetQueue().ToList();
            currentQueue = [.. currentQueue.Where(id => !trackIDs.Contains(id))];
            _player.SetQueue(currentQueue, true);
        }
        public void PlayTrack(int trackID)
        {
            var newIndex = _player.GetQueue().ToList().IndexOf(trackID);
            if (newIndex >= 0)
            {
                _player.PlayIndex(newIndex);
            }
        }
        public void SetSelectedTracksToPlayNext(List<int> trackIDs)
        {
            var currentTrackId = _player.CurrentTrackId;
            var currentQueue = _player.GetQueue().ToList();

            // remove tracks to play from current queue
            currentQueue = [.. currentQueue.Where(id => !trackIDs.Contains(id))];

            List<int> newQueue = [];

            // Insert tracks to play next after the current track
            int currentIndex = currentQueue.IndexOf(currentTrackId);
            if (currentIndex == -1) return;
            currentQueue.InsertRange(currentIndex + 1, trackIDs);

            // set queue to player
            _player.SetQueue(currentQueue, true);
        }

        private List<QueueData> TrackIdsToQueueData(IEnumerable<int> trackIds)
        {
            var queueDataList = new List<QueueData>();
            var currentTrackId = _player.CurrentTrackId;
            for (int i = 0; i < trackIds.Count(); i++)
            {
                var track = _library.Tracks.FirstOrDefault(t => t.Id == trackIds.ElementAt(i));
                if (track is not null)
                {
                    var coverArt = _library.CoverAssets.FirstOrDefault(c => c.Id == track.CoverId);

                    queueDataList.Add(new QueueData
                    {
                        TrackId = track.Id,
                        Title = track.Title,
                        ArtistString = string.Join(", ", track.ArtistIds.Select(aid => _library.Artists.FirstOrDefault(a => a.Id == aid)?.Name ?? "Unknown Artist")),
                        DurationString = TimeSpan.FromSeconds(track.Duration).ToString(@"m\:ss"),
                        AlbumArt = ImageHelper.LoadThumbnail(coverArt is not null ? Path.Combine(ImageHelper.BaseCoverPath, coverArt.FileName) : null, "album", 64),
                        IsPlaying = track.Id == currentTrackId
                    });
                }
            }
            return queueDataList;
        }

    }

    public class QueueData
    {
        public int TrackId { get; set; }
        public BitmapImage? AlbumArt { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistString { get; set; } = string.Empty;
        public string DurationString { get; set; } = "0:00";
        public bool IsPlaying { get; set; } = false;
    }
}
