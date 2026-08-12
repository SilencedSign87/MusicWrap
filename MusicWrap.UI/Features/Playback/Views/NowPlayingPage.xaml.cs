using MusicWrap.UI.Features.Playback.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Playback.Views
{
    public partial class NowPlayingPage : UserControl
    {
        private const double BalancedMinRatio = 1.4;
        private const double LandscapeMinRatio = 1.7;
        private const double VisualizerHeight = 120;
        private const double LyricsBandHeight = 140;

        private readonly NowPlayingViewModel _viewModel;
        public NowPlayingPage(NowPlayingViewModel viewmodel)
        {
            InitializeComponent();
            _viewModel = viewmodel;
            DataContext = viewmodel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyLayout();
        }
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NowPlayingViewModel.ShowLyrics) ||
                e.PropertyName == nameof(NowPlayingViewModel.IsVisualizerVisible))
            {
                ApplyLayout();
            }
        }

        private void OnNowPlayingLayoutSizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayout();
        private void ApplyLayout()
        {
            var mode = ResolveLayoutMode();

            switch (mode)
            {
                case NowPlayingLayoutMode.Compact: ApplyCompactLayout(); break;
                case NowPlayingLayoutMode.Portrait: ApplyPortraitLayout(); break;
                case NowPlayingLayoutMode.Balanced: ApplyBalancedLayout(); break;
                case NowPlayingLayoutMode.Landscape: ApplyLandscapeLayout(); break;
            }
        }
        private NowPlayingLayoutMode ResolveLayoutMode()
        {
            bool showLyrics = _viewModel.ShowLyrics;
            bool showVisualizer = _viewModel.IsVisualizerVisible;
            // no panels
            if (!showLyrics && !showVisualizer) return NowPlayingLayoutMode.Compact;
            // no sidebar
            if (!showLyrics) return NowPlayingLayoutMode.Portrait;
            double ratio = ActualWidth / ActualHeight;
            if (double.IsNaN(ratio)) ratio = 1.0;
            // no visualizer
            if (!showVisualizer)
                return ratio < BalancedMinRatio
                    ? NowPlayingLayoutMode.Portrait
                    : NowPlayingLayoutMode.Landscape;
            // all active
            if (ratio < BalancedMinRatio) return NowPlayingLayoutMode.Portrait;
            if (ratio < LandscapeMinRatio) return NowPlayingLayoutMode.Balanced;
            return NowPlayingLayoutMode.Landscape;
        }
        private void ApplyCompactLayout()
        {
            ArtworkColumn.Width = new GridLength(1, GridUnitType.Star);
            LyricsColumn.Width = new GridLength(0);
            ArtworkRow.Height = new GridLength(1, GridUnitType.Star);
            LyricsRow.Height = new GridLength(0);
            VisualizerRow.Height = new GridLength(0);
            Place(ArtworkHost, 0, 0);
        }
        private void ApplyPortraitLayout()
        {
            double visualizer = _viewModel.IsVisualizerVisible ? VisualizerHeight : 0;
            double lyrics = _viewModel.ShowLyrics ? LyricsBandHeight : 0;
            ArtworkColumn.Width = new GridLength(1, GridUnitType.Star);
            LyricsColumn.Width = new GridLength(0);
            ArtworkRow.Height = new GridLength(1, GridUnitType.Star);
            LyricsRow.Height = new GridLength(lyrics);
            VisualizerRow.Height = new GridLength(visualizer);
            Place(ArtworkHost, 0, 0);
            Place(LyricsHost, 1, 0, columnSpan: 2);
            Place(VisualizerHost, 2, 0, columnSpan: 2);
        }
        private void ApplyBalancedLayout()
        {
            double visualizer = _viewModel.IsVisualizerVisible ? VisualizerHeight : 0;
            double side = Math.Max(0, ActualHeight - visualizer);
            ArtworkColumn.Width = new GridLength(side);
            LyricsColumn.Width = new GridLength(1, GridUnitType.Star);
            ArtworkRow.Height = new GridLength(1, GridUnitType.Star);
            LyricsRow.Height = new GridLength(0);
            VisualizerRow.Height = new GridLength(visualizer);
            Place(ArtworkHost, 0, 0);
            Place(LyricsHost, 0, 1);
            Place(VisualizerHost, 2, 0, columnSpan: 2);
        }
        private void ApplyLandscapeLayout()
        {
            double visualizer = _viewModel.IsVisualizerVisible ? VisualizerHeight : 0;
            double side = Math.Max(0, ActualHeight);
            ArtworkColumn.Width = new GridLength(side);
            LyricsColumn.Width = new GridLength(1, GridUnitType.Star);
            ArtworkRow.Height = new GridLength(1, GridUnitType.Star);
            LyricsRow.Height = new GridLength(visualizer);
            VisualizerRow.Height = new GridLength(0);
            Place(ArtworkHost, 0, 0, rowSpan: 2);
            Place(LyricsHost, 0, 1);
            Place(VisualizerHost, 1, 1);
        }
        private static void Place(FrameworkElement element, int row, int column, int rowSpan = 1, int columnSpan = 1)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            Grid.SetRowSpan(element, rowSpan);
            Grid.SetColumnSpan(element, columnSpan);
        }
        public enum NowPlayingLayoutMode
        {
            Compact,
            Portrait,
            Balanced,
            Landscape,
        }
    }
}

