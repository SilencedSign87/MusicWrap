using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Sources.Contracts
{
    public interface IYoutubeResolutionService
    {
        Task<string?> TryResolveAudioUrlAsync(string videoId, CancellationToken cancellationToken = default);
        Task<YoutubeVideoMetadata?> TryFetchMetadataAsync(string videoId);
    }

    public sealed class YoutubeVideoMetadata
    {
        public required string Title { get; init; }
        public required int DurationSeconds { get; init; }
        public string? ThumbnailUrl { get; init; }
        public string? ChannelName { get; init; }
    }
}
