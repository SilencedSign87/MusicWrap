using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels.Library;
using System.Windows.Controls;

namespace MusicWrap.UI.Pages.MainWindow
{
    /// <summary>
    /// Lógica de interacción para QueueListPage.xaml
    /// </summary>
    public partial class QueueListPage : UserControl
    {
        public QueueListPage()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<QueueViewModel>();
        }
    }
}
