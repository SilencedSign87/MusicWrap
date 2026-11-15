using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Shared.Services;
using System.Windows;
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

        #region Dependency Properties
        public bool ShowToolbar
        {
            get { return (bool)GetValue(ShowToolbarProperty); }
            set { SetValue(ShowToolbarProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ShowToolbar.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ShowToolbarProperty =
            DependencyProperty.Register(nameof(ShowToolbar), typeof(bool), typeof(QueueListPage), new PropertyMetadata(true));

        #endregion

    }
}




