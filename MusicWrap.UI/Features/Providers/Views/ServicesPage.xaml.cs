using MusicWrap.UI.Features.Providers.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Providers.Views
{
    /// <summary>
    /// Lógica de interacción para ServicesPage.xaml
    /// </summary>
    public partial class ServicesPage : UserControl
    {
        public ServicesPage(ServicePageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}



