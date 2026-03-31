namespace MusicWrap.UI.Models;

/// <summary>
/// Represents a deduplicated artwork asset for staged tracks.
/// </summary>
public sealed class StagedArtworkNode
{
    public required string Id { get; init; }
    public required string SourceUrl { get; init; }
}
