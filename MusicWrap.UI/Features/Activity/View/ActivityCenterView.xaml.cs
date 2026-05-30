using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Activity.Viewmodel;
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
