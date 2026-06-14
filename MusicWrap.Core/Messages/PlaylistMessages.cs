using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Messages
{
    public sealed class PlaylistListChangedMessage { }

    public sealed class PlaylistContentChangedMessage
    {
        public int PlaylistId { get; }
        public IReadOnlySet<int> AffectedTrackIds { get; }
        public PlaylistContentChangedMessage(int playlistId, IEnumerable<int>? affectedTrackIds = null)
        {
            PlaylistId = playlistId;
            AffectedTrackIds = affectedTrackIds?.ToHashSet() ?? [];
        }
    }
}
