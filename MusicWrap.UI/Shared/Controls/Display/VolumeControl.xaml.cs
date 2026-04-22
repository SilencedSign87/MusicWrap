using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para VolumeControl.xaml
    /// </summary>
    public partial class VolumeControl : UserControl
    {
        private readonly PlayerViewModel playerViewModel;
        private bool isDragging = false;

        #region Dependency Properties
        public string DominantColorHex
        {
            get { return (string)GetValue(DominantColorHexProperty); }
            set { SetValue(DominantColorHexProperty, value); }
        }

        public static readonly DependencyProperty DominantColorHexProperty =
            DependencyProperty.Register("DominantColorHex", typeof(string), typeof(VolumeControl),
                new PropertyMetadata("#FFFFFF", (d, e) => ((VolumeControl)d).RebuildVolumeGeometry()));

        #endregion
        public VolumeControl()
        {
            InitializeComponent();
            playerViewModel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = playerViewModel;

            playerViewModel.PropertyChanged += PlayerViewModel_PropertyChanged;
        }

        private void PlayerViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlayerViewModel.Volume))
            {
                UpdateVolumeVisual(playerViewModel.Volume);
            }
        }

        private void RebuildVolumeGeometry()
        {
            if (VolumeContainer == null || PathBackground == null || PathForeground == null)
            {
                return;
            }

            double containerWidth = VolumeContainer.ActualWidth;
            double containerHeight = VolumeContainer.ActualHeight;

            if (containerWidth <= 0 || containerHeight <= 0)
            {
                PathBackground.Data = null;
                PathForeground.Data = null;
                VolumeClip.Rect = new Rect(0, 0, 0, 0);
                return;
            }

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, containerHeight / 2), true, true);
                context.LineTo(new Point(containerWidth, 0), true, false);
                context.LineTo(new Point(containerWidth, containerHeight), true, false);
            }

            geometry.Freeze();
            PathBackground.Data = geometry;
            PathForeground.Data = geometry;

            UpdateVolumeVisual(playerViewModel.Volume);
        }

        private void UpdateVolumeVisual(double volume)
        {
            if (VolumeContainer == null)
            {
                return;
            }

            double containerWidth = VolumeContainer.ActualWidth;
            double containerHeight = VolumeContainer.ActualHeight;
            if (containerWidth <= 0 || containerHeight <= 0)
            {
                VolumeClip.Rect = new Rect(0, 0, 0, 0);
                return;
            }

            double percentage = Math.Clamp(volume, 0, 1);
            VolumeClip.Rect = new Rect(0, 0, containerWidth * percentage, containerHeight);
        }

        private void InteractiveLayer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            isDragging = true;
            InteractiveLayer.CaptureMouse();
            UpdateVolumePopup(e);
            SetVolumeFromMouse(e);
        }

        private void InteractiveLayer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

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

            if (isDragging)
            {
                SetVolumeFromMouse(e);
            }
        }

        private void InteractiveLayer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!isDragging)
            {
                HideVolumePopup();
            }
        }

        private void SetVolumeFromMouse(MouseEventArgs e)
        {
            Point pos = e.GetPosition(VolumeContainer);
            double containerWidth = VolumeContainer.ActualWidth;
            if (containerWidth <= 0)
            {
                return;
            }

            float volume = (float)Math.Max(0, Math.Min(1, pos.X / containerWidth));
            playerViewModel.Volume = volume;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            RebuildVolumeGeometry();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RebuildVolumeGeometry();
        }

        private void UpdateVolumePopup(MouseEventArgs e)
        {
            double containerWidth = VolumeContainer.ActualWidth;
            if (containerWidth <= 0)
            {
                HideVolumePopup();
                return;
            }

            Point pos = e.GetPosition(VolumeContainer);
            double clampedX = Math.Clamp(pos.X, 0, containerWidth);
            double percentage = clampedX / containerWidth;
            int volumeValue = (int)Math.Round(percentage * 100);

            VolumePopupText.Text = volumeValue.ToString();
            VolumePopup.HorizontalOffset = clampedX + 12;
            VolumePopup.VerticalOffset = -30;
            VolumePopup.IsOpen = true;
        }

        private void HideVolumePopup()
        {
            VolumePopup.IsOpen = false;
        }
    }
}
