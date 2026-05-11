using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System.Windows.Controls;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para DJCompactControl.xaml
    /// </summary>
    public partial class DJCompactControl : UserControl
    {
        public DJCompactControl()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<DJControlViewModel>();
        }
    }
}
