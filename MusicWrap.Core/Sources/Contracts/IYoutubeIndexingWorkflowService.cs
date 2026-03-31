using MusicWrap.Data.Providers.Youtube;

namespace MusicWrap.Core.Sources.Contracts;

public sealed class YoutubeBatchIndexResult
{
    public int Saved { get; init; }
    public int Failed { get; init; }
}

public interface IYoutubeIndexingWorkflowService
{
    Task<YoutubeBatchIndexResult> IndexTracksAsync(
        IReadOnlyList<YoutubeIndexingRequest> tracks,
        Action<int, int>? onProgress = null,
        CancellationToken cancellationToken = default);
}
