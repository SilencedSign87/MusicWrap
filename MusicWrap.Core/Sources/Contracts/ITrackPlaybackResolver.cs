using System;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.Core.Sources.Contracts;

public interface ITrackPlaybackResolver
{
    bool TryResolve(Track track, out ResolvedPlaybackSource source);
}
