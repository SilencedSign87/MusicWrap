using MusicWrap.UI.Features.Lyrics.View;
using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Shell.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Lógica de interacción para FullScreenWindow.xaml
    /// </summary>
    public partial class FullScreenWindow : UserControl
    {
        public FullScreenWindow(FullscreenWindowViewModel viewmodel, LyricsView lyricsView)
        {
            InitializeComponent();

            DataContext = viewmodel;
            lyricsView.FontSize = 36.0;
            LyricsContainer.Child = lyricsView;
            viewmodel.NowPlayingViewModel.PropertyChanged += OnViewmodelPropertyChanged;
        }

        private void OnViewmodelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NowPlayingViewModel.ShowLyrics))
            {
                UpdateLyricsLayout(((FullscreenWindowViewModel)DataContext).NowPlayingViewModel.ShowLyrics);
            }
        }

        private readonly Thickness LyricsPadding = new(48);
        private readonly Thickness NoLyricsPadding = new(0);
        private readonly GridLength LyricsWidth = new(1, GridUnitType.Star);
        private readonly GridLength NoLyricsWidth = new(0);

        private void UpdateLyricsLayout(bool showLyrics)
        {
            if (showLyrics)
            {
                MainContentGrid.ColumnDefinitions[1].Width = LyricsWidth;
                LyricsContainer.Padding = LyricsPadding;
            }
            else
            {
                MainContentGrid.ColumnDefinitions[1].Width = NoLyricsWidth;
                LyricsContainer.Padding = NoLyricsPadding;
            }
        }

        private void RootFullScreenWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLyricsLayout(((FullscreenWindowViewModel)DataContext).NowPlayingViewModel.ShowLyrics);
        }
    }
}
