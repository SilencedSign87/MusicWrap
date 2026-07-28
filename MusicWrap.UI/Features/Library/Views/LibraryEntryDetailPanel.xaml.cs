using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Features.Library.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
    }
}
