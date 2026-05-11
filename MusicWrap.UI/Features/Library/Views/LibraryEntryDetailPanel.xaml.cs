using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.UI.Features.Library.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Library.Views
{
    /// <summary>
    /// Lógica de interacción para LibraryEntryDetailPanel.xaml
    /// </summary>
    public partial class LibraryEntryDetailPanel : UserControl
    {
        private readonly LibraryEntryDetailPanelViewModel _viewModel;
        public LibraryEntryDetailPanel()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<LibraryEntryDetailPanelViewModel>();
            DataContext = _viewModel;
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
    }
}
