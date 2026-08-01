using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Metadata.Viewmodels;
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
using System.Windows.Shapes;
using TagLib.Riff;

namespace MusicWrap.UI.Shell.Dialogs
{
    /// <summary>
    /// Lógica de interacción para MetadataEditorWindow.xaml
    /// </summary>
    public partial class MetadataEditorWindow : Window
    {
        public MetadataEditorWindow(MetadataEditorWindowViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        public void Initialize(List<int> trackIds) => (DataContext as MetadataEditorWindowViewModel)?.LoadTracks(trackIds);
    }
}

