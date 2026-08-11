using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Forms;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataArtworkEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        private readonly ILibraryService _library;
        private readonly MetadataEditorWorkspace _workspace;
        private readonly List<ArtworkEntry> _entries = [];
        private bool hasDialogOpen = false;

        [ObservableProperty]
        public partial CollectionViewSource ArtworksViewSource { get; set; } = new();

        public bool HasChanges => _entries.Any(e => e.HasChanged);

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
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Image files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png|All files (*.*)|*.*",
                    Title = $"Select new artwork for {entry.Title}",
                    Multiselect = false
                };
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    entry.SetArtwork(openFileDialog.FileName);
                }
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
            foreach (var e in _entries) e.PropertyChanged -= OnEntryPropertyChanged;
            _entries.Clear();

            var albumIds = new HashSet<int>();

            foreach (var trackId in _workspace.TrackIds)
            {
                var track = _library.GetTrackById(trackId);
                if (track is null || track.Id == 0) continue;

                if (track.CoverId != 0 && _library.GetCoverAsset(track.CoverId) is { } cover)
                {
                    var entry = new ArtworkEntry
                    {
                        SortOrder = track.TrackNumber + track.Disk*100,
                        Title = track.Title,
                        CoverId = track.CoverId,
                        TrackId = track.Id,
                        CurrentFilePath = cover.FileName,
                        OriginalFilePath = cover.FileName,
                        Type = ArtworkType.Track
                    };
                    _entries.Add(entry);
                    entry.PropertyChanged += OnEntryPropertyChanged;
                }
                if (track.AlbumId != 0) albumIds.Add(track.AlbumId);
            }

            foreach (var albumId in albumIds)
            {
                var album = _library.GetAlbumById(albumId);
                if (album is null || album.CoverId == 0) continue;
                if (_library.GetCoverAsset(album.CoverId) is not { } albumCover) continue;
                var entry = new ArtworkEntry
                {
                    Title = album.Title,
                    CoverId = album.CoverId,
                    AlbumId = album.Id,
                    CurrentFilePath = albumCover.FileName,
                    OriginalFilePath = albumCover.FileName,
                    Type = ArtworkType.Album
                };
                _entries.Add(entry);
                entry.PropertyChanged += OnEntryPropertyChanged;
            }

            var viewSource = new CollectionViewSource { Source = _entries };

            viewSource.GroupDescriptions.Add(
                new PropertyGroupDescription(nameof(ArtworkEntry.Type)));

            viewSource.SortDescriptions.Add(new SortDescription(nameof(ArtworkEntry.Type), ListSortDirection.Descending));
            viewSource.SortDescriptions.Add(new SortDescription(nameof(ArtworkEntry.SortOrder), ListSortDirection.Ascending));

            ArtworksViewSource = viewSource;
        }

        private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ArtworkEntry.HasChanged))
                OnPropertyChanged(nameof(HasChanges));
        }

        [RelayCommand]
        private void CancelChanges() {
            foreach (var entry in _entries)
            {
                if (entry.HasChanged)
                {
                    entry.RestoreOriginal();
                }
            }
        }

        [RelayCommand]
        private Task SaveAsync() => Task.CompletedTask;
    }

    public class ArtworkEntry : ObservableClass
    {
        private string _currentFilePath = string.Empty;
        private bool _hasChanged = false;
        public int? TrackId { get; set; } = 0;
        public int? AlbumId { get; set; } = 0;
        public int CoverId { get; set; } = 0;
        public int SortOrder { get; set; } = 0;
        public string OriginalFilePath { get; set; } = string.Empty;
        public string CurrentFilePath
        {
            get { return _currentFilePath; }
            set
            { SetProperty(ref _currentFilePath, value); }
        }
        public bool HasChanged
        {
            get { return _hasChanged; }
            set { SetProperty(ref _hasChanged, value); }
        }
        public string Title { get; set; } = string.Empty;
        public ArtworkType Type { get; set; } = ArtworkType.Track;

        public void SetArtwork(string newFilePath)
        {
            CurrentFilePath = newFilePath;
            HasChanged = true;
        }
        public void RestoreOriginal()
        {
            CurrentFilePath = OriginalFilePath;
            HasChanged = false;
        }
    }

    public enum ArtworkType
    {
        Track,
        Album
    }
}
