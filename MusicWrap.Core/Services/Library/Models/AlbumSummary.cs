namespace MusicWrap.Core.Services.Library.Models
{
    public sealed class AlbumSummary
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string ArtistNames { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public string? BluredImagePath { get; set; }
        public string DominantColorHex { get; set; } = "#808080";
        public string ForegroundColorHex { get; set; } = "#FFFFFF";
    }
}
