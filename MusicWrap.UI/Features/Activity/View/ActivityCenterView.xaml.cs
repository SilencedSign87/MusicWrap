using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Activity.Viewmodel;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Activity.View
{
    /// <summary>
    /// Lógica de interacción para ActivityCenterView.xaml
    /// </summary>
    public partial class ActivityCenterView : UserControl
    {
        public ActivityCenterView()
        {
            InitializeComponent();
            var viewModel = App.Services.GetRequiredService<ActivityCenterViewModel>();
            DataContext = viewModel;
        }
    }
}
