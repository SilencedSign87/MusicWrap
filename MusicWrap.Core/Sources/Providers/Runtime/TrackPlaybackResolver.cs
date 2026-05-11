using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.Core.Sources.Providers.Runtime;

public sealed class TrackPlaybackResolver : ITrackPlaybackResolver
{
    private readonly IEnumerable<ITrackSourceProvider> _providers;

    public TrackPlaybackResolver(IEnumerable<ITrackSourceProvider> providers)
    {
        _providers = providers;
    }

    public bool TryResolve(Track track, out ResolvedPlaybackSource source)
    {
        foreach (var provider in _providers)
        {
            if (provider.CanHandle(track) && provider.TryResolve(track, out source))
            {
                return true;
            }
        }

        source = default!;
        return false;
    }
}
