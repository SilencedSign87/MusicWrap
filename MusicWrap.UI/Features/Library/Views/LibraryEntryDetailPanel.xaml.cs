using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.UI.Features.Library.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MusicWrap.UI.Features.Library.Views
{
    /// <summary>
    /// Lógica de interacción para LibraryEntryDetailPanel.xaml
    /// </summary>
    public partial class LibraryEntryDetailPanel : UserControl, IDisposable
    {
        private bool _isDisposed = false;
        private readonly LibraryEntryDetailPanelViewModel _viewModel;
        public LibraryEntryDetailPanel()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<LibraryEntryDetailPanelViewModel>();
            _viewModel.PropertyChanged += _viewModel_PropertyChanged;
            Unloaded += LibraryEntryDetailPanel_Unloaded;
            DataContext = _viewModel;
        }

        private void LibraryEntryDetailPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void _viewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibraryEntryDetailPanelViewModel.SelectedTab) 
                || e.PropertyName == nameof(LibraryEntryDetailPanelViewModel.CurrentEntry))
            {
                RebuildContent();
            }
        }
        #region Dependency Properties
        public static readonly DependencyProperty SelectedEntryProperty =
            DependencyProperty.Register(
                nameof(SelectedEntry),
                typeof(LibraryEntry),
                typeof(LibraryEntryDetailPanel),
                new PropertyMetadata(null, OnSelectedEntryChanged));
        public LibraryEntry? SelectedEntry
        {
            get => (LibraryEntry?)GetValue(SelectedEntryProperty);
            set => SetValue(SelectedEntryProperty, value);
        }
        #endregion
        private static void OnSelectedEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LibraryEntryDetailPanel panel)
                return;

            panel._viewModel.LoadEntry(e.NewValue as LibraryEntry);
        }
        private void RebuildContent()
        {
            if (_viewModel.SelectedTab is null) return;

            foreach (var child in ContentGrid.Children)
            {
                if (child is FrameworkElement fe && fe.DataContext is IDisposable disposable)
                    disposable.Dispose();
            }

            ContentGrid.Children.Clear();

            switch (_viewModel.SelectedTab?.Key)
            {
                case LibraryDetailTabKey.Albums:
                    {

                        var view = new LibraryEntryAlbumsView();
                        view.SetBinding(FrameworkElement.DataContextProperty,
                            new Binding(nameof(LibraryEntryDetailPanelViewModel.AlbumEntriesViewModel))
                            { Source = _viewModel });
                        ContentGrid.Children.Add(view);
                        break;
                    }
                case LibraryDetailTabKey.Tracks:
                    {
                        var view = new LibraryEntryTracksView();
                        view.SetBinding(FrameworkElement.DataContextProperty,
                            new Binding(nameof(LibraryEntryDetailPanelViewModel.TracksViewModel))
                            { Source = _viewModel });
                        ContentGrid.Children.Add(view);
                        break;
                    }
            }

        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _viewModel.PropertyChanged -= _viewModel_PropertyChanged;
            Unloaded -= LibraryEntryDetailPanel_Unloaded;
            foreach (var child in ContentGrid.Children)
            {
                if (child is FrameworkElement fe && fe.DataContext is IDisposable disposable)
                    disposable.Dispose();
            }
            ContentGrid.Children.Clear();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is LibraryDetailTabItem tab)
            {
                _viewModel.SelectTabCommand.Execute(tab);
            }
        }
    }
}
