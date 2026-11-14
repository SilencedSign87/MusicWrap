using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicWrap.UI.ViewModels
{
    public partial class DJControlViewModel : ObservableObject
    {

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Tooltip))]
        [NotifyPropertyChangedFor(nameof(Icon))]
        private bool isDJModeEnabled;

        public string Tooltip => IsDJModeEnabled ? "Disable DJ Mode" : "Enable DJ Mode";
        public string Icon => IsDJModeEnabled ? "\xF8AE" : "\xE7F6";
        public DJControlViewModel()
        {

        }
        [RelayCommand]
        private void ToggleDJMode()
        {
            IsDJModeEnabled = !IsDJModeEnabled;
        }
    }
}
