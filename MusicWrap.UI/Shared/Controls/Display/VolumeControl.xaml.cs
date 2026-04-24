using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MusicWrap.UI.Controls
{
    public partial class VolumeControl : UserControl
    {
        private readonly PlayerViewModel playerViewModel;
        private bool isDragging;

        private const double MaxThickness = 16d;
        private const double InnerPadding = 4d;
        private const double ControlGap = 4d;

        public VolumeControl()
        {
            InitializeComponent();

            playerViewModel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = playerViewModel;
            playerViewModel.PropertyChanged += PlayerViewModel_PropertyChanged;
        }

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

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VolumeControl control)
            {
                control.ApplyLayout();
                control.UpdateVolumeVisual(control.playerViewModel.Volume);
            }
        }

        private void PlayerViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlayerViewModel.Volume))
            {
                UpdateVolumeVisual(playerViewModel.Volume);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ApplyLayout();
                UpdateVolumeVisual(playerViewModel.Volume);
            }, DispatcherPriority.Loaded);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyLayout();
            UpdateVolumeVisual(playerViewModel.Volume);
        }

        private void ApplyLayout()
        {
            if (VolumeContainer == null) return;

            ApplyRootLayout();
            ApplyPillLayout();
        }

        private void ApplyRootLayout()
        {
            if (ColButton == null) return;

            MuteButton.Visibility = ShowMuteButton ? Visibility.Visible : Visibility.Collapsed;

            if (Orientation == Orientation.Horizontal)
            {
                ColButton.Width = ShowMuteButton ? GridLength.Auto : new GridLength(0);
                ColSpacer.Width = ShowMuteButton ? new GridLength(ControlGap) : new GridLength(0);
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
                RowSpacer.Height = ShowMuteButton ? new GridLength(ControlGap) : new GridLength(0);
                RowButton.Height = ShowMuteButton ? GridLength.Auto : new GridLength(0);

                Grid.SetRow(VolumeHost, 0); Grid.SetColumn(VolumeHost, 0);
                Grid.SetRow(LayoutSpacer, 1); Grid.SetColumn(LayoutSpacer, 0);
                Grid.SetRow(MuteButton, 2); Grid.SetColumn(MuteButton, 0);

                MuteButton.HorizontalAlignment = HorizontalAlignment.Center;
                MuteButton.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        private void ApplyPillLayout()
        {
            double thickness = Orientation == Orientation.Horizontal
                ? Math.Min(MaxThickness, Math.Max(1, VolumeContainer.ActualHeight))
                : Math.Min(MaxThickness, Math.Max(1, VolumeContainer.ActualWidth));

            double innerThickness = Math.Max(1, thickness - (InnerPadding * 2));

            PillBackground.CornerRadius = new CornerRadius(thickness / 2d);
            FillHost.CornerRadius = new CornerRadius(innerThickness / 2d);
            PillForeground.CornerRadius = new CornerRadius(innerThickness / 2d);

            if (Orientation == Orientation.Horizontal)
            {
                VolumeContainer.MaxHeight = MaxThickness;
                VolumeContainer.ClearValue(WidthProperty);

                PillForeground.HorizontalAlignment = HorizontalAlignment.Left;
                PillForeground.VerticalAlignment = VerticalAlignment.Stretch;
            }
            else
            {
                VolumeContainer.MaxWidth = MaxThickness;
                VolumeContainer.ClearValue(HeightProperty);

                PillForeground.HorizontalAlignment = HorizontalAlignment.Stretch;
                PillForeground.VerticalAlignment = VerticalAlignment.Bottom;
            }
        }

        private (double normalized, Point pos) GetNormalizedFromMouse(MouseEventArgs e)
        {
            Point pos = e.GetPosition(VolumeContainer);

            double total = Orientation == Orientation.Horizontal
                ? VolumeContainer.ActualWidth
                : VolumeContainer.ActualHeight;

            double start = InnerPadding;
            double end = Math.Max(start, total - InnerPadding);
            double length = Math.Max(1, end - start);

            double axis = Orientation == Orientation.Horizontal ? pos.X : pos.Y;
            double raw = (axis - start) / length;

            double normalized = Orientation == Orientation.Horizontal ? raw : 1d - raw;
            normalized = Math.Clamp(normalized, 0d, 1d);

            return (normalized, pos);
        }

        private void UpdateVolumeVisual(double volume)
        {
            double total = Orientation == Orientation.Horizontal
                ? VolumeContainer.ActualWidth
                : VolumeContainer.ActualHeight;

            double usable = Math.Max(0, total - (InnerPadding * 2));
            double percentage = Math.Clamp(volume, 0d, 1d);

            if (Orientation == Orientation.Horizontal)
            {
                PillForeground.Width = usable * percentage;
                PillForeground.ClearValue(HeightProperty);
            }
            else
            {
                PillForeground.Height = usable * percentage;
                PillForeground.ClearValue(WidthProperty);
            }
        }

        private void SetVolumeFromMouse(MouseEventArgs e)
        {
            var (normalized, _) = GetNormalizedFromMouse(e);
            playerViewModel.Volume = (float)normalized;
        }

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

            if (!InteractiveLayer.IsMouseOver)
            {
                HideVolumePopup();
            }
        }

        private void InteractiveLayer_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateVolumePopup(e);
            if (isDragging) SetVolumeFromMouse(e);
        }

        private void InteractiveLayer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!isDragging) HideVolumePopup();
        }
    }
}
