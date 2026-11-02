using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Metadata;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        public static string MultipleValuesString = "Mixed Values";
        private readonly MetadataEditorWorkspace _workspace;
        private readonly ILibraryService _libraryService;
        private readonly IMetadataAutocompleteService _autocomplete;
        private readonly LocalMetadataEditorService _localMetadata;
        private readonly ILogger _logger;
        private Snapshot _original = Snapshot.Empty;


        public MetadataEditorViewmodel(MetadataEditorWorkspace workspace, ILibraryService libraryService, ILogger<MetadataEditorViewmodel> logger, IMetadataAutocompleteService autocomplete, LocalMetadataEditorService localMetadata)
        {
            _workspace = workspace;
            _libraryService = libraryService;
            _logger = logger;
            _autocomplete = autocomplete;
            _localMetadata = localMetadata;

            Artists.CollectionChanged += OnListChanged;
            AlbumArtists.CollectionChanged += OnListChanged;
            Genres.CollectionChanged += OnListChanged;
        }

        #region Properties

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DetailTitle))]
        public partial string Title { get; set; } = string.Empty;

        [ObservableProperty] public partial string Album { get; set; } = string.Empty;
        [ObservableProperty] public partial string Year { get; set; } = string.Empty;
        [ObservableProperty] public partial string TrackNumber { get; set; } = string.Empty;
        [ObservableProperty] public partial string TrackCount { get; set; } = string.Empty;
        [ObservableProperty] public partial string DiscNumber { get; set; } = string.Empty;
        [ObservableProperty] public partial string DiscCount { get; set; } = string.Empty;
        [ObservableProperty] public partial string Rating { get; set; } = string.Empty;
        [ObservableProperty] public partial string PlayCount { get; set; } = string.Empty;

        public ObservableCollection<string> Artists { get; } = [];
        public ObservableCollection<string> AlbumArtists { get; } = [];
        public ObservableCollection<string> Genres { get; } = [];

        [ObservableProperty] public partial string TitlePlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string AlbumPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string YearPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string TrackNumberPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string TrackCountPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string DiscNumberPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string DiscCountPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string ArtistsPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string AlbumArtistsPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string GenresPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string RatingPlaceholder { get; set; } = string.Empty;
        [ObservableProperty] public partial string PlayCountPlaceholder { get; set; } = string.Empty;

        [ObservableProperty] public partial IReadOnlyList<string> ArtistSuggestions { get; set; } = [];
        [ObservableProperty] public partial IReadOnlyList<string> AlbumSuggestions { get; set; } = [];
        [ObservableProperty] public partial IReadOnlyList<string> AlbumArtistSuggestions { get; set; } = [];
        [ObservableProperty] public partial IReadOnlyList<string> GenreSuggestions { get; set; } = [];

        #endregion

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveChangesCommand), nameof(CancelChangesCommand))]
        private bool isSaving;

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        public bool HasChanges => BuildChanges().Count > 0;
        private bool CanSave => !IsSaving && HasChanges;
        private bool CanCancel => !IsSaving && HasChanges;

        public string? DetailTitle =>
            _workspace.IsSingleTrack && !string.IsNullOrWhiteSpace(_original.Title)
                ? _original.Title
                : null;

        public void Load()
        {
            IsLoading = true;

            try
            {
                var snapshots = _workspace.TrackIds
                    .Select(_libraryService.GetTrackById)
                    .Where(t => t is { Origin: TrackOrigin.Local })
                    .Cast<Track>()
                    .Select(t => (Track: t, Album: _libraryService.GetAlbumById(t.AlbumId)))
                    .ToArray();

                Title = Scalar(snapshots, LocalTagKey.Title, (p) => TitlePlaceholder = p);
                Album = Scalar(snapshots, LocalTagKey.Album, (p) => AlbumPlaceholder = p);
                Year = Scalar(snapshots, LocalTagKey.Year, (p) => YearPlaceholder = p);
                TrackNumber = Scalar(snapshots, LocalTagKey.TrackNumber, (p) => TrackNumberPlaceholder = p);
                TrackCount = Scalar(snapshots, LocalTagKey.TrackCount, (p) => TrackCountPlaceholder = p);
                DiscNumber = Scalar(snapshots, LocalTagKey.DiscNumber, (p) => DiscNumberPlaceholder = p);
                DiscCount = Scalar(snapshots, LocalTagKey.DiscCount, (p) => DiscCountPlaceholder = p);
                Rating = Scalar(snapshots, LocalTagKey.Rating, (p) => RatingPlaceholder = p);
                PlayCount = Scalar(snapshots, LocalTagKey.PlayCount, (p) => PlayCountPlaceholder = p);

                FillList(Artists, snapshots, LocalTagKey.Artist, p => ArtistsPlaceholder = p);
                FillList(AlbumArtists, snapshots, LocalTagKey.AlbumArtist, p => AlbumArtistsPlaceholder = p);
                FillList(Genres, snapshots, LocalTagKey.Genre, p => GenresPlaceholder = p);

                AlbumSuggestions = _autocomplete.GetAllValues(MetadataType.AlbumTitle);
                ArtistSuggestions = _autocomplete.GetAllValues(MetadataType.ArtistName);
                AlbumArtistSuggestions = ArtistSuggestions;
                GenreSuggestions = _autocomplete.GetAllValues(MetadataType.GenreName);

                _original = Capture();
                NotifyHasChangesChanged();
                OnPropertyChanged(nameof(DetailTitle));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading local metadata.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string Scalar(IReadOnlyList<(Track Track, Album? Album)> snapshots, LocalTagKey key, Action<string> setPlaceholder)
        {
            if (snapshots.Count == 0)
                return string.Empty;

            string value = First(snapshots[0], key);

            for (int i = 1; i < snapshots.Count; i++)
            {
                if (!string.Equals(value, First(snapshots[i], key), StringComparison.OrdinalIgnoreCase))
                {
                    setPlaceholder(MultipleValuesString);
                    return string.Empty;
                }
            }

            return value;
        }

        private string First((Track Track, Album? Album) snapshot, LocalTagKey key)
        {
            var values = _localMetadata.GetValues(key, snapshot.Track, snapshot.Album);
            return values.Count > 0 ? values[0] : string.Empty;
        }

        private void FillList(
            ObservableCollection<string> target,
            IReadOnlyList<(Track Track, Album? Album)> snapshots,
            LocalTagKey key,
            Action<string> setPlaceholder)
        {
            target.Clear();
            setPlaceholder(string.Empty);

            if (snapshots.Count == 0)
                return;

            var first = _localMetadata.GetValues(key, snapshots[0].Track, snapshots[0].Album);

            for (int i = 1; i < snapshots.Count; i++)
            {
                var next = _localMetadata.GetValues(key, snapshots[i].Track, snapshots[i].Album);

                if (!next.SequenceEqual(first, StringComparer.OrdinalIgnoreCase))
                {
                    setPlaceholder(MultipleValuesString);
                    return;
                }
            }

            foreach (var value in first)
                if (!string.IsNullOrWhiteSpace(value))
                    target.Add(value);
        }

        private Snapshot Capture() => new(
           Title, [.. Artists],
           Album, [.. AlbumArtists],
           [.. Genres],
           Year, TrackNumber, TrackCount, DiscNumber, DiscCount, Rating, PlayCount);

        private List<LocalTagChange> BuildChanges()
        {
            var changes = new List<LocalTagChange>(12);
            var original = _original;

            if (!string.Equals(Title, original.Title, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.Title, [Title]));

            if (!Artists.SequenceEqual(original.Artists, StringComparer.Ordinal))
                changes.Add(new(LocalTagKey.Artist, [.. Artists]));

            if (!string.Equals(Album, original.Album, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.Album, [Album]));

            if (!AlbumArtists.SequenceEqual(original.AlbumArtists, StringComparer.Ordinal))
                changes.Add(new(LocalTagKey.AlbumArtist, [.. AlbumArtists]));

            if (!Genres.SequenceEqual(original.Genres, StringComparer.Ordinal))
                changes.Add(new(LocalTagKey.Genre, [.. Genres]));

            if (!string.Equals(Year, original.Year, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.Year, [Year]));

            if (!string.Equals(TrackNumber, original.TrackNumber, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.TrackNumber, [TrackNumber]));

            if (!string.Equals(TrackCount, original.TrackCount, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.TrackCount, [TrackCount]));

            if (!string.Equals(DiscNumber, original.DiscNumber, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.DiscNumber, [DiscNumber]));

            if (!string.Equals(DiscCount, original.DiscCount, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.DiscCount, [DiscCount]));

            if (!string.Equals(Rating, original.Rating, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.Rating, [Rating]));

            if (!string.Equals(PlayCount, original.PlayCount, StringComparison.Ordinal))
                changes.Add(new(LocalTagKey.PlayCount, [PlayCount]));

            return changes;
        }

        #region Relay Commands

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void CancelChanges() => Load();

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void SaveChanges()
        {
            var trackIds = _workspace.TrackIds.ToList();
            var changes = BuildChanges();

            if (trackIds.Count == 0 || changes.Count == 0)
                return;

            IsSaving = true;

            try
            {
                int updated = _localMetadata.Apply(trackIds, changes);

                if (updated > 0)
                {
                    _original = Capture();
                    _logger.LogInformation(
                        "Saved {Tags} local tags on {Updated}/{Total} tracks.",
                        changes.Count, updated, trackIds.Count);
                    MessageBox.Show("Changes saved correctly", "Metadata editor", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving metadata", "Metadata editor", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError(ex, "Error saving local metadata.");
            }
            finally
            {
                IsSaving = false;
                NotifyHasChangesChanged();
                OnPropertyChanged(nameof(DetailTitle));
            }
        }
        #endregion

        #region Events
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName is nameof(HasChanges) or nameof(DetailTitle))
                return;

            NotifyHasChangesChanged();
        }

        private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => NotifyHasChangesChanged();

        private void NotifyHasChangesChanged()
        {
            OnPropertyChanged(nameof(HasChanges));
            SaveChangesCommand.NotifyCanExecuteChanged();
            CancelChangesCommand.NotifyCanExecuteChanged();
        }

        #endregion


        #region Models
        private sealed record Snapshot(
            string Title,
            IReadOnlyList<string> Artists,
            string Album,
            IReadOnlyList<string> AlbumArtists,
            IReadOnlyList<string> Genres,
            string Year,
            string TrackNumber,
            string TrackCount,
            string DiscNumber,
            string DiscCount,
            string Rating,
            string PlayCount)
        {
            public static Snapshot Empty { get; } = new(
                string.Empty, [], string.Empty, [], [], string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        private sealed record MergedList(bool IsMixed, IReadOnlyList<string> Values);

        #endregion
    }
}

