namespace MusicWrap.Core.Sources.Contracts;

/// <summary>
/// Represents progress during YouTube track indexing with detailed state information.
/// Each track has 2 logical steps: downloading and indexing.
/// </summary>
public sealed class YoutubeIndexingProgress
{
    /// <summary>
    /// Current track title being processed.
    /// </summary>
    public required string TrackTitle { get; init; }

    /// <summary>
    /// Current phase: "downloading" or "indexing"
    /// </summary>
    public required string Phase { get; init; }

    /// <summary>
    /// Current track number (1-based index).
    /// </summary>
    public required int CurrentTrackIndex { get; init; }

    /// <summary>
    /// Total number of tracks to process.
    /// </summary>
    public required int TotalTracks { get; init; }

    /// <summary>
    /// Overall progress as percentage (0-100).
    /// Calculated as: (CurrentTrackIndex * 2 + StepOffset) / (TotalTracks * 2) * 100
    /// where StepOffset = 0 for downloading, 1 for indexing
    /// </summary>
    public int ProgressPercentage => CalculatePercentage();

    /// <summary>
    /// Human-readable status message: "downloading {TrackTitle}..." or "indexing {TrackTitle}..."
    /// </summary>
    public string StatusMessage => $"{Phase} {TrackTitle}...";

    private int CalculatePercentage()
    {
        if (TotalTracks == 0)
            return 0;

        int stepOffset = Phase.Equals("indexing", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        int totalSteps = TotalTracks * 2;
        int currentStep = (CurrentTrackIndex - 1) * 2 + stepOffset + 1;

        return Math.Min(100, (currentStep * 100) / totalSteps);
    }
}
