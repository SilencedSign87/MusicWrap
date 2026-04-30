using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.UI.Features.State.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.Features.State.ViewModels
{
    public partial class StatusbarViewModel : ObservableObject
    {
        private readonly IStatusService _statusbarService;
        private bool _disposed;

        public StatusbarSlotViewModel Left { get; } = new();
        public StatusbarSlotViewModel Center { get; } = new();
        public StatusbarSlotViewModel Right { get; } = new();

        [ObservableProperty]
        private bool isVisible;

        [ObservableProperty]
        private bool isIndeterminate = true;

        [ObservableProperty]
        private double progressValue;

        [ObservableProperty]
        private double progressMaximum = 100;

        [ObservableProperty]
        private string operationName = string.Empty;

        public StatusbarViewModel(IStatusService statusbarService)
        {
            _statusbarService = statusbarService;
            _statusbarService.StateChanged += OnServiceStateChanged;
            UpdateFromService();
        }

        private void OnServiceStateChanged(object? sender, EventArgs e)
        {
            UpdateFromService();
        }

        private void UpdateFromService()
        {
            var state = _statusbarService.Current;

            Left.UpdateFrom(state.Left);
            Center.UpdateFrom(state.Center);
            Right.UpdateFrom(state.Right);

            IsVisible = state.IsVisible;
            IsIndeterminate = state.IsIndeterminate;
            ProgressValue = state.ProgressValue;
            ProgressMaximum = state.ProgressMaximum;
            OperationName = state.OperationName;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _statusbarService.StateChanged -= OnServiceStateChanged;
        }
    }
}
