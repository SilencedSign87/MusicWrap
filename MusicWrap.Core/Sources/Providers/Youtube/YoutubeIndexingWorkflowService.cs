using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.Providers.Youtube;
using System.Diagnostics;

namespace MusicWrap.Core.Sources.Providers.Youtube;

public sealed class YoutubeIndexingWorkflowService : IYoutubeIndexingWorkflowService
{
    private readonly IYoutubeStagingService _youtubeStagingService;
    private readonly IYoutubeLibraryIndexingService _youtubeLibraryIndexingService;

    public YoutubeIndexingWorkflowService(
        IYoutubeStagingService youtubeStagingService,
        IYoutubeLibraryIndexingService youtubeLibraryIndexingService)
    {
        _youtubeStagingService = youtubeStagingService;
        _youtubeLibraryIndexingService = youtubeLibraryIndexingService;
    }

    public async Task<YoutubeBatchIndexResult> IndexTracksAsync(
        IReadOnlyList<YoutubeIndexingRequest> tracks,
        Action<int, int>? onProgress = null,
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
                var localSourcePath = await _youtubeStagingService
                    .GetPlayableFileAsync(track.ExternalId, cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(localSourcePath))
                {
                    var result = await _youtubeLibraryIndexingService
                        .IndexResolvedTrackAsync(track, localSourcePath, cancellationToken)
                        .ConfigureAwait(false);
                    success = result.Success;
                }
            }
            catch (YoutubeStagingException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YT] Error indexing {track.Title} :{ex.Message}");
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
