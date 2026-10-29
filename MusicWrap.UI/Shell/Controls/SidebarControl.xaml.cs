using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Controls;
using MusicWrap.UI.Features.Lyrics.View;
using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Features.Playback.Views;
using System;
using System.Collections.Generic;
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

namespace MusicWrap.UI.Shell.Controls
{
    /// <summary>
    /// Lógica de interacción para SidebarControl.xaml
    /// </summary>
    public partial class SidebarControl : UserControl
    {
        private new int TabIndex = 0;
        private UIElement? cachedLyricsPage;
        public SidebarControl()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<NowPlayingViewModel>();
            LoadIndex(0);
        }

        private void ShellTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tabControl)
            {
                if (tabControl.SelectedIndex != TabIndex)
                {
                    TabIndex = tabControl.SelectedIndex;
                    LoadIndex(TabIndex);
                }
            }
        }

        private void LoadIndex(int index)
        {
            var children = ContentContainer.Children;

            foreach (var item in children)
            {
                if (item is IDisposable disposable)
                    disposable.Dispose();
            }

            ContentContainer.Children.Clear();
            switch (index)
            {
                case 0:
                default:
                    var queuepage = App.Services.GetRequiredService<QueueListPage>();
                    queuepage.VerticalAlignment = VerticalAlignment.Stretch;
                    ContentContainer.Children.Add(queuepage);
                    break;
                case 1:
                    cachedLyricsPage ??= BuildLyricsSection();
                    ContentContainer.Children.Add(cachedLyricsPage!);
                    break;
                case 2:
                    var trackInfoPage = App.Services.GetRequiredService<TrackInformationPage>();
                    trackInfoPage.VerticalAlignment = VerticalAlignment.Stretch;
                    ContentContainer.Children.Add(trackInfoPage);
                    break;
            }
        }

        private UIElement BuildLyricsSection()
        {
            return new LyricsView
            {
                FontSize = 18,
                AllowScroll = true,
                AllowSeek = true,
                ShowToolbar = true,
                LyricsAligment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                LyricsPadding = new Thickness(12),
            };
        }
    }
}
