using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Shared.Controls.ViewModel;
using MusicWrap.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MusicWrap.UI.Controls
{
    public partial class VolumeControl : UserControl, IDisposable
    {
        private readonly VolumeControlViewModel _viewModel;
        private bool isDragging;
        private bool isSubscribed;
        private bool isDisposed;

        private const double TrackThickness = 4d;
        private const double ThumbWidth = 4d;
        private const double ThumbLength = 16d;

        public VolumeControl()
        {
            InitializeComponent();

            Loaded += VolumeControl_Loaded;
            Unloaded += VolumeControl_Unloaded;

            _viewModel = App.Services.GetRequiredService<VolumeControlViewModel>();
            DataContext = _viewModel;
        }

        private void VolumeControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!isSubscribed)
            {
                _viewModel.PropertyChanged += PlayerViewModel_PropertyChanged;
                isSubscribed = true;
            }
            ApplyLayout();
            UpdateVolumeVisual(_viewModel.Volume);
        }

        private void VolumeControl_Unloaded(object sender, RoutedEventArgs e)
        {
           Dispose();
        }
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            if (isSubscribed)
            {
                _viewModel.PropertyChanged -= PlayerViewModel_PropertyChanged;
                (_viewModel as IDisposable)?.Dispose();
                Loaded -= VolumeControl_Loaded;
                Unloaded -= VolumeControl_Unloaded;
                isSubscribed = false;
            }
            isDragging = false;
            HideVolumePopup();
        }

        #region Dependency Properties

        public string DominantColorHex
        {
            get => (string)GetValue(DominantColorHexProperty);
            set => SetValue(DominantColorHexProperty, value);
        }

        public static readonly DependencyProperty DominantColorHexProperty =
            DependencyProperty.Register(nameof(DominantColorHex), typeof(string), typeof(VolumeControl), new PropertyMetadata("#FFFFFF"));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(VolumeControl),
                new PropertyMetadata(Orientation.Horizontal, OnLayoutPropertyChanged));

        public bool ShowMuteButton
        {
            get => (bool)GetValue(ShowMuteButtonProperty);
            set => SetValue(ShowMuteButtonProperty, value);
        }

        public static readonly DependencyProperty ShowMuteButtonProperty =
            DependencyProperty.Register(nameof(ShowMuteButton), typeof(bool), typeof(VolumeControl),
                new PropertyMetadata(true, OnLayoutPropertyChanged));

        #endregion

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VolumeControl control)
            {
                control.ApplyLayout();
                control.UpdateVolumeVisual(control._viewModel.Volume);
            }
        }

        private void PlayerViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VolumeControlViewModel.Volume))
            {
                UpdateVolumeVisual(_viewModel.Volume);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ApplyLayout();
                UpdateVolumeVisual(_viewModel.Volume);
            }, DispatcherPriority.Loaded);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyLayout();
            UpdateVolumeVisual(_viewModel.Volume);
        }

        #region Layout

        private void ApplyLayout()
        {
            if (VolumeContainer == null) return;

            ApplyRootLayout();
            ApplyTrackLayout();
        }

        private void ApplyRootLayout()
        {
            if (ColButton == null) return;

            MuteButton.Visibility = ShowMuteButton ? Visibility.Visible : Visibility.Collapsed;

            if (Orientation == Orientation.Horizontal)
            {
                ColButton.Width = ShowMuteButton ? GridLength.Auto : new GridLength(0);
                ColSpacer.Width = ShowMuteButton ? new GridLength(4) : new GridLength(0);
                ColVolume.Width = new GridLength(1, GridUnitType.Star);

                RowVolume.Height = new GridLength(1, GridUnitType.Star);
                RowSpacer.Height = new GridLength(0);
                RowButton.Height = new GridLength(0);

                Grid.SetRow(MuteButton, 0); Grid.SetColumn(MuteButton, 0);
                Grid.SetRow(LayoutSpacer, 0); Grid.SetColumn(LayoutSpacer, 1);
                Grid.SetRow(VolumeHost, 0); Grid.SetColumn(VolumeHost, 2);

                MuteButton.HorizontalAlignment = HorizontalAlignment.Center;
                MuteButton.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                ColButton.Width = new GridLength(1, GridUnitType.Star);
                ColSpacer.Width = new GridLength(0);
                ColVolume.Width = new GridLength(0);

                RowVolume.Height = new GridLength(1, GridUnitType.Star);
                RowSpacer.Height = ShowMuteButton ? new GridLength(4) : new GridLength(0);
                RowButton.Height = ShowMuteButton ? GridLength.Auto : new GridLength(0);

                Grid.SetRow(VolumeHost, 0); Grid.SetColumn(VolumeHost, 0);
                Grid.SetRow(LayoutSpacer, 1); Grid.SetColumn(LayoutSpacer, 0);
                Grid.SetRow(MuteButton, 2); Grid.SetColumn(MuteButton, 0);

                MuteButton.HorizontalAlignment = HorizontalAlignment.Center;
                MuteButton.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        private void ApplyTrackLayout()
        {
            if (Orientation == Orientation.Horizontal)
            {
                TrackBackground.ClearValue(WidthProperty);
                TrackBackground.Height = TrackThickness;
                TrackBackground.HorizontalAlignment = HorizontalAlignment.Stretch;
                TrackBackground.VerticalAlignment = VerticalAlignment.Center;

                TrackForeground.ClearValue(WidthProperty);
                TrackForeground.Height = TrackThickness;
                TrackForeground.HorizontalAlignment = HorizontalAlignment.Left;
                TrackForeground.VerticalAlignment = VerticalAlignment.Center;

                Thumb.Width = ThumbWidth;
                Thumb.Height = ThumbLength;
                Thumb.HorizontalAlignment = HorizontalAlignment.Left;
                Thumb.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                TrackBackground.Width = TrackThickness;
                TrackBackground.ClearValue(HeightProperty);
                TrackBackground.HorizontalAlignment = HorizontalAlignment.Center;
                TrackBackground.VerticalAlignment = VerticalAlignment.Stretch;

                TrackForeground.Width = TrackThickness;
                TrackForeground.ClearValue(HeightProperty);
                TrackForeground.HorizontalAlignment = HorizontalAlignment.Center;
                TrackForeground.VerticalAlignment = VerticalAlignment.Bottom;

                Thumb.Width = ThumbLength;
                Thumb.Height = ThumbWidth;
                Thumb.HorizontalAlignment = HorizontalAlignment.Center;
                Thumb.VerticalAlignment = VerticalAlignment.Top;
            }
        }

        #endregion

        #region Mouse to Value

        private (double normalized, Point pos) GetNormalizedFromMouse(MouseEventArgs e)
        {
            Point pos = e.GetPosition(VolumeContainer);

            double total = Orientation == Orientation.Horizontal
                ? VolumeContainer.ActualWidth
                : VolumeContainer.ActualHeight;

            if (total <= 0) return (0, pos);

            double axis = Orientation == Orientation.Horizontal ? pos.X : pos.Y;
            double normalized = Orientation == Orientation.Horizontal
                ? axis / total
                : 1.0 - (axis / total);

            normalized = Math.Clamp(normalized, 0.0, 1.0);
            return (normalized, pos);
        }

        private void SetVolumeFromMouse(MouseEventArgs e)
        {
            var (normalized, _) = GetNormalizedFromMouse(e);
            _viewModel.Volume = (float)normalized;
        }

        #endregion

        #region Visual Update

        private void UpdateVolumeVisual(double volume)
        {
            double percentage = Math.Clamp(volume, 0.0, 1.0);

            if (Orientation == Orientation.Horizontal)
            {
                double totalWidth = VolumeContainer.ActualWidth;
                if (totalWidth <= 0) return;

                double fillWidth = totalWidth * percentage;
                TrackForeground.Width = fillWidth;

                double thumbLeft = fillWidth - ThumbWidth / 2.0;
                thumbLeft = Math.Clamp(thumbLeft, 0, Math.Max(0, totalWidth - ThumbWidth));
                Thumb.Margin = new Thickness(thumbLeft, 0, 0, 0);
            }
            else
            {
                double totalHeight = VolumeContainer.ActualHeight;
                if (totalHeight <= 0) return;

                double fillHeight = totalHeight * percentage;
                TrackForeground.Height = fillHeight;

                double thumbTop = (totalHeight - fillHeight) - ThumbLength / 2.0;
                thumbTop = Math.Clamp(thumbTop, 0, Math.Max(0, totalHeight - ThumbWidth));
                Thumb.Margin = new Thickness(0, thumbTop, 0, 0);
            }
        }

        #endregion

        #region Volume Popup

        private void UpdateVolumePopup(MouseEventArgs e)
        {
            var (normalized, pos) = GetNormalizedFromMouse(e);
            VolumePopupText.Text = ((int)Math.Round(normalized * 100)).ToString();

            if (Orientation == Orientation.Horizontal)
            {
                VolumePopup.HorizontalOffset = pos.X + 12;
                VolumePopup.VerticalOffset = -30;
            }
            else
            {
                VolumePopup.HorizontalOffset = 28;
                VolumePopup.VerticalOffset = pos.Y - 20;
            }

            VolumePopup.IsOpen = true;
        }

        private void HideVolumePopup() => VolumePopup.IsOpen = false;

        #endregion

        #region Mouse Handlers

        private void InteractiveLayer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            isDragging = true;
            InteractiveLayer.CaptureMouse();
            UpdateVolumePopup(e);
            SetVolumeFromMouse(e);
        }

        private void InteractiveLayer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            isDragging = false;
            InteractiveLayer.ReleaseMouseCapture();
            HideVolumePopup();
        }

        private void InteractiveLayer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;

            UpdateVolumePopup(e);
            SetVolumeFromMouse(e);
        }

        private void InteractiveLayer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!isDragging) HideVolumePopup();
        }

        #endregion

        private void VolumeHost_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Right)
            {
                _viewModel.Volume = Math.Min(1.0f, _viewModel.Volume + 0.05f);
                e.Handled = true;
            }
            else if (e.Key == Key.Down || e.Key == Key.Left)
            {
                _viewModel.Volume = Math.Max(0.0f, _viewModel.Volume - 0.05f);
                e.Handled = true;
            }
        }
    }
}
