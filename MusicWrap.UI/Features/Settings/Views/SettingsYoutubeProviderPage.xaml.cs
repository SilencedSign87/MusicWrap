using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Settings.ViewModels;
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


