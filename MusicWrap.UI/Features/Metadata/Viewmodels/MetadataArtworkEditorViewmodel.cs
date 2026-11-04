using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataArtworkEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        private readonly ILibraryService _library;
        private readonly MetadataEditorWorkspace _workspace;

        [ObservableProperty] public partial string AlbumSectionTitle { get; set; } = "Album Artwork";
        public ArtworkEntry AlbumArtworkSource { get; } = new();
        [ObservableProperty] public partial string TrackSectionTitle { get; set; } = "Track Artwork";
        public ArtworkEntry TrackArtworkSource { get; } = new();

        public bool HasChanges => AlbumArtworkSource.HasChanged || TrackArtworkSource.HasChanged;
        private bool CanSave => HasChanges;
        private bool CanCancel => HasChanges;

        public MetadataArtworkEditorViewmodel(MetadataEditorWorkspace workspace, ILibraryService library)
        {
            _workspace = workspace;
            _library = library;

            AlbumArtworkSource.PropertyChanged += OnEntryChanged;
            TrackArtworkSource.PropertyChanged += OnEntryChanged;
        }

        #region Relay Commands
        [RelayCommand(CanExecute = nameof(CanSave))]
        public void SaveChanges()
        {
            // TODO
        }

        [RelayCommand(CanExecute = nameof(CanCancel))]
        public void CancelChanges()
        {
            AlbumArtworkSource.RestoreOriginal();
            TrackArtworkSource.RestoreOriginal();
        }
        #endregion
        public void Load()
        {
            // reset states
            var options = _library.GetArtworkOptions();
            AlbumArtworkSource.HardClear();
            AlbumArtworkSource.SetAvailableArtworks(options);
            TrackArtworkSource.HardClear();
            TrackArtworkSource.SetAvailableArtworks(options);

            var trackCoverIds = new HashSet<int>();
            var albumIds = new HashSet<int>();
            var albumCoverIds = new HashSet<int>();

            TrackSectionTitle = _workspace.TrackIds.Count switch
            {
                0 => "No Track Artwork",
                1 => "Track Artwork",
                _ => $"Track Artwork • {_workspace.TrackIds.Count} tracks",
            };

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

            AlbumSectionTitle = albumIds.Count switch
            {
                0 => "No Album Artwork",
                1 => "Album Artwork",
                _ => $"Album Artwork • {albumIds.Count} albums",
            };
            foreach (var albumId in albumIds)
            {
                var album = _library.GetAlbumById(albumId);
                if (album is null || album.CoverId == 0) continue;

                if (_library.GetCoverAsset(album.CoverId) is { } cover)
                {
                    albumCoverIds.Add(album.CoverId);
                }
            }

            ApplyCover(TrackArtworkSource, trackCoverIds);
            ApplyCover(AlbumArtworkSource, albumCoverIds);

            NotifyHasChangesChanged();
        }
        private void ApplyCover(ArtworkEntry entry, HashSet<int> coverIds)
        {
            if (coverIds.Count > 1)
            {
                entry.HasMultipleValues = true;
                return;
            }

            if (coverIds.Count == 1 && _library.GetCoverAsset(coverIds.First()) is { } cover)
            {
                entry.OriginalArtworkId = cover.Id;
                entry.OriginalArtworkPath = cover.FileName;
                entry.CurrentArtworkPath = cover.FileName;
                return;
            }

            entry.OriginalArtworkId = null;
            entry.OriginalArtworkPath = string.Empty;
            entry.CurrentArtworkPath = string.Empty;
        }

        private void OnEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ArtworkEntry.HasChanged))
                NotifyHasChangesChanged();
        }

        private void NotifyHasChangesChanged()
        {
            OnPropertyChanged(nameof(HasChanges));
            SaveChangesCommand.NotifyCanExecuteChanged();
            CancelChangesCommand.NotifyCanExecuteChanged();
        }
    }

    public partial class ArtworkEntry : ObservableObject
    {
        public const int MaxSuggestions = 10;

        private IReadOnlyList<ArtworkOption> _available = [];
        [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
        [ObservableProperty] public partial IReadOnlyList<ArtworkOption> Suggestions { get; set; } = [];
        [ObservableProperty] public partial ArtworkOption? PickedArtwork { get; set; }
        [ObservableProperty] public partial bool HasSuggestions { get; set; }




        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial int? OriginalArtworkId { get; set; } = null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial string OriginalArtworkPath { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial string CurrentArtworkPath { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelectFileCommand))]
        [NotifyPropertyChangedFor(nameof(HasChanged))]

        public partial bool EditEnabled { get; set; } = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelectFileCommand))]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial ArtworkSelectionMode SelectionMode { get; set; } = ArtworkSelectionMode.SelectFile;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial int? SelectedCoverId { get; set; } = null;

        [ObservableProperty] public partial bool HasMultipleValues { get; set; } = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelectFileCommand))]
        public partial bool HasSelectorOpen { get; set; } = false;

        public List<ArtworkSelectionMode> SelectionModes { get; } = [ArtworkSelectionMode.SelectFile, ArtworkSelectionMode.ExistingFile];

        public bool HasChanged => EditEnabled && SelectionMode switch
        {
            ArtworkSelectionMode.SelectFile => !string.Equals(OriginalArtworkPath, CurrentArtworkPath, StringComparison.OrdinalIgnoreCase),
            ArtworkSelectionMode.ExistingFile => SelectedCoverId != null && SelectedCoverId != OriginalArtworkId,
            _ => false
        };
        #region Relay Commands
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

            SearchText = string.Empty;
            PickedArtwork = null;
        }

        [RelayCommand(CanExecute = nameof(CanSelectFile))]
        public void SelectFile()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                Title = "Select Artwork Image",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
            };

            if (dialog.ShowDialog() != true) return;

            var selectedFile = dialog.FileName;
            if (!string.IsNullOrEmpty(selectedFile))
            {
                CurrentArtworkPath = selectedFile;
            }
        }
        private bool CanSelectFile()
        {
            return EditEnabled && SelectionMode == ArtworkSelectionMode.SelectFile && !HasSelectorOpen;
        }

        [RelayCommand]
        public void ClearSelection()
        {
            CurrentArtworkPath = OriginalArtworkPath;
            SelectedCoverId = null;
            SearchText = string.Empty;
            PickedArtwork = null;
        }
        #endregion

        #region Internal
        public void HardClear()
        {
            OriginalArtworkId = null;
            OriginalArtworkPath = string.Empty;
            CurrentArtworkPath = string.Empty;
            SelectedCoverId = null;
            EditEnabled = false;
            SelectionMode = ArtworkSelectionMode.ExistingFile;
            HasMultipleValues = false;
            SearchText = string.Empty;
            Suggestions = [];
            PickedArtwork = null;
            HasSuggestions = false;
        }
        public void SetAvailableArtworks(IReadOnlyList<ArtworkOption> available)
        {
            _available = available;
            RefreshSuggestions();
        }

        private void RefreshSuggestions()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                Suggestions = [];
                HasSuggestions = false;
                return;
            }

            Suggestions = _available
                .Where(a => a.Matches(SearchText))
                .Take(MaxSuggestions)
                .ToList();

            HasSuggestions = Suggestions.Count > 0;
        }
        #endregion
        #region Partial Methods
        partial void OnSearchTextChanged(string value) => RefreshSuggestions();
        partial void OnEditEnabledChanged(bool value)
        {
            CurrentArtworkPath = OriginalArtworkPath;
            SelectedCoverId = OriginalArtworkId;
            SelectionMode = ArtworkSelectionMode.SelectFile;
            SearchText = string.Empty;
            PickedArtwork = null;
        }

        partial void OnPickedArtworkChanged(ArtworkOption? value)
        {
            if (value is null) return;

            SelectedCoverId = value.Id;
            CurrentArtworkPath = value.FilePath;

        }
        #endregion
    }

    public enum ArtworkSelectionMode
    {
        SelectFile = 0,
        ExistingFile = 1
    }
}
