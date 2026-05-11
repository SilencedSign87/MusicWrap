namespace MusicWrap.UI.Features.State.Models
{
    public class StatusBarState
    {
        // Slots
        public StatusBarSlot Left { get; } = new();
        public StatusBarSlot Center { get; } = new();
        public StatusBarSlot Right { get; } = new();

        // Progress
        public bool IsVisible { get; set; }
        public bool IsIndeterminate { get; set; }
        public double ProgressValue { get; set; }
        public double ProgressMaximum { get; set; } = 100;

        // Dismissal
        public string OperationName { get; set; } = string.Empty;
        public DateTime? DismissAt { get; set; }

        public void Clear()
        {
            Left.Text = string.Empty;
            Left.Icon = null;
            Left.Actions.Clear();

            Center.Text = string.Empty;
            Center.Icon = null;
            Center.Actions.Clear();

            Right.Text = string.Empty;
            Right.Icon = null;
            Right.Actions.Clear();

            IsVisible = false;
            IsIndeterminate = true;
            ProgressValue = 0;
            ProgressMaximum = 100;
            OperationName = string.Empty;
            DismissAt = null;
        }
    }
}
