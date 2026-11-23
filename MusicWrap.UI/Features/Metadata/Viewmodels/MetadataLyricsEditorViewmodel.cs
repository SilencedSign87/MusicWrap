using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Core.Services.Lyrics;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;

using lyricsModel = MusicWrap.Data.Library.Models.Lyrics;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataLyricsEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        private readonly LyricsProviderService _lyricsService;
        private readonly MetadataEditorWorkspace _workspace;

        [ObservableProperty]
        public partial LyricsEntryViewModel DefaultLyrics { get; set; }
         = LyricsEntryViewModel.New(isDefault: true);

        public ObservableCollection<LyricsEntryViewModel> LyricsVariants { get; } = [];

        public List<LyricsOrigin> Origins => Enum.GetValues<LyricsOrigin>().ToList();

        public bool HasChanges =>
            DefaultLyrics.CanSave || LyricsVariants.Any(v => v.CanSave);


        public MetadataLyricsEditorViewmodel(MetadataEditorWorkspace workspace, LyricsProviderService lyricsProvider)
        {
            _workspace = workspace;
            _lyricsService = lyricsProvider;

            DefaultLyrics.PropertyChanged -= OnEntryChanged;
            DefaultLyrics.PropertyChanged += OnEntryChanged;

            LyricsVariants.CollectionChanged += (_, e) =>
            {
                if (e.OldItems is not null)
                    foreach (LyricsEntryViewModel old in e.OldItems)
                        old.PropertyChanged -= OnEntryChanged;

                if (e.NewItems is not null)
                    foreach (LyricsEntryViewModel added in e.NewItems)
                        added.PropertyChanged += OnEntryChanged;

                OnPropertyChanged(nameof(HasChanges));

            };
        }


        public void Load()
        {
            LyricsVariants.Clear();

            // Una lista por track
            var perTrack = _workspace.TrackIds
                .Select(id => _lyricsService.LoadLyricsByTrackId(id).ToList())
                .ToList();

            DefaultLyrics = BuildDefault(perTrack);
            BuildVariants(perTrack);
        }

        partial void OnDefaultLyricsChanged(LyricsEntryViewModel value)
        {
            value.PropertyChanged -= OnEntryChanged;
            value.PropertyChanged += OnEntryChanged;
            OnPropertyChanged(nameof(HasChanges));
        }

        private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(LyricsEntryViewModel.HasChanged) or nameof(LyricsEntryViewModel.CanSave))
                OnPropertyChanged(nameof(HasChanges));
        }

        private static LyricsEntryViewModel BuildDefault(IReadOnlyList<List<lyricsModel>> perTrack)
        {
            var defaults = perTrack
                .Select(rows => rows.FirstOrDefault(l => l.Default))
                .Where(l => l is not null)
                .Select(l => l!)
                .ToList();

            if (defaults.Count == 0)
                return LyricsEntryViewModel.New(isDefault: true);

            // Igualdad por contenido, no por referencia
            var distinct = defaults
                .Select(LyricsEntryViewModel.FromModel)
                .Distinct()
                .ToList();

            return distinct.Count == 1
                ? LyricsEntryViewModel.Existing(defaults[0])
                : LyricsEntryViewModel.Multiple(isDefault: true);
        }

        private void BuildVariants(IReadOnlyList<List<lyricsModel>> perTrack)
        {
            var groups = perTrack
                .SelectMany(rows => rows.Where(l => !l.Default))
                .GroupBy(l => (l.Language, l.Type));

            foreach (var group in groups)
            {
                var drafts = group
                    .Select(LyricsEntryViewModel.FromModel)
                    .Distinct()
                    .ToList();

                LyricsVariants.Add(drafts.Count == 1
                    ? LyricsEntryViewModel.Existing(group.First())
                    : LyricsEntryViewModel.Multiple(isDefault: false));
            }
        }

        #region Relay Commands
        [RelayCommand]
        private void AddTranslation() =>
            LyricsVariants.Add(LyricsEntryViewModel.New(isDefault: false));

        [RelayCommand]
        private void DeleteEntry(LyricsEntryViewModel entry)
        {
            if (!entry.CanDelete) return;

            // Draft
            if (entry.State == LyricsEntryState.New)
            {
                if (entry.IsDefault)
                    DefaultLyrics = LyricsEntryViewModel.New(isDefault: true);
                else
                    LyricsVariants.Remove(entry);

                return;
            }

            // Existing
            entry.PendingDelete = !entry.PendingDelete;
        }
        #endregion
    }

    public partial class LyricsEntryViewModel : ObservableObject
    {
        // snapshot
        private readonly LyricsDraft? _original;

        // identity
        public int? SourceId { get; }
        public LyricsEntryState State { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial string Language { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial bool IsDefault { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial LyricsType Type { get; set; } = LyricsType.plain;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial LyricsOrigin Origin { get; set; } = LyricsOrigin.Manual;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial string Provider { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial string? Text { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanged))]
        public partial bool PendingDelete { get; set; } = false;


        public bool CanDelete => State != LyricsEntryState.Multiple;
        public bool IsEditable => State != LyricsEntryState.Multiple;
        public bool IsNew => State == LyricsEntryState.New;
        public bool IsMultiple => State == LyricsEntryState.Multiple;
        public bool HasContent => !string.IsNullOrWhiteSpace(Text);
        public bool HasChanged => IsEditable && (PendingDelete || ToDraft() != _original);
        public bool CanSave => IsEditable && (PendingDelete || (State == LyricsEntryState.New ? HasContent : HasChanged));


        private LyricsEntryViewModel(LyricsEntryState state, int? sourceId, LyricsDraft draft)
        {
            State = state;
            SourceId = sourceId;
            _original = draft;

            Language = draft.Language;
            IsDefault = draft.IsDefault;
            Type = draft.Type;
            Origin = draft.Origin;
            Provider = draft.Provider;
            Text = draft.Text;
        }

        public static LyricsEntryViewModel New(bool isDefault) =>
            new(LyricsEntryState.New, null, new LyricsDraft { IsDefault = isDefault });

        public static LyricsEntryViewModel Existing(lyricsModel source) =>
            new(LyricsEntryState.Existing, source.Id, FromModel(source));

        public static LyricsEntryViewModel Multiple(bool isDefault) =>
            new(LyricsEntryState.Multiple, null, new LyricsDraft { IsDefault = isDefault });

        public static LyricsDraft FromModel(lyricsModel source) => new()
        {
            Language = source.Language,
            IsDefault = source.Default,
            Type = source.Type,
            Origin = source.Origin,
            Provider = source.Provider,
            Text = source.LyricsText,
        };

        public LyricsDraft ToDraft() => new()
        {
            Language = Language,
            IsDefault = IsDefault,
            Type = Type,
            Origin = Origin,
            Provider = Provider,
            Text = Text,
        };


        #region Relay Commands
        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void SelectExternalFile()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                Title = "Select lyrics file",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Filter = "Lyrics files |*.lrc|Text files |*.txt "
            };

            if (dialog.ShowDialog() != true) return;

            Origin = LyricsOrigin.File;
            Provider = dialog.FileName;
        }
        private bool CanEdit() => IsEditable;
        [RelayCommand]
        private void SaveChanges()
        {

        }
        [RelayCommand]
        private void CancelChanges()
        {

        }
        #endregion

        partial void OnOriginChanged(LyricsOrigin value)
        {
            if (value == LyricsOrigin.Embedded)
                Provider = string.Empty;
        }
    }

    public sealed record LyricsDraft
    {
        public string Language { get; init; } = string.Empty;
        public bool IsDefault { get; init; }
        public LyricsType Type { get; init; } = LyricsType.plain;
        public LyricsOrigin Origin { get; init; } = LyricsOrigin.Manual;
        public string Provider { get; init; } = string.Empty;
        public string? Text { get; init; }
    }
    public enum LyricsEntryState
    {
        New,
        Existing,
        Multiple
    }
}
