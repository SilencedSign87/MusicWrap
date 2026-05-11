using System.Collections.ObjectModel;

namespace MusicWrap.UI.Features.State.Models
{
    public class StatusBarSlot
    {
        public string Text { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public ObservableCollection<StatusBarAction> Actions { get; set; } = new();
    }
}
