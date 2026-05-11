namespace MusicWrap.Core.Queue
{
    public enum QueueItemSourceType
    {
        LocalFile,
        RemoteUrl,
        Youtube,
        RadioStream,
    }
    public class PlaybackQueueItem
    {
        public QueueItemSourceType SourceType { get; init; }
        public string Source { get; init; } = string.Empty; // Local path
        public string? ExternalId { get; init; }           // YouTube ID
        public string? DisplayTitle { get; set; }
        public Dictionary<string, string>? ExtraMetadata { get; set; }
        public int? LibraryId { get; init; }               // indexed
        public override string ToString() => DisplayTitle ?? Source;
    }
}
