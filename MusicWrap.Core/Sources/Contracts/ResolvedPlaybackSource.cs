namespace MusicWrap.Core.Sources.Contracts;

public sealed class ResolvedPlaybackSource
{
    public required PlaybackSourceKind Kind { get; init; }

    public required string Input { get; init; }
    public string? Display { get; init; }
}
