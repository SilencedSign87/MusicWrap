using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Reflection;
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

namespace MusicWrap.UI.Features.Playback.Views
{
    /// <summary>
    /// Lógica de interacción para DevicePage.xaml
    /// </summary>
    public partial class DevicePage : UserControl
    {
        private readonly DeviceViewModel _viewModel;
        public DevicePage()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<DeviceViewModel>();
            DataContext = _viewModel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel.LoadData();
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.UnloadData();
        }
    }
}

