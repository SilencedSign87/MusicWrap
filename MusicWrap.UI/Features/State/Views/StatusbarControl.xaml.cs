using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.State.ViewModels;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.State
{
    public partial class StatusbarControl : UserControl
    {
        private readonly StatusbarViewModel _viewModel;
        public StatusbarControl()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<StatusbarViewModel>();
            DataContext = _viewModel;
        }
    }
}
