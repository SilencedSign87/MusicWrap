using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace MusicWrap.UI.Features.State.Models
{
    public class StatusBarAction
    {
        public string Text { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public ICommand Command { get; set; } = null!;
        public object? CommandParameter { get; set; }
    }
}
