using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Library.ViewModels;
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
    }
}
