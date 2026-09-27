using MusicWrap.UI.Features.Library.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryEntryAlbumsView : UserControl
    {
        private const int MinTileWidth = 160;
        private const int Gutter = 0;
        private const int MinColumns = 1;

        public LibraryEntryAlbumsView()
        {
            InitializeComponent();
        }

        private void AlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryEntryAlbumViewModel viewModel)
                return;


            if (sender is Button button && button.DataContext is LibraryViewModel.AlbumData albumData)
                viewModel.ExpandAlbum(albumData.Id);
        }

        private void CloseTracksButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryEntryAlbumViewModel viewModel)
                return;

            viewModel.CollapseAlbum();
        }

        private void AlbumsViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is not LibraryEntryAlbumViewModel viewModel || e.NewSize.Width <= 0)
                return;

            //viewModel.LayoutColumns = CalculateColumns(Math.Max(1, (int)AlbumsViewport.ActualWidth));
            var columns = CalculateColumns(AlbumsViewport.ActualWidth);
            if (columns == viewModel.LayoutColumns)
                return;

            viewModel.LayoutColumns = columns;
        }

        private void AlbumsViewport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is not null)
            {
                listBox.SelectedItem = null;
            }
        }

        private static int CalculateColumns(double width)
        {
            var safeWidth = Math.Max(1d, width);
            var columns = (int)Math.Floor((safeWidth) / (MinTileWidth + Gutter));
            return Math.Max(columns, MinColumns);
        }

    }
}
