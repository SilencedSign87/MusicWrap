using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.Core.Sources.Providers.Local;

public sealed class LocalTrackSourceProvider : ITrackSourceProvider
{
    public bool CanHandle(Track track)
    {
        return track.Origin == TrackOrigin.Local;
    }

    public bool TryResolve(Track track, out ResolvedPlaybackSource source)
    {
        if (string.IsNullOrWhiteSpace(track.FilePath))
        {
            source = default!;
            return false;
        }

        source = new ResolvedPlaybackSource
        {
            Kind = PlaybackSourceKind.LocalFile,
            Input = track.FilePath,
            Display = track.FilePath
        };

        return true;

    }
}
