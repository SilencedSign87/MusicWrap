using Hardcodet.Wpf.TaskbarNotification;
using Jot;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Favorites.Views;
using MusicWrap.UI.Features.Library.Components;
using MusicWrap.UI.Features.Library.Views;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Playlist.Views;
using MusicWrap.UI.Features.Providers.Views;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IMusicPlayerService _player;
        private readonly BitmapImage _playIcon = LoadBitmapFromResource("pack://application:,,,/Resources/Icons/PlayIcon.png");
        private readonly BitmapImage _pauseIcon = LoadBitmapFromResource("pack://application:,,,/Resources/Icons/PauseIcon.png");

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

        public MainWindow(IMusicPlayerService playerService, Tracker tracker)
        {
            InitializeComponent();
            _player = playerService;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;

            _player.PlaybackStateChanged += _player_PlaybackStateChanged;

            UpdateBackdrop();
            NavigateToTab(0);
            PlayPauseButton.ImageSource = _playIcon;

            tracker.Track(this);
        }


        private void _player_PlaybackStateChanged(object? sender, PlaybackState e)
        {
            // update play/pause button in taskbar, always on UI thread
            Dispatcher.Invoke(() =>
            {
                if (e == PlaybackState.Playing)
                {
                    PlayPauseButton.ImageSource = _pauseIcon;
                }
                else
                {
                    PlayPauseButton.ImageSource = _playIcon;
                }
            });
        }

        private void UpdateBackdrop()
        {
            if (!BackdropHelper.IsBackdropSupported() && !BackdropHelper.IsBackdropSupported())
            {
                this.SetResourceReference(BackgroundProperty, "WindowBackground");
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                BorderWindow.Padding = new Thickness(8);
            }
            else
            {
                BorderWindow.Padding = new Thickness(0);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (App.IsShuttingDown || App.IsWindowTransitioning)
            {
                return;
            }

            if (App.ShouldKeepAppInTray())
            {
                App.Services.GetService<ITrayService>()?.HideFlyout();
                return;
            }

            App.Services.GetService<IMusicPlayerService>()?.FlushPlaybackState();
            App.RequestShutdown();
        }
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            StateChanged -= MainWindow_StateChanged;
            Closing -= MainWindow_Closing;
            Closed -= MainWindow_Closed;

            _player.PlaybackStateChanged -= _player_PlaybackStateChanged;
            MainFrame.Content = null;
        }
        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RestoreButtonClick(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
            }

        }

        private void MinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void HandleOpenMiniPlayer(object sender, RoutedEventArgs e)
        {
            App.ShowCompactPlayer();
        }

        private void HandleOpenSettings(object sender, RoutedEventArgs e)
        {
            var existingWindow = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
            if (existingWindow is not null)
            {
                existingWindow.Activate();
                existingWindow.Focus();
            }
            else
            {
                var settingsWindow = App.Services.GetRequiredService<SettingsWindow>();
                if (settingsWindow is not null)
                {
                    WindowHelper.LauchFromParent(this, settingsWindow, false);
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tabControl)
            {
                NavigateToTab(tabControl.SelectedIndex);
            }
        }

        private void NavigateToTab(int index)
        {
            object page = index switch
            {
                0 => new LibraryPage(),
                1 => new PlaylistPage(),
                2 => new FavoritesPage(),
                3 => new ServicesPage(),
                4 => new NowPlayingPage(),
                _ => new LibraryPage()
            };

            if (MainFrame.Content is IDisposable disposable)
            {
                disposable.Dispose();
            }

            MainFrame.Content = page;
        }

        private void ThumbButtonInfo_Previous(object sender, EventArgs e)
        {
            _player.Previous();
        }

        private void ThumbButtonInfo_PlayPause(object sender, EventArgs e)
        {
            if (_player.IsPlaying)
            {
                _player.Pause();
            }
            else
            {
                _player.Play();
            }
        }

        private void ThumbButtonInfo_Next(object sender, EventArgs e)
        {
            _player.Next();
        }

        private void TrackExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (TrackInformationHost.Content is null)
            {
                TrackInformationHost.Content = App.Services.GetRequiredService<TrackInformationPage>();
            }
        }

        private void TrackExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            TrackInformationHost.Content = null;
        }
    }
}




