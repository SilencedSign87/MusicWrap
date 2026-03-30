using MusicWrap.Core.Sources.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace MusicWrap.Core.Sources.Providers.Youtube;

public sealed class YoutubeResolutionService : IYoutubeResolutionService
{
    private readonly YoutubeClient _youtube = new();
    private readonly Dictionary<string, (string AudioUrl, DateTime ExpiresAt)> _urlCache = [];
    private readonly object _cacheLock = new();
    public async Task<string?> TryResolveAudioUrlAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return null;

        lock (_cacheLock)
        {
            if (_urlCache.TryGetValue(videoId, out var cacheEntry))
            {
                if (DateTime.UtcNow < cacheEntry.ExpiresAt)
                    return cacheEntry.AudioUrl;

                _urlCache.Remove(videoId);
            }
        }

        try
        {
            var streamManifest = await _youtube.Videos.Streams
                                        .GetManifestAsync(videoId, cancellationToken)
                                        .ConfigureAwait(false);

            // var audioStream = streamManifest
            //     .GetAudioOnlyStreams()
            //     .OrderByDescending(s => s.Bitrate)
            //     .FirstOrDefault();
            var audioStream = streamManifest
            .GetAudioOnlyStreams()
            .Where(s => string.Equals(s.Container.Name, "mp4", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault()
            ?? streamManifest.GetAudioOnlyStreams()
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();


            if (audioStream is null)
            {
                Debug.WriteLine($"[YT] No audio stream found for video {videoId}");
                return null;
            }

            var audioUrl = audioStream.Url;

            lock (_cacheLock)
            {
                _urlCache[videoId] = (audioUrl, DateTime.UtcNow.AddHours(1));
            }

            return audioUrl;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[YT] Audio URL resolution for video {videoId} was canceled.");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[YT] Error resolving audio URL for video {videoId}: {ex}");
            return null;
        }
    }
    public async Task<YoutubeVideoMetadata?> TryFetchMetadataAsync(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId)) return null;

        try
        {
            var video = await _youtube.Videos.GetAsync(videoId);
            return new YoutubeVideoMetadata
            {
                Title = video.Title,
                DurationSeconds = (int)(video.Duration?.TotalSeconds ?? 0),
                ThumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url,
                ChannelName = video.Author.ChannelTitle,
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[YT] Error fetching metadata for video {videoId}: {ex}");
            return null;
        }
    }

}

