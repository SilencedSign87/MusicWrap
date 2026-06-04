using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TagLib.Matroska;

namespace MusicWrap.UI.Features.Playback.Views
{
    /// <summary>
    /// Lógica de interacción para PlayerPage.xaml
    /// </summary>
    public partial class PlayerPage : UserControl
    {
        private readonly PlayerViewModel _viewModel;
        public PlayerPage(PlayerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

        }
    }
}

