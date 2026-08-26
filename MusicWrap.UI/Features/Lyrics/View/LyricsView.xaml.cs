using MusicWrap.Core.Threading;
using MusicWrap.UI.Features.Lyrics.Viewmodel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicWrap.UI.Features.Lyrics.View
{
    public partial class LyricsView : UserControl
    {
        private bool _followLyrics = true;
        private readonly LyricsViewModel _viewModel;
        private readonly IUIDispatcher _dispatcher;
        public LyricsView(LyricsViewModel viewModel, IUIDispatcher dispatcher)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _dispatcher = dispatcher;
            DataContext = viewModel;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

            _viewModel.PropertyChanged += OnViewmodelPropertyChanged;

            UpdateSpacers();
            
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(UpdateLineOpacities));

            Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(CenterActiveLine));
        }


        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.PropertyChanged -=
                OnViewmodelPropertyChanged;
        }
        private void OnViewmodelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LyricsViewModel.ActiveIndex))
            {
                return;
            }

            UpdateLineOpacities();

            if (!_followLyrics) return;

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(CenterActiveLine));
        }
        private void UpdateSpacers()
        {
            var height =
            LyricsScrollViewer.ViewportHeight / 2.0;

            if (height <= 0)
                return;

            TopSpacer.Height = height;
            BottomSpacer.Height = height;
        }
        private void LyricsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            DisableFollow();
        }

        private void LyricsScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            DisableFollow();
        }

        private void LyricsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateSpacers();
        }
        private void DisableFollow()
        {
            if (!_followLyrics) return;
            _followLyrics = false;
        }

        private void AppButton_Click(object sender, RoutedEventArgs e)
        {
            _followLyrics = true;

            CenterActiveLine();
        }
        private void CenterActiveLine()
        {
            if (!_followLyrics)
                return;

            if (_viewModel == null)
                return;

            var index = _viewModel.ActiveIndex;

            if (index < 0)
                return;

            if (index >= LyricsItems.Items.Count)
                return;

            var container =
                LyricsItems.ItemContainerGenerator
                    .ContainerFromIndex(index)
                    as FrameworkElement;

            if (container == null)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(CenterActiveLine));

                return;
            }

            if (container.ActualHeight <= 0)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(CenterActiveLine));

                return;
            }

            var transform =
                container.TransformToAncestor(
                    LyricsScrollViewer);

            var position =
                transform.Transform(new Point(0, 0));

            var itemCenter =
                position.Y +
                container.ActualHeight / 2.0;

            var viewportCenter =
                LyricsScrollViewer.ViewportHeight / 2.0;

            var targetOffset =
                LyricsScrollViewer.VerticalOffset +
                itemCenter -
                viewportCenter;

            var maxOffset =
                LyricsScrollViewer.ScrollableHeight;

            targetOffset =
                Math.Clamp(
                    targetOffset,
                    0,
                    maxOffset);

            LyricsScrollViewer.ScrollToVerticalOffset(
                targetOffset);
        }

        private void UpdateLineOpacities()
        {
            var activeIdx = _viewModel?.ActiveIndex ?? -1;
            var count = LyricsItems.Items.Count;

            for (int i = 0; i < count; i++)
            {
                if (LyricsItems.ItemContainerGenerator
                    .ContainerFromIndex(i) is FrameworkElement container)
                {
                    container.Opacity = i == activeIdx ? 1.0 : 0.3;
                }
            }
        }
    }
}
