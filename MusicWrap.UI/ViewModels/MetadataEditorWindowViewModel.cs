using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.Features.Metadata.Viewmodels;
using System.ComponentModel;

namespace MusicWrap.UI.ViewModels
{
    public partial class MetadataEditorWindowViewModel : ObservableObject
    {
        private readonly MetadataEditorWorkspace _workspace;
        private readonly IReadOnlyList<IMetadataEditorTabViewmodel> _tabs;

        public MetadataEditorWorkspace Workspace => _workspace;

        public MetadataGeneralViewmodel General { get; }
        public MetadataArtworkEditorViewmodel Artwork { get; }
        public MetadataEditorViewmodel Fields { get; }
        public MetadataTagEditorViewmodel Tags { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedTab))]
        [NotifyPropertyChangedFor(nameof(SelectedTabHasChanges))]
        private int selectedTabIndex = 2;

        [ObservableProperty]
        private string displayTitle = "Edit Metadata";

        public IMetadataEditorTabViewmodel? SelectedTab =>
            _tabs.Count == 0 ? null : _tabs[Math.Clamp(SelectedTabIndex, 0, _tabs.Count - 1)];

        public bool SelectedTabHasChanges => SelectedTab?.HasChanges == true;

        public MetadataEditorWindowViewModel(
             MetadataEditorWorkspace workspace,
             MetadataGeneralViewmodel general,
             MetadataArtworkEditorViewmodel artwork,
             MetadataEditorViewmodel fields,
             MetadataTagEditorViewmodel tags
            )
        {
            _workspace = workspace;
            General = general;
            Artwork = artwork;
            Fields = fields;
            Tags = tags;
            _tabs = [general, artwork, fields, tags];

            foreach (var tab in _tabs)
                tab.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IMetadataEditorTabViewmodel.HasChanges))
                        OnPropertyChanged(nameof(SelectedTabHasChanges));
                };
        }

        public void LoadTracks(IReadOnlyList<int> trackIds)
        {
            _workspace.TrackIds = trackIds;
            foreach (var tab in _tabs) tab.Load();
            DisplayTitle = trackIds.Count == 1
                ? $"Edit Metadata - {Fields.DetailTitle ?? "Track"}"
                : $"Edit Metadata - {trackIds.Count} Tracks";
        }
        [RelayCommand] private void Save() => SelectedTab?.SaveCommand.Execute(null);
        [RelayCommand] private void CancelChanges() => SelectedTab?.CancelChangesCommand.Execute(null);
    }
    public interface IMetadataEditorTabViewmodel : INotifyPropertyChanged
    {
        bool HasChanges { get; }
        void Load();
        IAsyncRelayCommand SaveCommand { get; }
        IRelayCommand CancelChangesCommand { get; }
    }
}
