using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.UI.ViewModels.Library;
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
    /// Lógica de interacción para AlbumTracksPage.xaml
    /// </summary>
    public partial class AlbumTracksPage : UserControl
    {
        private readonly IMusicPlayerService _musicPlayerService;
        public AlbumTracksPage()
        {
            InitializeComponent();
            _musicPlayerService = App.Services.GetRequiredService<IMusicPlayerService>();
            //DataContext = App.Services.GetRequiredService<AlbumTracksViewModel>();
        }

        private void StartPlayingFromTrack(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is int trackId && DataContext is AlbumTracksViewModel vm)
            {
                vm.PlayTrackCommand.Execute(trackId);
            }
        }
    }
}
