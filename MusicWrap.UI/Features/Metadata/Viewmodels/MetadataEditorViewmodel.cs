using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Metadata;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        public static string MultipleValuesString = "Mixed Values";
        private readonly MetadataEditorWorkspace _workspace;
        private readonly MetadataEditorService _metadataEditorService;
        private readonly ILibraryService _libraryService;
        private readonly ILogger _logger;
        private int _loadVersion;

        public ObservableCollection<TagRow> Rows { get; } = [];
        public ObservableCollection<TagDefinition> AvailableTags { get; } = [];

        [ObservableProperty]
        public partial TagDefinition? SelectedTagToAdd { get; set; }

        [ObservableProperty]
        public partial string TagSearchText { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveChangesCommand), nameof(CancelChangesCommand))]
        private bool isSaving;

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        public bool HasAvailableTags => AvailableTags.Count > 0;
        public bool HasChanges => Rows.Any(r => r.HasChanges);
        private bool CanSave => !IsSaving && HasChanges;
        private bool CanCancel => !IsSaving && HasChanges;
        public string? DetailTitle => DetermineTitle();

        public MetadataEditorViewmodel(MetadataEditorWorkspace workspace, ILibraryService libraryService, ILogger<MetadataEditorViewmodel> logger, MetadataEditorService metadataEditorService)
        {
            _workspace = workspace;
            _libraryService = libraryService;
            _logger = logger;
            _metadataEditorService = metadataEditorService;
        }

        public void Load()
        {
            int version = ++_loadVersion;

            foreach (var row in Rows)
                row.PropertyChanged -= OnRowPropertyChanged;

            Rows.Clear();
            AvailableTags.Clear();

            foreach (var definition in TagDefinitions.All)
                AvailableTags.Add(definition);

            IsLoading = true;
            var trackIds = _workspace.TrackIds.ToList();

            _ = LoadCoreAsync(trackIds, version);
        }

        private async Task LoadCoreAsync(IReadOnlyList<int> trackIds, int version)
        {
            try
            {
                var rows = await Task.Run(() => BuildRows(trackIds));
                if (version != _loadVersion)
                    return;

                foreach (var row in rows)
                {
                    Rows.Add(row);
                    row.PropertyChanged += OnRowPropertyChanged;
                    AvailableTags.Remove(row.Definition);
                }
                OnPropertyChanged(nameof(HasAvailableTags));
                NotifyHasChangesChanged();
                OnPropertyChanged(nameof(DetailTitle));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading tags.");
            }
            finally
            {
                if (version == _loadVersion)
                    IsLoading = false;
            }
        }

        private IReadOnlyList<TagRow> BuildRows(IReadOnlyList<int> trackIds)
        {
            var tracks = trackIds
                .Select(_libraryService.GetTrackById)
                .Where(t => t is not null && t.Origin == TrackOrigin.Local)
                .Cast<Track>()
                .ToList();
            if (tracks.Count == 0)
                return [];

            var perTrack = new IReadOnlyDictionary<string, string>?[tracks.Count];
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) };
            Parallel.ForEach(Enumerable.Range(0, tracks.Count), options, i =>
            {
                perTrack[i] = ReadTrack(tracks[i]);
            });

            var readable = perTrack.Where(d => d is not null).Select(d => d!).ToList();
            if (readable.Count == 0)
                return [];

            var rows = new List<TagRow>();
            foreach (var definition in TagDefinitions.All)
            {
                var values = readable
                    .Select(d => d.TryGetValue(definition.Key, out var value) ? value : string.Empty)
                    .ToList();
                if (values.All(string.IsNullOrWhiteSpace))
                    continue;

                bool allEqual = values.All(v => string.Equals(v, values[0], StringComparison.Ordinal));
                rows.Add(allEqual
                    ? TagRow.FromValue(definition, values[0])
                    : TagRow.FromMixed(definition, MultipleValuesString));
            }
            return rows;
        }

        private static IReadOnlyDictionary<string, string>? ReadTrack(Track track)
        {
            try
            {
                using var file = TagLib.File.Create(track.Path);
                var result = new Dictionary<string, string>();
                if (file.Tag is not { } tag)
                    return result;

                foreach (var definition in TagDefinitions.All)
                {
                    try
                    {
                        result[definition.Key] = definition.GetValue(tag) ?? string.Empty;
                    }
                    catch
                    {
                        result[definition.Key] = string.Empty;
                    }

                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        partial void OnSelectedTagToAddChanged(TagDefinition? value)
        {
            if (value is not null)
                AddTagCore(value);
        }
        private void AddTagCore(TagDefinition definition)
        {
            var row = TagRow.New(definition);
            Rows.Add(row);
            row.PropertyChanged += OnRowPropertyChanged;
            AvailableTags.Remove(definition);

            SelectedTagToAdd = null;   // handler re-entra con null → guard
            TagSearchText = string.Empty;

            OnPropertyChanged(nameof(HasAvailableTags));
            NotifyHasChangesChanged();
        }
        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void CancelChanges()
        {
            var keptKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = Rows.Count - 1; i >= 0; i--)
            {
                var row = Rows[i];
                if (row.IsNew)
                {
                    Rows.RemoveAt(i);
                    row.PropertyChanged -= OnRowPropertyChanged;
                    continue;
                }

                keptKeys.Add(row.Key);
                row.Reset();
            }

            AvailableTags.Clear();
            foreach (var definition in TagDefinitions.All)
                if (!keptKeys.Contains(definition.Key))
                    AvailableTags.Add(definition);

            SelectedTagToAdd = null;
            OnPropertyChanged(nameof(HasAvailableTags));
            NotifyHasChangesChanged();
        }
        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveChangesAsync(CancellationToken ct)
        {
            var changedRows = Rows.Where(r => r.HasChanges).ToList();
            var trackIds = _workspace.TrackIds.ToList();
            if (changedRows.Count == 0 || trackIds.Count == 0)
                return;

            IsSaving = true;

            try
            {
                int ok = 0;
                ok = await _metadataEditorService.EditTagsAsync(trackIds, tag =>
                {
                    foreach (var row in changedRows)
                        row.Definition.SetValue(tag, row.Value);
                }, ct);

                if (ok == trackIds.Count)
                {
                    foreach (var row in changedRows)
                        row.Commit();
                    _logger.LogInformation("Saved {Tags} tags on {Ok}/{Total} tracks.", changedRows.Count, ok, trackIds.Count);
                }
                else
                {
                    _logger.LogWarning("Partial save: {Ok}/{Total} tracks updated.", ok, trackIds.Count);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving tags.");
            }
            finally
            {
                IsSaving = false;
                NotifyHasChangesChanged();
                OnPropertyChanged(nameof(HasAvailableTags));
            }
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TagRow.Value) or nameof(TagRow.HasChanges))
                NotifyHasChangesChanged();
        }

        private void NotifyHasChangesChanged()
        {
            OnPropertyChanged(nameof(HasChanges));
            SaveChangesCommand.NotifyCanExecuteChanged();
            CancelChangesCommand.NotifyCanExecuteChanged();
        }

        private string? DetermineTitle()
        {
            if (!_workspace.IsSingleTrack)
                return null;
            var title = Rows.FirstOrDefault(r => r.Definition.Key == "TITLE");
            if (title is null || title.IsNew || string.IsNullOrWhiteSpace(title.Original))
                return null;
            return title.Original;
        }
    }

    public partial class TagRow : ObservableObject
    {
        public TagDefinition Definition { get; }
        public string Key => Definition.Key;
        public string DisplayName => Definition.DisplayName;
        public bool IsMultiValue => Definition.IsMultipleValue;
        public MetadataType AutocompleteType => Definition.AutocompleteType;
        public bool IsNew { get; private set; }
        public string Placeholder { get; }

        private string _original;
        public string Original => _original;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanges))]
        public partial string Value { get; set; } = string.Empty;

        public bool HasChanges => IsNew || !string.Equals(Value, _original, StringComparison.Ordinal);

        private TagRow(TagDefinition definition, string original, string placeholder, bool isNew)
        {
            Definition = definition;
            _original = original;
            Placeholder = placeholder;
            IsNew = isNew;
            Value = isNew ? string.Empty : original;
        }

        public static TagRow FromValue(TagDefinition definition, string value)
            => new(definition, value, string.Empty, false);
        public static TagRow FromMixed(TagDefinition definition, string mixedPlaceholder)
            => new(definition, string.Empty, mixedPlaceholder, false);
        public static TagRow New(TagDefinition definition)
            => new(definition, string.Empty, string.Empty, true);
        public void Reset()
        {
            if (IsNew)
                return;
            Value = _original;
        }
        public void Commit()
        {
            _original = Value;
            IsNew = false;
            OnPropertyChanged(nameof(HasChanges));
        }
    }
}

