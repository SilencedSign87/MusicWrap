using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        private readonly ILibraryService _library;
        private readonly MetadataEditorWorkspace _workspace;

        public EditableField Title { get; } = new();
        public EditableField Artist { get; } = new();
        public EditableField Album { get; } = new();
        public EditableField AlbumArtist { get; } = new();
        public EditableField Year { get; } = new();
        public EditableField TrackNumber { get; } = new();
        public EditableField DiskNumber { get; } = new();
        public EditableField Genre { get; } = new();
        public IEnumerable<EditableField> AllFields => [Title, Artist, Album, AlbumArtist, Year, TrackNumber, DiskNumber, Genre];

        public bool HasChanges => AllFields.Any(f => f.HasChanges);
        public string? DetailTitle => _workspace.IsSingleTrack ? Title.Original : null;
        public MetadataEditorWorkspace Workspace => _workspace;

        public MetadataEditorViewmodel(MetadataEditorWorkspace workspace, ILibraryService library)
        {
            _workspace = workspace;
            _library = library;

            foreach (var f in AllFields)
                f.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(EditableField.Value) or nameof(EditableField.Original))
                        OnPropertyChanged(nameof(HasChanges));
                };
        }

        public void Load()
        {
            foreach (var f in AllFields) f.Clear();

            foreach (var trackId in _workspace.TrackIds)
            {
                var track = _library.GetTrackById(trackId);

                if (track is not null && track.Id != 0)
                {
                    // Track Properties
                    Title.ApplyValue(track.Title);
                    Artist.ApplyValue(_library.GetArtistNamesForTrack(trackId));
                    TrackNumber.ApplyValue(track.TrackNumber.ToString());
                    DiskNumber.ApplyValue(track.Disk.ToString());
                    Genre.ApplyValue(string.Join(", ", _library.GetGenreById([.. track.GenreIds]).Select(g => g.Name)));

                    // Album Properties
                    var album = _library.GetAlbumById(track.AlbumId);
                    if (album is null)
                        continue;

                    Album.ApplyValue(album.Title);
                    AlbumArtist.ApplyValue(_library.GetArtistNamesForAlbum(album.Id));
                    Year.ApplyValue(album.Year.ToString());
                }
            }

            OnPropertyChanged(nameof(HasChanges));
        }
    }

    public partial class EditableField : ObservableObject
    {
        public const string MixedPlaceholder = "--mixed--";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanges))]
        private string value = string.Empty;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChanges))]
        private string original = string.Empty;
        [ObservableProperty]
        private string placeholder = string.Empty;
        public bool IsMixed => Placeholder == MixedPlaceholder;
        public bool HasChanges => !string.Equals(Value, Original);

        public void ApplyValue(string newValue)
        {
            if (string.IsNullOrEmpty(Value) && !IsMixed)
            {
                Value = newValue;
                Original = newValue;
                Placeholder = string.Empty;
            }
            else if (!Value.Equals(newValue))
            {
                Original = string.Empty;
                Value = string.Empty;
                Placeholder = MixedPlaceholder;
            }
        }
        public void Reset() => Value = Original;
        public void Clear() { Value = string.Empty; Original = string.Empty; Placeholder = string.Empty; }
    }
}
