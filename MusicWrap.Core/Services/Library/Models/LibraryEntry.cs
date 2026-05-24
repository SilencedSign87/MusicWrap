using MessagePack;

namespace MusicWrap.Core.Services.Library.Models
{
    [MessagePackObject]
    public sealed class LibraryEntry
    {
        [Key(0)] public int Id { get; set; }
        [Key(1)] public required string Type { get; set; }
        [Key(2)] public string? ImagePath { get; set; }
        [Key(3)] public required string Title { get; set; }
        [Key(4)] public required string Description { get; set; }
        [Key(5)] public required string GroupKey { get; set; }
    }
}
