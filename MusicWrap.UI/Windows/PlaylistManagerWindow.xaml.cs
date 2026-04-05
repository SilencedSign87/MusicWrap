using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels.Playlist;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Lógica de interacción para PlaylistManagerWindow.xaml
    /// </summary>
    public partial class PlaylistManagerWindow : Window
    {
        private readonly PlaylistManagerViewModel _viewModel;
        public PlaylistManagerWindow()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<PlaylistManagerViewModel>();
            DataContext = _viewModel;
        }
    }
}
