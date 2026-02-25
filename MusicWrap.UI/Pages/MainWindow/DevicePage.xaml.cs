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
        private readonly DeviceViewModel _viewModel;
        public DevicePage()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<DeviceViewModel>();
            DataContext = _viewModel;
        }

        private void SampleRateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_viewModel.IsInitialized) return;

            // get index
            if (sender is ComboBox comboBox)
            {
                int index = comboBox.SelectedIndex;
                _viewModel.SetCurrentSampleRate(index);
            }
        }

        private void DeviceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_viewModel.IsInitialized) return;

            if (sender is ComboBox combobox)
            {
                int index = combobox.SelectedIndex;

                _viewModel.SetCurrentDevice(index);

            }
        }
    }
}
