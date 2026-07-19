using MusicWrap.Core.Services.Library;
using MusicWrap.Mobile.Controls;
using MusicWrap.Mobile.Features.Home.views;
using MusicWrap.Mobile.Features.Library.views;
using MusicWrap.Mobile.Features.Playlists.views;
using MusicWrap.Mobile.Features.Plugins.views;
using MusicWrap.Mobile.Features.Settings.views;
using System.Diagnostics;
using System.Windows.Input;

namespace MusicWrap.Mobile
{
    public partial class MainHostTab : ContentPage
    {
        private readonly Dictionary<int, View> _pages = new();
        private readonly List<AppTabButton> _tabButtons = new();
        private int _currentIndex;

        public ICommand SelectTabCommand { get; }
        public MainHostTab(IServiceProvider serviceProvider)
        {
            SelectTabCommand = new Command<object>(param => SelectTab(Convert.ToInt32(param)));

            InitializeComponent();

            _tabButtons = [TabHome, TabLibrary, TabPlaylists, TabPlugins, TabSettings];
            _pages[0] = serviceProvider.GetRequiredService<HomePage>();
            _pages[1] = serviceProvider.GetRequiredService<LibraryPage>();
            _pages[2] = serviceProvider.GetRequiredService<PlaylistsPage>();
            _pages[3] = serviceProvider.GetRequiredService<PluginsPage>();
            _pages[4] = serviceProvider.GetRequiredService<SettingsPage>();
            
            SelectTab(0);
        }

        private void SelectTab(int index)
        {
            if (_currentIndex == index) return;
            _tabButtons[_currentIndex].IsSelected = false;
            _currentIndex = index;
            _tabButtons[_currentIndex].IsSelected = true;
            if (_pages.TryGetValue(index, out var page))
            {
                ContentArea.Content = page;
            }
        }
    }
}
