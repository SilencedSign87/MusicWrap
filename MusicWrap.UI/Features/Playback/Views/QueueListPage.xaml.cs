using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Playback.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Playback.Views
{
    /// <summary>
    /// Lógica de interacción para QueueListPage.xaml
    /// </summary>
    public partial class QueueListPage : UserControl
    {
        //private readonly TracksContextMenuService _tracksContextMenuService;

        public QueueListPage(QueueViewModel queueViewModel)
        {
            InitializeComponent();
            //_tracksContextMenuService = tracksContextMenuService;
            DataContext = queueViewModel;
        }
    }
}




