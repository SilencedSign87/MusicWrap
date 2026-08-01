using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataArtworkEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        private readonly ILibraryService _library;
        private readonly MetadataEditorWorkspace _workspace;

        [ObservableProperty] private string trackArtworkUrl = string.Empty;
        [ObservableProperty] private string albumArtworkUrl = string.Empty;

        public bool HasChanges => false;

        public MetadataArtworkEditorViewmodel(MetadataEditorWorkspace workspace, ILibraryService library)
        {
            _workspace = workspace;
            _library = library;
        }

        public void Load()
        {
            TrackArtworkUrl = string.Empty;
            AlbumArtworkUrl = string.Empty;

            foreach (var trackId in _workspace.TrackIds)
            {
                var track = _library.GetTrackById(trackId);
                if (track is null || track.Id == 0) continue;

                if (track.CoverId != 0 && _library.GetCoverAsset(track.CoverId) is { } trackCover)
                    TrackArtworkUrl = trackCover.FileName;

                var album = _library.GetAlbumById(track.AlbumId);
                if (album is null) continue;

                if (album.CoverId != 0 && _library.GetCoverAsset(album.CoverId) is { } albumCover)
                    AlbumArtworkUrl = albumCover.FileName;
            }
        }

        // TODO fase 2: picker de imagen + escritura de cover vía IMetadataWriter
        [RelayCommand]
        private void CancelChanges() { }

        [RelayCommand]
        private Task SaveAsync() => Task.CompletedTask;
    }
}
