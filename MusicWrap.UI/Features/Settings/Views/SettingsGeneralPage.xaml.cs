using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Settings.ViewModels;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Settings.Views
{
    public partial class SettingsGeneralPage : UserControl
    {
        private readonly SettingsGeneralViewModel _viewModel;
        public SettingsGeneralPage()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<SettingsGeneralViewModel>();
            DataContext = _viewModel;
        }
    }
}


