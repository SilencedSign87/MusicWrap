using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
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

namespace MusicWrap.UI.Pages.MainWindow
{
    /// <summary>
    /// Lógica de interacción para DevicePage.xaml
    /// </summary>
    public partial class DevicePage : UserControl
    {
        public DevicePage()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<DeviceViewModel>();
        }
    }
}
