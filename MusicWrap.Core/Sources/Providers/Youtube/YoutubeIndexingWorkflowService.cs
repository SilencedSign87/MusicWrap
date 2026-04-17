using Microsoft.Extensions.Logging;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Core.Services.Providers.Youtube;
using System.Diagnostics;

namespace MusicWrap.Core.Sources.Providers.Youtube;

public sealed class YoutubeIndexingWorkflowService : IYoutubeIndexingWorkflowService
{
    private readonly IYoutubeStagingService _youtubeStagingService;
    private readonly IYoutubeLibraryIndexingService _youtubeLibraryIndexingService;
    private readonly ILogger<YoutubeIndexingWorkflowService> _logger;

    public YoutubeIndexingWorkflowService(
        ILogger<YoutubeIndexingWorkflowService> logger,
        IYoutubeStagingService youtubeStagingService,
        IYoutubeLibraryIndexingService youtubeLibraryIndexingService)
    {
        _logger = logger;
        _youtubeStagingService = youtubeStagingService;
        _youtubeLibraryIndexingService = youtubeLibraryIndexingService;
    }

    public async Task<YoutubeBatchIndexResult> IndexTracksAsync(
        IReadOnlyList<YoutubeIndexingRequest> tracks,
        Action<int, int>? onProgress = null,
        Action<YoutubeIndexingProgress>? onDetailedProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (tracks is null || tracks.Count == 0)
        {
            return new YoutubeBatchIndexResult();
        }

        int saved = 0;
        int failed = 0;

        for (int i = 0; i < tracks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var track = tracks[i];
            bool success = false;

            try
            {
                // Report downloading phase
                onDetailedProgress?.Invoke(new YoutubeIndexingProgress
                {
                    TrackTitle = track.Title,
                    Phase = "downloading",
                    CurrentTrackIndex = i + 1,
                    TotalTracks = tracks.Count
                });

                var localSourcePath = await _youtubeStagingService
                    .GetPlayableFileAsync(track.ExternalId, cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(localSourcePath))
                {
                    // Report indexing phase
                    onDetailedProgress?.Invoke(new YoutubeIndexingProgress
                    {
                        TrackTitle = track.Title,
                        Phase = "indexing",
                        CurrentTrackIndex = i + 1,
                        TotalTracks = tracks.Count
                    });

                    var result = await _youtubeLibraryIndexingService
                        .IndexResolvedTrackAsync(track, localSourcePath, cancellationToken)
                        .ConfigureAwait(false);
                    success = result.Success;

                    if (!result.Success)
                    {
                        _logger.LogWarning("Indexing failed for track '{TrackTitle}' (ID: {ExternalId}): {Error}", track.Title, track.ExternalId, result.Error);
                        _youtubeStagingService.InvalidateCachedFile(track.ExternalId);
                    }
                }
                else
                {
                    _logger.LogWarning("Staging returned no playable file for track '{TrackTitle}' (ID: {ExternalId})", track.Title, track.ExternalId);
                }
            }
            catch (YoutubeStagingException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing track '{TrackTitle}' (ID: {ExternalId})", track.Title, track.ExternalId);
                _youtubeStagingService.InvalidateCachedFile(track.ExternalId);
                success = false;
            }

            if (success)
            {
                saved++;
            }
            else
            {
                failed++;
            }

            // Legacy progress callback
            onProgress?.Invoke(i + 1, tracks.Count);
        }

        _youtubeLibraryIndexingService.Persist();

        return new YoutubeBatchIndexResult
        {
            Saved = saved,
            Failed = failed
        };
    }
}
