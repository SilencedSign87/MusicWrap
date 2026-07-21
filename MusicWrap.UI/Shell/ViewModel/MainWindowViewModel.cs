using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Library.Views;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Playlist.Views;
using MusicWrap.UI.Features.Providers.Views;
using MusicWrap.UI.Shared.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MusicWrap.UI.Shell.ViewModel
{
    public partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        private readonly IMusicPlayerService _playerService;
        private readonly IServiceProvider _serviceProvider;
        private readonly WindowManager _windowManager;
        private readonly ILogger _logger;
        private readonly UserSettings _userSettings;

        [ObservableProperty]
        private int selectedTabIndex;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SidebarToggleIcon))]
        [NotifyPropertyChangedFor(nameof(SidebarWidth))]
        [NotifyPropertyChangedFor(nameof(SidebarBorderThickness))]
        [NotifyPropertyChangedFor(nameof(SidebarTooltip))]
        private bool isSidePanelVisible;

        [ObservableProperty] public UserControl? currentControl;

        public BitmapImage PlayPauseIcon => _playerService.IsPlaying
            ? LoadBitmapFromResource("pack://application:,,,/Resources/Icons/PauseIcon.png")
            : LoadBitmapFromResource("pack://application:,,,/Resources/Icons/PlayIcon.png");

        public string SidebarToggleIcon
            => IsSidePanelVisible ? "\xE89F" : "\xE8A0";
        public GridLength SidebarWidth
            => IsSidePanelVisible ? new GridLength(300) : new GridLength(0);
        public Thickness SidebarBorderThickness
            => IsSidePanelVisible ? new Thickness(4) : new Thickness(0);
        public string SidebarTooltip
            => IsSidePanelVisible ? "Hide Sidebar" : "Show Sidebar";

        private CancellationTokenSource? _loadingCts;
        private bool _disposed = false;

        public MainWindowViewModel(IMusicPlayerService playerService, IServiceProvider serviceProvider, ILogger<MainWindowViewModel> logger, WindowManager manager, UserSettings userSettings)
        {
            _playerService = playerService;
            _serviceProvider = serviceProvider;
            _windowManager = manager;
            _logger = logger;
            _userSettings = userSettings;

            _playerService.PlaybackStateChanged += _playerService_PlaybackStateChanged;

            IsSidePanelVisible = _userSettings.IsSidebarOpen;

            SelectedTabIndex = _userSettings.MainWindowTab;
            _ = NavigateAsync(_userSettings.MainWindowTab);
        }

        private void _playerService_PlaybackStateChanged(object? sender, ManagedBass.PlaybackState e)
        {
            OnPropertyChanged(nameof(PlayPauseIcon));
        }

        #region Relay Commands
        [RelayCommand]
        private void PlayPause()
        {
            if ( _playerService.IsPlaying)
            {
                _playerService.Pause();
            }else
            {
                _playerService.Play();
            }
        }
        [RelayCommand]
        private void Previous()
        {
            _playerService.Previous();
        }
        [RelayCommand]
        private void Next()
        {
            _playerService.Next();
        }
        [RelayCommand]
        private void openSettings()
        {
            _windowManager.LaunchSettingsWindow();

        }
        [RelayCommand]
        private void showMiniplayer()
        {
            _windowManager.SwitchToCompactPlayer();
        }
        [RelayCommand]
        private void showFullScreen()
        {
            _windowManager.SwitchToFullScreenPlayer();
        }
        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidePanelVisible = !IsSidePanelVisible;
            _userSettings.IsSidebarOpen = IsSidePanelVisible;
        }
        #endregion
        #region Partial Functions
        partial void OnSelectedTabIndexChanged(int value) => _ = NavigateAsync(value);
        #endregion
        #region Internal
        private async Task NavigateAsync(int index)
        {
            var oldCts = _loadingCts;
            var cts = new CancellationTokenSource();
            _loadingCts = cts;

            oldCts?.Cancel();
            oldCts?.Dispose();

            var token = cts.Token;
            try
            {

                await Dispatcher.Yield(DispatcherPriority.Background);

                if (token.IsCancellationRequested) return;

                UserControl page = index switch
                {
                    0 => _serviceProvider.GetRequiredService<LibraryPage>(),
                    1 => _serviceProvider.GetRequiredService<PlaylistPage>(),
                    2 => _serviceProvider.GetRequiredService<ServicesPage>(),
                    3 => _serviceProvider.GetRequiredService<NowPlayingPage>(),
                    _ => _serviceProvider.GetRequiredService<LibraryPage>()
                };
                _userSettings.MainWindowTab = index;

                if (token.IsCancellationRequested) return;

                if (CurrentControl is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                CurrentControl = page;

            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to page index {Index}", index);
            }
        }

        private static BitmapImage LoadBitmapFromResource(string uri)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(uri);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        #endregion
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (CurrentControl is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
