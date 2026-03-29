using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MusicWrap.Core.Sources.Providers.Youtube
{
    public sealed class YoutubeSourceProvider : ITrackSourceProvider
    {
        private readonly IYoutubeResolutionService _resolutionService;

        public YoutubeSourceProvider(IYoutubeResolutionService resolutionService)
        {
            _resolutionService = resolutionService;
        }
        public bool CanHandle(Track track)
        {
            return track.Origin == TrackOrigin.Youtube;
        }

        public bool TryResolve(Track track, out ResolvedPlaybackSource source)
        {
            if (string.IsNullOrWhiteSpace(track.ExternalId))
            {
                source = default!;
                return false;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                string? audioUrl;
                try
                {
                    audioUrl = Task.Run(async () => await _resolutionService.TryResolveAudioUrlAsync(track.ExternalId!, cts.Token).ConfigureAwait(false), cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine($"[YT] YouTube resolve timeout for track {track.Id}");
                    source = default!;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    Debug.WriteLine($"[YT] Failed to resolve audio URL for track {track.Id} with ExternalId {track.ExternalId}");
                    source = default!;
                    return false;
                }

                // var resolveTask = _resolutionService.TryResolveAudioUrlAsync(track.ExternalId);
                // bool completed = resolveTask.Wait(TimeSpan.FromSeconds(50));

                // if (!completed)
                // {
                //     Debug.WriteLine($"[YT] YouTube resolve timeout for track {track.Id}");
                //     source = default!;
                //     return false;
                // }

                // var audioUrl = resolveTask.Result;
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    Debug.WriteLine($"[YT] Failed to resolve audio URL for track {track.Id} with ExternalId {track.ExternalId}");
                    source = default!;
                    return false;
                }
                source = new ResolvedPlaybackSource
                {
                    Kind = PlaybackSourceKind.RemoteUrl,
                    Input = audioUrl,
                    Display = track.Title
                };
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YT] Error resolving Youtube track {track.Id} : {ex.Message}");
                source = default!;
                return false;
            }
        }
    }
}
