using MusicWrap.Core.Services.Providers.Youtube;

namespace MusicWrap.Core.Sources.Contracts;

public sealed class YoutubeBatchIndexResult
{
    public int Saved { get; init; }
    public int Failed { get; init; }
}

public interface IYoutubeIndexingWorkflowService
{
    /// <summary>
    /// Indexes a batch of YouTube tracks with optional progress reporting.
    /// </summary>
    /// <param name="tracks">Tracks to index with full metadata.</param>
    /// <param name="onProgress">Legacy callback: (processedCount, totalCount). Deprecated, use onDetailedProgress instead.</param>
    /// <param name="onDetailedProgress">Detailed progress callback with track and phase information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<YoutubeBatchIndexResult> IndexTracksAsync(
        IReadOnlyList<YoutubeIndexingRequest> tracks,
        Action<int, int>? onProgress = null,
        Action<YoutubeIndexingProgress>? onDetailedProgress = null,
        CancellationToken cancellationToken = default);
}
