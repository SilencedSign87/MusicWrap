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

        [ObservableProperty]
        private string leftText = string.Empty;

        [ObservableProperty]
        private string? leftIcon;

        [ObservableProperty]
        private string centerText = string.Empty;

        [ObservableProperty]
        private string? centerIcon;

        [ObservableProperty]
        private string rightText = string.Empty;

        [ObservableProperty]
        private string? rightIcon;

        [ObservableProperty]
        private bool isVisible;

        [ObservableProperty]
        private bool isIndeterminate = true;

        [ObservableProperty]
        private double progressValue;

        [ObservableProperty]
        private double progressMaximum = 100;

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

            LeftText = state.Left.Text;
            LeftIcon = state.Left.Icon;

            CenterText = state.Center.Text;
            CenterIcon = state.Center.Icon;

            RightText = state.Right.Text;
            RightIcon = state.Right.Icon;

            IsVisible = state.IsVisible;
            IsIndeterminate = state.IsIndeterminate;
            ProgressValue = state.ProgressValue;
            ProgressMaximum = state.ProgressMaximum;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _statusbarService.StateChanged -= OnServiceStateChanged;
        }
    }
}
