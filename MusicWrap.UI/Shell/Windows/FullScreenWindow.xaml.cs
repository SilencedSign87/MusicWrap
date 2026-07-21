using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.Shell.ViewModel;
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

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Lógica de interacción para FullScreenWindow.xaml
    /// </summary>
    public partial class FullScreenWindow : Window
    {
        public FullScreenWindow(FullscreenWindowViewModel viewmodel)
        {
            InitializeComponent();

            DataContext = viewmodel;
        }

        private void FullScreenWindowOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.F11)
            {
                //handle go to main window
            }

        }
    }
}
