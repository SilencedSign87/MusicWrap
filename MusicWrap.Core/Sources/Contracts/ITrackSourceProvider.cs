using System;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.Core.Sources.Contracts;

public interface ITrackSourceProvider
{
    bool CanHandle(Track track);
    bool TryResolve(Track track, out ResolvedPlaybackSource source);
}
