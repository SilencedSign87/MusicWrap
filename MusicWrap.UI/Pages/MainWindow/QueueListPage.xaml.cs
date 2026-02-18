using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels.Library;
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
    /// Lógica de interacción para QueueListPage.xaml
    /// </summary>
    public partial class QueueListPage : UserControl
    {
        public readonly QueueViewModel _vm;
        public QueueListPage()
        {
            InitializeComponent();

            _vm = App.Services.GetRequiredService<QueueViewModel>();
            DataContext = _vm;

        }
    }
}
