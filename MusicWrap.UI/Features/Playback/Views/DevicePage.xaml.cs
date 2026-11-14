using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

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

