using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Library.Views;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Playlist.Views;
using MusicWrap.UI.Features.Providers.Views;
using MusicWrap.UI.Shell.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainPlayer : UserControl
    {
        private const double SidebarExpandedWidth = 307d;
        private const double ContentEnterOffset = 32d;
        private static readonly TimeSpan SidebarDuration = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan ContentEnterDuration = TimeSpan.FromMilliseconds(300);

        private readonly MainPlayerViewModel _viewmodel;
        private readonly IServiceProvider _serviceProvider;
        private readonly TranslateTransform _sidebarSlide = new();
        private LibraryPage? _cachedLibraryPage;
        private int _currentIndex = -1;

        public MainPlayer(PlayerPage playerPage, MainPlayerViewModel viewmodel, IServiceProvider serviceProvider)
        {
            InitializeComponent();

            SidebarContent.RenderTransform = _sidebarSlide;
            _viewmodel = viewmodel;
            _serviceProvider = serviceProvider;
            DataContext = viewmodel;

            PlayerContainer.Children.Add(playerPage);
            _viewmodel.PropertyChanged += OnViewmodelPropertyChanged;

            AnimateSidebar(viewmodel.IsSidePanelVisible, animate: false);
            AnimateContent(viewmodel.SelectedTabIndex, animate: false);
        }

        private void OnViewmodelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainPlayerViewModel.IsSidePanelVisible))
                AnimateSidebar(_viewmodel.IsSidePanelVisible);
            if (e.PropertyName == nameof(MainPlayerViewModel.SelectedTabIndex))
                AnimateContent(_viewmodel.SelectedTabIndex);
        }

        private void AnimateSidebar(bool isSidePanelVisible, bool animate = true)
        {
            var restingX = isSidePanelVisible ? 0d : SidebarExpandedWidth;

            if (!animate)
            {
                _sidebarSlide.BeginAnimation(TranslateTransform.XProperty, null);
                _sidebarSlide.X = restingX;
                SidebarHost.Opacity = isSidePanelVisible ? 1d : 0d;
                SetSidebarColumnWidth(isSidePanelVisible);
                return;
            }

            if (isSidePanelVisible)
            {
                SetSidebarColumnWidth(true);
            }

            var slide = new DoubleAnimation(_sidebarSlide.X, restingX, SidebarDuration)
            {
                EasingFunction = new CubicEase { EasingMode = isSidePanelVisible ? EasingMode.EaseOut : EasingMode.EaseIn},
                FillBehavior = FillBehavior.HoldEnd
            };

            if (!isSidePanelVisible)
            {
                slide.Completed += (s, e) =>
                {
                    if (!_viewmodel.IsSidePanelVisible) // Check again in case it changed during the animation
                        SetSidebarColumnWidth(false);
                };
            }

            _sidebarSlide.BeginAnimation(TranslateTransform.XProperty, slide);
            SidebarHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(isSidePanelVisible ? 1d : 0d, SidebarDuration));
        }

        private void SetSidebarColumnWidth(bool isVisible) => SidebarColumn.Width = isVisible ? new GridLength(SidebarExpandedWidth) : new GridLength(0);

        private void AnimateContent(int index, bool animate = true)
        {
            if (index == _currentIndex) return;
            var direction = index - _currentIndex > 0 ? 1 : -1;
            _currentIndex = index;
            SwapContent(ResolvePage(index));
            if (!animate) return;


            ContentOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(ContentEnterOffset * direction, 0d, ContentEnterDuration)
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            ContentHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0d, 1d, ContentEnterDuration)
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        }
        private void SwapContent(UserControl next)
        {
            var previous = ContentHost.Content;
            ContentHost.Content = next;

            if (previous is IDisposable disposable && !ReferenceEquals(previous, _cachedLibraryPage))
                disposable.Dispose();
        }

        private UserControl ResolvePage(int index) => index switch
        {
            1 => _serviceProvider.GetRequiredService<PlaylistPage>(),
            2 => _serviceProvider.GetRequiredService<ServicesPage>(),
            3 => _serviceProvider.GetRequiredService<NowPlayingPage>(),
            _ => _cachedLibraryPage ??= _serviceProvider.GetRequiredService<LibraryPage>()
        };

        private void CommandBinding_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            SearchInput.FocusInput();

        }
    }
}

