using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Lyrics;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;
using System.Windows;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataLyricsEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {

        [ObservableProperty]
        public partial string RawLyrics { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveLyricsCommand))]
        public partial bool IsEnabled { get; set; } = false;

        private readonly LyricsProviderService _lyricsProvider;
        private readonly MetadataEditorWorkspace _workspace;
        public MetadataLyricsEditorViewmodel(LyricsProviderService lyricsProvider, MetadataEditorWorkspace workspace)
        {
            _lyricsProvider = lyricsProvider;
            _workspace = workspace;
        }
        public void Load()
        {
            if (_workspace.IsSingleTrack)
            {
                var lyrics = _lyricsProvider.GetLyricsForTrackId(_workspace.TrackIds[0]);
                RawLyrics = lyrics.RawText;
                IsEnabled = true;
            }
            else
            {
                RawLyrics = "Multiple Tracks Detected....";
                IsEnabled = false;
            }
        }

        #region Relay Commands
        [RelayCommand(CanExecute = nameof(CanSaveLyrics))]
        private async Task SaveLyrics()
        {
            var trackId = _workspace.TrackIds[0];
            var success = await _lyricsProvider.EmbedLyricsAsync(trackId, RawLyrics);
            if (success)
            {
                MessageBox.Show("Lyrics saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }else
            {
                MessageBox.Show("Failed to save lyrics.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private bool CanSaveLyrics()
        {
            return IsEnabled;
        }
        #endregion
    }
}
