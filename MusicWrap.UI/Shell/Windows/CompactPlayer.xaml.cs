using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Shell.Windows
{
    public partial class CompactPlayer : UserControl
    {
        private readonly WindowManagerService _windowManager;
        private bool _isQueueOpen = false;

        private const int _playerWidth = 250;
        private const int _compactHeight = 320;
        private const int _expandedHeight = 700;

        public CompactPlayer(PlayerViewModel playervm, QueueListPage queuepage, WindowManagerService wm)
        {
            _windowManager = wm;

            InitializeComponent();
            InitializeWindow();

            DataContext = playervm;

            QueueTab.Content = queuepage;
        }

        private void InitializeWindow()
        {
            Width = _playerWidth;
            Height = _compactHeight;
        }

        private void HandleOpenMainPlayer(object sender, RoutedEventArgs e)
        {
            _windowManager.SwitchToMainPlayer();
        }

        private void HandleOpenQueue(object sender, RoutedEventArgs e)
        {
            var window = _windowManager.ShellWindow;
            if (window is null)
                return;

            _isQueueOpen = !_isQueueOpen;

            if (_isQueueOpen)
            {
                QueuePanel.Visibility = Visibility.Visible;
                QueuePanel.Height = _expandedHeight - _compactHeight;
                PanelIcon.Text = "\xE70E";
                window.Height = _expandedHeight;
                MusicWrapCompactWindow.Height = _expandedHeight;
            }
            else
            {
                QueuePanel.Visibility = Visibility.Collapsed;
                PanelIcon.Text = "\xE70D";
                window.Height = _compactHeight;
                MusicWrapCompactWindow.Height = _compactHeight;
            }
        }

        private void HandleToggleAllwayOnTop(object sender, RoutedEventArgs e)
        {
            var window = _windowManager.ShellWindow;
            if (window is null)
                return;

            window.Topmost = !window.Topmost;

            PinIconFont.Text = window.Topmost
                ? "\ue77a"
                : "\ue718";
        }

        private void HandleCloseApp(object sender, RoutedEventArgs e)
        {
            _windowManager.ShellWindow?.Close();
        }

        private void MinimizeClick(object sender, RoutedEventArgs e)
        {
            var window = _windowManager.ShellWindow;
            if (window is not null)
                window.WindowState = WindowState.Minimized;
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumePopup.IsOpen = true;
        }
 
    }
}


