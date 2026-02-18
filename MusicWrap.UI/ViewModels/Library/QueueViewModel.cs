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
        private QueueData? nowPlaying;

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
            var currentQueueTrackIds = currentQueue;
            var currentTrackId = _player.CurrentTrackId;

            var currentTrackIndex = currentQueueTrackIds.IndexOf(currentTrackId);

            //var previousTracks = currentQueueTrackIds.Take(currentTrackIndex).ToList();
            List<int> nextTracks = currentTrackIndex >= 0 ? [.. currentQueueTrackIds.Skip(currentTrackIndex + 1)] : [];

            QueueDataList = TrackIdsToQueueData(nextTracks);
            if (currentTrackIndex >= 0)
            {
                NowPlaying = TrackIdsToQueueData(new List<int> { currentTrackId }).FirstOrDefault();
            }
            else
            {
                NowPlaying = null;
            }

        }

        private List<QueueData> TrackIdsToQueueData(List<int> trackIds)
        {
            var queueDataList = new List<QueueData>();
            for (int i = 0; i < trackIds.Count; i++)
            {
                var track = _library.Tracks.FirstOrDefault(t => t.Id == trackIds[i]);
                if (track is not null)
                {
                    var coverArt = _library.CoverAssets.FirstOrDefault(c => c.Id == track.CoverId);

                    queueDataList.Add(new QueueData
                    {
                        TrackId = track.Id,
                        Title = track.Title,
                        ArtistString = string.Join(", ", track.ArtistIds.Select(aid => _library.Artists.FirstOrDefault(a => a.Id == aid)?.Name ?? "Unknown Artist")),
                        DurationString = TimeSpan.FromSeconds(track.Duration).ToString(@"m\:ss"),
                        AlbumArt = ImageHelper.LoadThumbnail(coverArt is not null ? Path.Combine(ImageHelper.BaseCoverPath ,coverArt.FileName) : null, "album", 64)
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
    }
}
