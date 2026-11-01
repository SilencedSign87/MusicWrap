using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.Features.Metadata.Viewmodels;
using System.ComponentModel;

namespace MusicWrap.UI.ViewModels
{
    public partial class InformationWindowViewModel : ObservableObject
    {
        private readonly ILibraryService _libraryService;
        private readonly MetadataEditorWorkspace _workspace;
        private readonly IReadOnlyList<IMetadataEditorTabViewmodel> _tabs;

        public MetadataEditorWorkspace Workspace => _workspace;

        public MetadataGeneralViewmodel General { get; }
        public MetadataArtworkEditorViewmodel Artwork { get; }
        public MetadataEditorViewmodel Fields { get; }
        public MetadataLyricsEditorViewmodel Lyrics { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedTab))]
        private int selectedTabIndex = 0;

        [ObservableProperty]
        public partial string DisplayTitle { get; set; } = "Edit Metadata";

        [ObservableProperty]
        public partial string Title { get; set; } = "Edit Metadata";
        [ObservableProperty]
        public partial string Subtitle { get; set; } = "Edit Metadata";
        [ObservableProperty]
        public partial string? PreviewArtworkPath { get; set; } = null;

        public IMetadataEditorTabViewmodel? SelectedTab =>
            _tabs.Count == 0 ? null : _tabs[Math.Clamp(SelectedTabIndex, 0, _tabs.Count - 1)];

        public InformationWindowViewModel(
             MetadataEditorWorkspace workspace,
             MetadataGeneralViewmodel general,
             MetadataArtworkEditorViewmodel artwork,
             MetadataEditorViewmodel fields,
             MetadataLyricsEditorViewmodel lyrics,
             ILibraryService libraryService
            )
        {
            _workspace = workspace;
            General = general;
            Artwork = artwork;
            Fields = fields;
            Lyrics = lyrics;
            _tabs = [general, fields, artwork, lyrics];
            _libraryService = libraryService;
        }

        public void LoadTracks(IReadOnlyList<int> trackIds)
        {
            _workspace.TrackIds = trackIds;
            foreach (var tab in _tabs) tab.Load();
            DisplayTitle = trackIds.Count == 1
                ? $"Edit Metadata - {Fields.DetailTitle ?? "Track"}"
                : $"Edit Metadata - {trackIds.Count} Tracks";

            if (trackIds.Count == 1)
            {
                var track = _libraryService.GetTrackById(trackIds[0]);
                Title = track?.Title ?? "Edit Metadata";
                var artist = _libraryService.GetArtistNamesByIds(track?.ArtistIds ?? Array.Empty<int>());
                Subtitle = artist.Length != 0 ? string.Join(", ", artist) : "Unknown Artist";
                var artwork = _libraryService.GetCoverAsset(track?.CoverId ?? 0);
                PreviewArtworkPath = artwork?.FileName;
            }
            else
            {
                Title = "Edit Metadata";
                Subtitle = $"{trackIds.Count} Tracks";
                PreviewArtworkPath = null;
            }
        }
    }
    public interface IMetadataEditorTabViewmodel : INotifyPropertyChanged
    {
        void Load();
    }
}
