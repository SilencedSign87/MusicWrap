using Microsoft.Extensions.Logging;
using MusicWrap.Core.Threading;
using MusicWrap.UI.Features.State.Models;
using System.Collections.ObjectModel;

namespace MusicWrap.UI.Features.State.Services
{
    public interface IStatusService
    {
        StatusBarState Current { get; }
        event EventHandler? StateChanged;
        void PublishState(StatusbarSlotKind slot, string text, string? icon = null, ObservableCollection<StatusBarAction>? actions = null);
        void ClearSlot(StatusbarSlotKind slot);
        void ReportProgress(double value, double maximum = 100, bool isIndeterminate = false, string? detail = null, StatusbarSlotKind? slot = StatusbarSlotKind.Center);
        void ClearProgress();
        void Clear();
    }
    public enum StatusbarSlotKind
    {
        Left,
        Center,
        Right
    }
    public class StatusService : IStatusService, IDisposable
    {
        private readonly IUIDispatcher _dispatcher;
        private readonly ILogger _logger;
        private readonly StatusBarState _state = new();
        private bool _dispose;

        private readonly Stack<StatusSlotSnapshot> _leftStack = new();
        private readonly Stack<StatusSlotSnapshot> _centerStack = new();
        private readonly Stack<StatusSlotSnapshot> _rightStack = new();

        public StatusBarState Current => _state;
        public event EventHandler? StateChanged;

        public StatusService(IUIDispatcher dispatcher, ILogger<StatusService> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
        }
        public void PublishState(StatusbarSlotKind slot, string text, string? icon = null, ObservableCollection<StatusBarAction>? actions = null)
        {
            _dispatcher.Invoke(() =>
            {
                var targetSlot = GetSlot(slot);
                var stack = GetStack(slot);
                var snapshot = new StatusSlotSnapshot
                {
                    Text = targetSlot.Text,
                    Icon = targetSlot.Icon,
                    Actions = targetSlot.Actions
                };
                stack.Push(snapshot);
                // publicate new state
                targetSlot.Text = text;
                targetSlot.Icon = icon;
                if (actions != null)
                {
                    targetSlot.Actions = actions;
                }
                RaiseStateChanged();
                //_logger.LogInformation("Published new status in {Slot} slot: {Text}", slot, text);
            });
        }
        public void ClearSlot(StatusbarSlotKind slot)
        {
            _dispatcher.Invoke(() =>
            {
                var targetSlot = GetSlot(slot);
                var stack = GetStack(slot);
                if (stack.Count > 0)
                {
                    var previousState = stack.Pop();
                    targetSlot.Text = previousState.Text;
                    targetSlot.Icon = previousState.Icon;
                    targetSlot.Actions = previousState.Actions;
                }
                else
                {
                    targetSlot.Text = string.Empty;
                    targetSlot.Icon = null;
                    targetSlot.Actions.Clear();
                }
                RaiseStateChanged();
            });
        }
        public void ReportProgress(double value, double maximum = 100, bool isIndeterminate = false, string? detail = null, StatusbarSlotKind? slot = StatusbarSlotKind.Center)
        {
            _dispatcher.Invoke(() =>
            {
                _state.ProgressValue = Math.Clamp(value, 0, maximum);
                _state.ProgressMaximum = Math.Max(1, maximum);
                _state.IsIndeterminate = isIndeterminate;
                _state.IsVisible = true;

                if (!string.IsNullOrEmpty(detail))
                {
                    var targetSlot = slot.HasValue ? GetSlot(slot.Value) : _state.Center;
                    targetSlot.Text = detail;
                }

                RaiseStateChanged();
            });
        }
        public void ClearProgress()
        {
            _dispatcher.Invoke(() =>
            {
                _state.ProgressValue = 0;
                _state.ProgressMaximum = 100;
                _state.IsIndeterminate = false;
                _state.IsVisible = false;
                RaiseStateChanged();
            });
        }

        public void Clear()
        {
            _dispatcher.Invoke(() =>
            {
                _state.Clear();
                _leftStack.Clear();
                _centerStack.Clear();
                _rightStack.Clear();
                RaiseStateChanged();
                _logger.LogInformation("Statusbar cleared");
            });
        }


        public void Dispose()
        {
            if (_dispose) return;
            _dispose = true;
        }

        private Stack<StatusSlotSnapshot> GetStack(StatusbarSlotKind kind) => kind switch
        {
            StatusbarSlotKind.Left => _leftStack,
            StatusbarSlotKind.Center => _centerStack,
            StatusbarSlotKind.Right => _rightStack,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private StatusBarSlot GetSlot(StatusbarSlotKind kind) => kind switch
        {
            StatusbarSlotKind.Left => _state.Left,
            StatusbarSlotKind.Center => _state.Center,
            StatusbarSlotKind.Right => _state.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        #region Internal

        #endregion
    }

    public class StatusSlotSnapshot
    {
        public string Text { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public ObservableCollection<StatusBarAction> Actions { get; set; } = new();
    }
}
