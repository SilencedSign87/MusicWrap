using Microsoft.Extensions.Logging;
using MusicWrap.Core.Threading;
using MusicWrap.UI.Features.State.Models;
using System;
using System.Collections.Generic;
using System.Timers;

namespace MusicWrap.UI.Features.State.Services
{
    public interface IStatusService
    {
        StatusBarState Current { get; }
        event EventHandler? StateChanged;
        StatusbarOperation BeginOpetation(string operationName);
        void SetLeftText(string text, string? icon = null);
        void SetCenterText(string text, string? icon = null);
        void SetRightText(string text, string? icon = null);
        void AddAction(StatusbarSlotKind slot, StatusBarAction action);
        void ClearActions(StatusbarSlotKind slot);
        void ReportProgress(double value, double maximum = 100, bool isIndeterminate = false, string? detail = null);
        void ShowMessage(string message, TimeSpan duration, StatusbarSlotKind slot = StatusbarSlotKind.Center);
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
        private readonly System.Timers.Timer _dismissTimer;
        private string _currentOperationId = string.Empty;
        private bool _disposed;
        public StatusBarState Current => _state;

        public event EventHandler? StateChanged;

        public StatusService(IUIDispatcher dispatcher, ILogger<StatusService> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;

            _dismissTimer = new System.Timers.Timer
            {
                AutoReset = false,
                Interval = TimeSpan.FromSeconds(5).TotalMilliseconds
            };
            _dismissTimer.Elapsed += _dismissTimer_Elapsed;

        }
        public StatusbarOperation BeginOpetation(string operationName)
        {
            _dispatcher.Invoke(() =>
            {
                _currentOperationId = Guid.NewGuid().ToString("N").Substring(0,8);
                _state.OperationName = operationName;
                _state.IsVisible = true;
                _state.IsIndeterminate = true;
                _state.ProgressValue = 0;
                _state.DismissAt = null;
                RaiseStateChanged();
                _dismissTimer.Start();

                _logger.LogInformation("Statusbar operation started: {OperationName}", operationName);
            });

            return new StatusbarOperation(_currentOperationId, () => EndOperation(_currentOperationId));
        }
        public void SetLeftText(string text, string? icon = null)
        {
            _dispatcher.Invoke(() =>
            {
                _state.Left.Text = text;
                _state.Left.Icon = icon;
                RaiseStateChanged();
            });
        }

        public void SetCenterText(string text, string? icon = null)
        {
            _dispatcher.Invoke(() =>
            {
                _state.Center.Text = text;
                _state.Center.Icon = icon;
                RaiseStateChanged();
            });
        }

        public void SetRightText(string text, string? icon = null)
        {
            _dispatcher.Invoke(() =>
            {
                _state.Right.Text = text;
                _state.Right.Icon = icon;
                RaiseStateChanged();
            });
        }

        public void AddAction(StatusbarSlotKind slot, StatusBarAction action)
        {
            if (action == null)
            {
                _logger.LogWarning("Attempted to add null action to statusbar");
                return;
            }
            _dispatcher.Invoke(() => { 
                var targetSlot = GetSlot(slot);
                targetSlot?.Actions.Add(action);
                RaiseStateChanged();
            });

        }

        public void ClearActions(StatusbarSlotKind slot)
        {
            _dispatcher.Invoke(() =>
            {
                var targetSlot = GetSlot(slot);
                targetSlot.Actions.Clear();
                RaiseStateChanged();
            });
        }

        public void ReportProgress(double value, double maximum = 100, bool isIndeterminate = false, string? detail = null)
        {
            _dispatcher.Invoke(() =>
            {
                _state.ProgressValue = Math.Clamp(value, 0, maximum);
                _state.ProgressMaximum = Math.Max(1, maximum);
                _state.IsIndeterminate = isIndeterminate;

                if (!string.IsNullOrEmpty(detail))
                {
                    _state.Center.Text = detail;
                }

                RaiseStateChanged();
            });
        }

        public void ShowMessage(string message, TimeSpan duration, StatusbarSlotKind slot = StatusbarSlotKind.Center)
        {
            _dispatcher.Invoke(() =>
            {
                var targetSlot = GetSlot(slot);
                targetSlot.Text = message;
                _state.IsVisible = true;
                _state.IsIndeterminate = true;
                _state.DismissAt = DateTime.UtcNow.Add(duration);
                RaiseStateChanged();
                _dismissTimer.Start();

                _logger.LogInformation("Statusbar message shown: {Message}, will dismiss in {Duration}ms", message, duration.TotalMilliseconds);
            });
        }

        public void Clear()
        {
            _dispatcher.Invoke(() =>
            {
                _state.Clear();
                _currentOperationId = string.Empty;
                _dismissTimer.Stop();
                RaiseStateChanged();

                _logger.LogInformation("Statusbar cleared");
            });
        }

        private void EndOperation(string operationId)
        {
            _dispatcher.Invoke(() => {
                if (_currentOperationId != operationId) return;

                _state.Clear();
                _currentOperationId = string.Empty;
                _dismissTimer.Stop();
                RaiseStateChanged();

                _logger.LogInformation("Statusbar operation ended: {OperationId}", operationId);
            });
        }

        private void _dismissTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            _dispatcher.Invoke(() =>
            {
                if (_state.DismissAt.HasValue && DateTime.UtcNow >= _state.DismissAt)
                {
                    Clear();
                }
                else if (_state.IsVisible)
                {
                    _dismissTimer.Start();
                }
            });
        }

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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _dismissTimer?.Dispose();
        }
    }
}
