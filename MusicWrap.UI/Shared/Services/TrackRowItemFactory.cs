using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Controls.Models;

namespace MusicWrap.UI.Shared.Services
{
    public interface ITrackRowItemFactory
    {
        List<TrackRowItem> Build(IEnumerable<int> trackIds);
    }

    public sealed class TrackRowItemFactory : ITrackRowItemFactory
    {
        private readonly ILibraryService _libraryService;

        public TrackRowItemFactory(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        public List<TrackRowItem> Build(IEnumerable<int> trackIds)
        {
            var trackRowItems = new List<TrackRowItem>();
            var index = 1;

            foreach (var trackId in trackIds)
            {
                var track = _libraryService.GetTrackById(trackId);
                if (track is null)
                {
                    continue;
                }

                var artists = track.AlbumArtists.Length > 0 ? track.AlbumArtists : track.Artists;
                var coverAssetPath = _libraryService.FindCover(trackIds: [track.Id]);

                trackRowItems.Add(new TrackRowItem
                {
                    Id = track.Id,
                    Title = track.Title ?? string.Empty,
                    ArtistNames = artists.Length > 0 ? string.Join(", ", artists) : "Unknown Artist",
                    AlbumName = track.AlbumName ?? "Unknown Album",
                    DiskNumber = track.DiskNumber,
                    CoverAssetPath = coverAssetPath,
                    DurationText = TimeSpan.FromSeconds(Math.Max(0, track.DurationSeconds)).ToString(@"m\:ss"),
                    TrackNumber = track.TrackNumber,
                    ListIndex = index
                });

                index++;
            }

            return trackRowItems;
        }
    }
}
