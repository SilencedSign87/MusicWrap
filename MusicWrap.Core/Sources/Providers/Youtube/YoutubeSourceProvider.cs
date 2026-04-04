using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MusicWrap.Core.Sources.Providers.Youtube
{
    public sealed class YoutubeSourceProvider : ITrackSourceProvider
    {
        private readonly IYoutubeStagingService _staging;


        public YoutubeSourceProvider(IYoutubeStagingService stagingService)
        {
            _staging = stagingService;
        }

        public bool CanHandle(Track track) => track.Origin == TrackOrigin.Youtube;

        public bool TryResolve(Track track, out ResolvedPlaybackSource source)
        {
            if (!string.IsNullOrWhiteSpace(track.Path) && File.Exists(track.Path))
            {
                source = new ResolvedPlaybackSource
                {
                    Kind = PlaybackSourceKind.LocalFile,
                    Input = track.Path,
                    Display = track.Title
                };
                return true;
            }

            if (string.IsNullOrWhiteSpace(track.ExternalId))
            {
                source = default!;
                return false;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                string? stagedPath = Task.Run(async () => await _staging.GetPlayableFileAsync(track.ExternalId!, cts.Token).ConfigureAwait(false), cts.Token).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(stagedPath))
                {
                    source = default!;
                    return false;
                }

                source = new ResolvedPlaybackSource
                {
                    Kind = PlaybackSourceKind.LocalFile,
                    Input = stagedPath,
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
