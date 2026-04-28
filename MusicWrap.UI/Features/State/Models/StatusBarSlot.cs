using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MusicWrap.UI.Features.State.Models
{
    public class StatusBarSlot
    {
        public string Text { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public ObservableCollection<StatusBarAction> Actions { get; set; } = new();
    }
}
