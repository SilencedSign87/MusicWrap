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
        public SidebarControl()
        {
            InitializeComponent();
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
                    ContentContainer.Children.Add(new QueueListPage() { VerticalAlignment = VerticalAlignment.Stretch });
                    break;
                case 1:
                    ContentContainer.Children.Add(new TrackInformationPage() { VerticalAlignment = VerticalAlignment.Stretch });
                    break;
                case 2:
                    break;
            }
        }
    }
}
