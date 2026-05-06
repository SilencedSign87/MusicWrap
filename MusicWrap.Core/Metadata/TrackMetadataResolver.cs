using MusicWrap.Core.Queue;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Metadata
{
    [Flags]
    public enum MetadataRequest
    {
        Basic = 1, // Title, Artist, Duration
        Extended = 2, // Album,Year, Artwork
        AudioDetails = 4, // Codec, Bitrate, Sample Rate, Bitdepth, Channels
    }
    public class TrackMetadata
    {
        // Basic
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public TimeSpan? Duration { get; set; }
        // Extended
        public string? Album { get; set; }
        public int? Year { get; set; }
        public Uri? ArtworkUri { get; set; }
        // AudioDetails
        public string? Codec { get; set; }
        public int? BitrateKbps { get; set; }
        public int? SampleRate { get; set; }
        public int? BitDepth { get; set; }
        public int? Channels { get; set; }
    }
    public interface IMetadataProvider
    {
        bool CanHandle(PlaybackQueueItem item);
        Task<TrackMetadata> ResolveAsync(
            PlaybackQueueItem item,
            MetadataRequest request,
            CancellationToken ct);
    }
    public class TrackMetadataResolver : IMetadataProvider
    {
        public bool CanHandle(PlaybackQueueItem item)
        {
            throw new NotImplementedException();
        }

        public Task<TrackMetadata> ResolveAsync(PlaybackQueueItem item, MetadataRequest request, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
