using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicWrap.UI.Features.Metadata.Services
{
    public sealed partial class MetadataEditorWorkspace : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSingleTrack))]
        private IReadOnlyList<int> trackIds = [];
        public bool IsSingleTrack => TrackIds.Count == 1;
    }
}
