using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Forms;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataArtworkEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        private readonly ILibraryService _library;
        private readonly MetadataEditorWorkspace _workspace;
        private bool hasDialogOpen = false;

        [ObservableProperty] public partial ArtworkEntry AlbumArtworkSource { get; set; } = new();
        [ObservableProperty] public partial ArtworkEntry TrackArtworkSource { get; set; } = new();

        public MetadataArtworkEditorViewmodel(MetadataEditorWorkspace workspace, ILibraryService library)
        {
            _workspace = workspace;
            _library = library;
        }

        #region Relay Commands
        [RelayCommand]
        public void ChangeArtwork(ArtworkEntry? entry)
        {
            if (entry is null || hasDialogOpen) return;
            hasDialogOpen = true;
            try
            {
                // TODO
            }
            finally
            {
                hasDialogOpen = false;
            }
        }
        [RelayCommand]
        public void RestoreArtwork(ArtworkEntry? entry)
        {
            if (entry is null || !entry.HasChanged) return;
            entry.RestoreOriginal();
        }
        #endregion
        public void Load()
        {
            // reset states
            AlbumArtworkSource.HardClear();
            TrackArtworkSource.HardClear();

            var trackCoverIds = new HashSet<int>();
            var albumIds = new HashSet<int>();
            var albumCoverIds = new HashSet<int>();

            foreach (var trackId in _workspace.TrackIds)
            {
                var track = _library.GetTrackById(trackId);
                if (track is null || track.Id == 0) continue;

                if (track.CoverId != 0 && _library.GetCoverAsset(track.CoverId) is { } cover)
                {
                    trackCoverIds.Add(track.CoverId);
                }

                if (track.AlbumId != 0) albumIds.Add(track.AlbumId);
            }

            foreach (var albumId in albumIds)
            {
                var album = _library.GetAlbumById(albumId);
                if (album is null || album.CoverId == 0) continue;

                if (_library.GetCoverAsset(album.CoverId) is { } cover)
                {
                    albumCoverIds.Add(album.CoverId);
                }
            }

            if (trackCoverIds.Count > 1) // multiple different track covers
            {
                TrackArtworkSource.HasMultipleValues = true;
            }
            else if (trackCoverIds.Count == 1) // 1 track cover
            {
                var coverId = trackCoverIds.First();
                var cover = _library.GetCoverAsset(coverId);
                if (cover is not null)
                {
                    TrackArtworkSource.OriginalArtworkId = cover.Id;
                    TrackArtworkSource.OriginalArtworkPath = cover.FileName;
                    TrackArtworkSource.CurrentArtworkPath = cover.FileName;
                }
            }
            else // no track cover
            {
                TrackArtworkSource.OriginalArtworkId = null;
                TrackArtworkSource.OriginalArtworkPath = string.Empty;
                TrackArtworkSource.CurrentArtworkPath = string.Empty;
            }

            if (albumCoverIds.Count > 1) // multiple different album covers
            {
                AlbumArtworkSource.HasMultipleValues = true;
            }
            else if (albumCoverIds.Count == 1) // 1 album cover
            {
                var coverId = albumCoverIds.First();
                var cover = _library.GetCoverAsset(coverId);
                if (cover is not null)
                {
                    AlbumArtworkSource.OriginalArtworkId = cover.Id;
                    AlbumArtworkSource.OriginalArtworkPath = cover.FileName;
                    AlbumArtworkSource.CurrentArtworkPath = cover.FileName;
                }
            }
            else // no album cover
            {
                AlbumArtworkSource.OriginalArtworkId = null;
                AlbumArtworkSource.OriginalArtworkPath = string.Empty;
                AlbumArtworkSource.CurrentArtworkPath = string.Empty;
            }
        }
    }

    public partial class ArtworkEntry : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial int? OriginalArtworkId { get; set; } = null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial string OriginalArtworkPath { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial string CurrentArtworkPath { get; set; } = string.Empty;

        [ObservableProperty] public partial bool EditEnabled { get; set; } = false;
        [ObservableProperty] public partial ArtworkSelectionMode SelectionMode { get; set; } = ArtworkSelectionMode.SelectFile;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial int? SelectedCoverId { get; set; } = null;

        [ObservableProperty] public partial bool HasMultipleValues { get; set; } = false;

        public List<ArtworkSelectionMode> SelectionModes { get; } = [ArtworkSelectionMode.SelectFile, ArtworkSelectionMode.ExistingFile];

        public bool HasChanged => EditEnabled && SelectionMode switch
        {
            ArtworkSelectionMode.SelectFile => !string.Equals(OriginalArtworkPath, CurrentArtworkPath, StringComparison.OrdinalIgnoreCase),
            ArtworkSelectionMode.ExistingFile => SelectedCoverId != null && SelectedCoverId != OriginalArtworkId,
            _ => false
        };

        [RelayCommand]
        public void EnableEdit()
        {
            EditEnabled = true;
        }

        [RelayCommand]
        public void RestoreOriginal()
        {
            EditEnabled = false;
            CurrentArtworkPath = OriginalArtworkPath;
            SelectedCoverId = OriginalArtworkId;
        }

        public void HardClear()
        {
            OriginalArtworkId = null;
            OriginalArtworkPath = string.Empty;
            CurrentArtworkPath = string.Empty;
            SelectedCoverId = null;
            EditEnabled = false;
            SelectionMode = 0;
            HasMultipleValues = false;
        }

    }

    public enum ArtworkSelectionMode
    {
        SelectFile = 0,
        ExistingFile = 1
    }
}
