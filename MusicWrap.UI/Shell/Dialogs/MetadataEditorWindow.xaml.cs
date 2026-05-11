using MusicWrap.UI.ViewModels;
using System.Windows;

namespace MusicWrap.UI.Shell.Dialogs
{
    /// <summary>
    /// Lógica de interacción para MetadataEditorWindow.xaml
    /// </summary>
    public partial class MetadataEditorWindow : Window
    {
        public MetadataEditorWindow(MetadataEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}

