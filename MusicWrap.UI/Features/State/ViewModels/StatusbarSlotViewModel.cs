using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.UI.Features.State.Models;
using System.Collections.ObjectModel;

namespace MusicWrap.UI.Features.State.ViewModels
{
    public partial class StatusbarSlotViewModel : ObservableObject
    {
        [ObservableProperty] private string text = string.Empty;
        [ObservableProperty] private string? icon;

        public ObservableCollection<StatusBarAction> Actions { get; } = [];

        public void UpdateFrom(StatusBarSlot slot)
        {
            Text = slot.Text;
            Icon = slot.Icon;
            Actions.Clear();
            foreach (var action in slot.Actions)
            {
                Actions.Add(action);
            }
        }
    }
}
