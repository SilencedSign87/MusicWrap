using MusicWrap.Core.Queue;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicWrap.Core.Sources.Providers.Queue
{
    public interface IQueueItemPlaybackResolver
    {
        bool TryResolve (PlaybackQueueItem item, out ResolvedPlaybackSource source);
    }
    public class QueueItemPlaybackResolver : IQueueItemPlaybackResolver
    {
        private readonly MusicLibrary _library;
        private readonly ITrackPlaybackResolver _trackResolver;

        public QueueItemPlaybackResolver(
            MusicLibrary library,
            ITrackPlaybackResolver trackResolver
            )
        {
            _library = library;
            _trackResolver = trackResolver;
            
        }
        public bool TryResolve(PlaybackQueueItem item, out ResolvedPlaybackSource source)
        {
            if (item.LibraryId.HasValue)
            {
                var track = _library.Tracks.FirstOrDefault(t => t.Id == item.LibraryId.Value);
                if (track != null)
                {
                    return _trackResolver.TryResolve(track, out source);
                }
            }
            if (item.SourceType == QueueItemSourceType.LocalFile)
            {
                source = new ResolvedPlaybackSource
                {
                    Kind = PlaybackSourceKind.LocalFile,
                    Input = item.Source,
                    Display = item.DisplayTitle ?? item.Source
                };
                return true;
            }
            if (item.SourceType == QueueItemSourceType.RemoteUrl || item.SourceType == QueueItemSourceType.RadioStream)
            {
                source = new ResolvedPlaybackSource
                {
                    Kind = PlaybackSourceKind.RemoteUrl,
                    Input = item.Source,
                    Display = item.DisplayTitle ?? item.Source
                };
                return true;
            }
            source = default!;
            return false;
        }
    }
}
