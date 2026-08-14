using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.Features.Metadata.Viewmodels;
using System.ComponentModel;

namespace MusicWrap.UI.ViewModels
{
    public partial class InformationWindowViewModel : ObservableObject
    {
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
        private string displayTitle = "Edit Metadata";

        public IMetadataEditorTabViewmodel? SelectedTab =>
            _tabs.Count == 0 ? null : _tabs[Math.Clamp(SelectedTabIndex, 0, _tabs.Count - 1)];

        public InformationWindowViewModel(
             MetadataEditorWorkspace workspace,
             MetadataGeneralViewmodel general,
             MetadataArtworkEditorViewmodel artwork,
             MetadataEditorViewmodel fields,
             MetadataLyricsEditorViewmodel lyrics
            )
        {
            _workspace = workspace;
            General = general;
            Artwork = artwork;
            Fields = fields;
            Lyrics = lyrics;
            _tabs = [general, artwork, fields, lyrics];
        }

        public void LoadTracks(IReadOnlyList<int> trackIds)
        {
            _workspace.TrackIds = trackIds;
            foreach (var tab in _tabs) tab.Load();
            DisplayTitle = trackIds.Count == 1
                ? $"Edit Metadata - {Fields.DetailTitle ?? "Track"}"
                : $"Edit Metadata - {trackIds.Count} Tracks";
        }
    }
    public interface IMetadataEditorTabViewmodel : INotifyPropertyChanged
    {
        void Load();
    }
}
