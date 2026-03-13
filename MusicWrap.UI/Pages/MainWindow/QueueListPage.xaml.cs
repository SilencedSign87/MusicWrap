using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels.Library;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Lógica de interacción para QueueListPage.xaml
    /// </summary>
    public partial class QueueListPage : UserControl
    {
        public readonly QueueViewModel _vm;
        public QueueListPage()
        {
            InitializeComponent();

            _vm = App.Services.GetRequiredService<QueueViewModel>();
            DataContext = _vm;

        }

        private void ListDoubleClickPlay(object sender, MouseButtonEventArgs e)
        {
            var items = QueueListView.SelectedItems;

            var selectedTrackIds = items
                .OfType<QueueData>()
                .Select(x => x.TrackId)
                .ToList();
            if (selectedTrackIds.Count == 1)
            {
                _vm.PlayTrack(selectedTrackIds.First());
            }
            else if (selectedTrackIds.Count > 1)
            {
                _vm.PlayTracks(selectedTrackIds);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RestoreSelectionByTracksId(selectedTrackIds);
                }));
            }

        }

        private void PlayNext(object sender, RoutedEventArgs e)
        {
            var items = QueueListView.SelectedItems;
            var selectedTrackIds = items
                .OfType<QueueData>()
                .Select(x => x.TrackId)
                .ToList();
            if (selectedTrackIds.Count == 0) return;

            _vm.SetSelectedTracksToPlayNext(selectedTrackIds);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RestoreSelectionByTracksId(selectedTrackIds);
            }));
        }

        private void RemoveFromQueue(object sender, RoutedEventArgs e)
        {
            var items = QueueListView.SelectedItems;
            var selectedTrackIds = items
                .OfType<QueueData>()
                .Select(x => x.TrackId)
                .ToList();
            if (selectedTrackIds.Count != 0)
            {
                _vm.RemoveSelectedTracksFromQueue(selectedTrackIds);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RestoreSelectionByTracksId(selectedTrackIds);
                }));
            }
        }

        private void PlaySelectedNow(object sender, RoutedEventArgs e)
        {
            var items = QueueListView.SelectedItems;
            var selectedTrackIds = items
               .OfType<QueueData>()
               .Select(x => x.TrackId)
               .ToList();
            if (selectedTrackIds.Count == 0) return;

            _vm.PlayTracks(selectedTrackIds);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RestoreSelectionByTracksId(selectedTrackIds);
            }));
        }
        private void RestoreSelectionByTracksId(List<int> selectedTrackIds)
        {
            if (selectedTrackIds.Count == 0) return;

            var remaining = selectedTrackIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

            QueueListView.SelectedItems.Clear();

            foreach (var item in QueueListView.Items.OfType<QueueData>())
            {
                if (!remaining.TryGetValue(item.TrackId, out int count) || count <= 0)
                    continue;

                QueueListView.SelectedItems.Add(item);
                if (count == 1) remaining.Remove(item.TrackId);
                else remaining[item.TrackId] = count - 1;
            }
        }
    }
}
