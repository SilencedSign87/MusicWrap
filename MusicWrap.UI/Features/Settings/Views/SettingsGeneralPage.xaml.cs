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


