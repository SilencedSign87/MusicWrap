using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Pages.MainWindow;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Dictionary<int, UserControl> _pageCache = [];
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

        public MainWindow()
        {
            InitializeComponent();

            StateChanged += MainWindow_StateChanged;

            _player = App.Services.GetRequiredService<IMusicPlayerService>();
            _player.PlaybackStateChanged += _player_PlaybackStateChanged;

            UpdateBackdrop();
            NavigateToTab(0);
            PlayPauseButton.ImageSource = _playIcon;

            TrackExpander.Expanded += TrackExpander_Expanded;
            DeviceExpander.Expanded += DeviceExpander_Expanded;
        }

        private void TrackExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (DeviceExpander.IsExpanded)
                DeviceExpander.IsExpanded = false;
        }

        private void DeviceExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (TrackExpander.IsExpanded)
                TrackExpander.IsExpanded = false;
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
                //RestoreIconFont.Text = "\xE923";
                //RestoreButton.ToolTip = "Restore";
                BorderWindow.Padding = new Thickness(8);
            }
            else
            {
                //RestoreIconFont.Text = "\xE922";
                //RestoreButton.ToolTip = "Maximize";
                BorderWindow.Padding = new Thickness(0);
            }
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
            if (!_pageCache.TryGetValue(index, out var page))
            {
                page = index switch
                {
                    0 => new LibraryPage(),
                    1 => new PlaylistPage(),
                    2 => new FavoritesPage(),
                    _ => new LibraryPage()
                };
                _pageCache[index] = page;
            }

            MainFrame.Content = page;

            TrimPageCache(index);
        }

        private void TrimPageCache(int currentIndex)
        {
            const int libraryIndex = 0;
            var keysToRemove = _pageCache.Keys
                .Where(k=>k != libraryIndex && k != currentIndex)
                .ToList();

            foreach (var key in keysToRemove) {
                if (!_pageCache.TryGetValue(key, out var cachedPage))
                    continue;

                if (ReferenceEquals(MainFrame.Content, cachedPage))
                    MainFrame.Content = null;

                if (cachedPage is IDisposable disposable)
                    disposable.Dispose();

                _pageCache.Remove(key);
            }
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
    }
}
