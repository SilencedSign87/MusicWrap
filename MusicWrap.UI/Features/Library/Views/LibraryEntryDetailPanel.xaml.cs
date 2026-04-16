using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Features.Library.ViewModels;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        public static readonly DependencyProperty LibraryViewModelProperty =
            DependencyProperty.Register(
                nameof(LibraryViewModel),
                typeof(LibraryViewModel),
                typeof(LibraryEntryDetailPanel),
                new PropertyMetadata(null, OnLibraryViewModelChanged));

        public LibraryViewModel? LibraryViewModel
        {
            get => (LibraryViewModel?)GetValue(LibraryViewModelProperty);
            set => SetValue(LibraryViewModelProperty, value);
        }
        #endregion
        private static void OnSelectedEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LibraryEntryDetailPanel panel)
                return;

            panel._viewModel.LoadEntry(e.NewValue as LibraryEntry);
        }

        private static void OnLibraryViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LibraryEntryDetailPanel panel)
            {
                return;
            }

            panel._viewModel.AttachLibraryViewModel(e.NewValue as LibraryViewModel);
        }
    }
}
