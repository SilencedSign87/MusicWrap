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
        private const double SidebarExpandedWidth = 307;
        private const double ContentEnterOffset = 12d;
        private static readonly TimeSpan SidebarDuration = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan ContentEnterDuration = TimeSpan.FromMilliseconds(300);

        private readonly MainPlayerViewModel _viewmodel;
        private readonly IServiceProvider _serviceProvider;
        private LibraryPage? _cachedLibraryPage;
        private int _currentIndex = -1;

        public MainPlayer(PlayerPage playerPage, MainPlayerViewModel viewmodel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
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
            var target = isSidePanelVisible ? SidebarExpandedWidth : 0d;

            if (!animate)
            {
                SidebarHost.BeginAnimation(FrameworkElement.WidthProperty, null);
                SidebarHost.Width = target;
                return;
            }

            var from = SidebarHost.ActualWidth;

            SidebarHost.BeginAnimation(FrameworkElement.WidthProperty, null);
            SidebarHost.Width = target;

            SidebarHost.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(from, target, SidebarDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });

            SidebarHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(isSidePanelVisible ? 1d : 0d, TimeSpan.FromMilliseconds(140)));
        }

        private void AnimateContent(int index, bool animate = true)
        {
            if (index == _currentIndex) return;
            _currentIndex = index;
            SwapContent(ResolvePage(index));
            if (!animate) return;

            ContentOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(ContentEnterOffset, 0d, ContentEnterDuration)
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

