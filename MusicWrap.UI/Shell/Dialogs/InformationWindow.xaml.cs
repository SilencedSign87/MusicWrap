using MusicWrap.UI.Helpers;
using MusicWrap.UI.ViewModels;
using System.Windows;
using System.Windows.Interop;

namespace MusicWrap.UI.Shell.Dialogs
{
    public partial class InformationWindow : Window
    {
        private readonly double WindowWidth = 600;
        private readonly double WindowHeight = 700;
        public InformationWindow(InformationWindowViewModel vm)
        {
            InitializeComponent();

            Width = WindowWidth;
            MaxWidth = WindowWidth;
            MinWidth = WindowWidth;

            Height = WindowHeight;
            MaxHeight = WindowHeight;
            MinHeight = WindowHeight;

            DataContext = vm;
            SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = Win32Helper.DwnGetWindowLong(hwnd, Win32Helper.GWL_STYLE) & ~Win32Helper.WS_SYSMENU;
            Win32Helper.DwnSetWindowLong(hwnd, Win32Helper.GWL_STYLE, style);
        }

        public void Initialize(IReadOnlyList<int> trackIds) => (DataContext as InformationWindowViewModel)?.LoadTracks(trackIds);

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

