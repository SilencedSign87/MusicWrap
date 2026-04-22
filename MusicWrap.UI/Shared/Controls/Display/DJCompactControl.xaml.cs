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
