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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Pages
{
    /// <summary>
    /// Lógica de interacción para PlayerPage.xaml
    /// </summary>
    public partial class PlayerPage : UserControl
    {
        public PlayerPage()
        {
            InitializeComponent();

            DataContext = App.Services.GetRequiredService<PlayerViewModel>();
        }

        private void DeviceBtn_Click(object sender, RoutedEventArgs e)
        {
            DevicePopup.IsOpen = true;
        }

        private void QueueBtn_Click(object sender, RoutedEventArgs e)
        {
            QueuePopup.IsOpen = true;
        }

        private void PlusButton_Click(object sender, RoutedEventArgs e)
        {
            PlusPopup.IsOpen = true;
        }
    }
}
