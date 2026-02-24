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

namespace MusicWrap.UI.Pages.MainWindow
{
    /// <summary>
    /// Lógica de interacción para TrackInformationPage.xaml
    /// </summary>
    public partial class TrackInformationPage : UserControl
    {
        private PlayerViewModel _vm;
        public TrackInformationPage()
        {
            InitializeComponent();
            _vm = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _vm;
        }

        private void OpenArtwork(object sender, RoutedEventArgs e)
        {
            _vm.OpenArtworkOnDefaultApp();
        }
    }
}
