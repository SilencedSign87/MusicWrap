using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Playback.ViewModels;
using System.Windows;
using System.Windows.Controls;
using MusicWrap.UI.Shared.Services;

namespace MusicWrap.UI.Features.Playback.Views
{
    public partial class QueueListPage : UserControl
    {

        public QueueListPage(QueueViewModel queueViewModel, ContextMenuFactory menuFactory)
        {
            InitializeComponent();

            DataContext = queueViewModel;

            QueueTracksView.ContextMenu = menuFactory.Create(
                QueueTracksView,
                ContextMenuType.Queue,
                extras: [
                    new ExtraMenuItem("Remove from queue", "\uE738", queueViewModel.RemoveFromQueueCommand)
                    ]
                );
        }
    }
}




