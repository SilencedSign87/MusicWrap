using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Shared.Services;
using System.Windows.Controls;

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
                ContextMenuType.Queue
                );
        }
    }
}




