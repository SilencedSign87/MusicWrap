using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Settings.ViewModels;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Settings.Views
{
    /// <summary>
    /// Lógica de interacción para SettingsYoutubeProviderPage.xaml
    /// </summary>
    public partial class SettingsYoutubeProviderPage : UserControl
    {
        public SettingsYoutubeProviderPage()
        {
            InitializeComponent();

            DataContext = App.Services.GetRequiredService<SettingsYoutubeViewModel>();
        }
    }
}


