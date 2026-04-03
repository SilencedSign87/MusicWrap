using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
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
using System.Windows.Shapes;

namespace MusicWrap.UI.Windows
{
    /// <summary>
    /// Lógica de interacción para TrayFlyoutWindow.xaml
    /// </summary>
    public partial class TrayFlyoutWindow : Window
    {
        private PlayerViewModel _viewmodel;
        public TrayFlyoutWindow()
        {
            InitializeComponent();
            _viewmodel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _viewmodel;
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            this.Hide();
        }

        private void OpenMainWindow(object sender, RoutedEventArgs e)
        {
            Hide();
            App.ShowOrRestoreCurrentWindow();
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumePopup.IsOpen = true;
        }
    }
}
