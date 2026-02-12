using Microsoft.Extensions.DependencyInjection;
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
        public MainWindow()
        {
            InitializeComponent();

            StateChanged += MainWindow_StateChanged;

            NavigateToTab(0);
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                RestoreIconFont.Text = "\xE923";
                RestoreButton.ToolTip = "Restore";
                BorderWindow.Padding = new Thickness(8);
            }
            else
            {
                RestoreIconFont.Text = "\xE922";
                RestoreButton.ToolTip = "Maximize";
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
        }
    }
}