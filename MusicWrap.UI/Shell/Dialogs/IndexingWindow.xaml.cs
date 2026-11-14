using MusicWrap.UI.Models;
using MusicWrap.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Shell.Dialogs;

/// <summary>
/// Interaction logic for IndexingWindow.xaml
/// </summary>
public partial class IndexingWindow : Window
{
    private readonly IndexingViewModel _viewModel;

    public IndexingWindow(IndexingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Closed += IndexingWindow_Closed;
    }

    private void IndexingWindow_Closed(object? sender, EventArgs e)
    {
        var viewModel = DataContext as IndexingViewModel;

        viewModel?.ClearAllStagedTracksCommand.Execute(null);
    }

    private void StagedTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        _viewModel.UpdateSelectedTracks(listBox.SelectedItems.OfType<MusicWrap.UI.Models.StagedTrackNode>());
    }

    private void RemoveSelectedTracks_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.RemoveSelectedStagedTracksCommand.CanExecute(null))
        {
            _viewModel.RemoveSelectedStagedTracksCommand.Execute(null);
        }
    }

    private void RemoveTrack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        if (menuItem.DataContext is not StagedTrackNode track)
        {
            return;
        }

        if (_viewModel.RemoveStagedTrackCommand.CanExecute(track))
        {
            _viewModel.RemoveStagedTrackCommand.Execute(track);
        }
    }
}

