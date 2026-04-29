using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.State.ViewModels;
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

namespace MusicWrap.UI.Features.State
{
    public partial class StatusbarControl : UserControl
    {
        private readonly StatusbarViewModel _viewModel;
        public StatusbarControl()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<StatusbarViewModel>();
            DataContext = _viewModel;
        }
    }
}
