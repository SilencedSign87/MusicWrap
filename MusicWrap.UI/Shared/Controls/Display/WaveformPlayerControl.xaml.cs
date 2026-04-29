using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para WaveformPlayerControl.xaml
    /// </summary>
    public partial class WaveformPlayerControl : UserControl
    {
        private bool _isDragging = false;
        private bool _dragCanceled = false;
        private UIElement? _dragCaptureElement;

        public WaveformPlayerControl()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public static readonly DependencyProperty WaveformDataProperty =
            DependencyProperty.Register("WaveformData", typeof(float[]), typeof(WaveformPlayerControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnWaveformDataChanged));
        public float[] WaveformData
        {
            get => (float[])GetValue(WaveformDataProperty);
            set => SetValue(WaveformDataProperty, value);
        }
        public static readonly DependencyProperty PositionProperty =
           DependencyProperty.Register("Position", typeof(double), typeof(WaveformPlayerControl),
               new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged));

        public double Position
        {
            get => (double)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register("Duration", typeof(double), typeof(WaveformPlayerControl),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged));

        public double Duration
        {
            get => (double)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        public static readonly DependencyProperty DominantColorHexProperty =
    DependencyProperty.Register(
        "DominantColorHex",
        typeof(string),
        typeof(WaveformPlayerControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public string? DominantColorHex
        {
            get => (string?)GetValue(DominantColorHexProperty);
            set => SetValue(DominantColorHexProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler? SeekStarted;
        public event EventHandler<double>? SeekEnded;
        public event EventHandler? SeekCanceled;

        #endregion

        private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((WaveformPlayerControl)d).DrawWaveform();
        }

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (WaveformPlayerControl)d;
            // Solo actualizamos visualmente si NO estamos arrastrando
            // (para evitar que el timer del player pelee con el ratón del usuario)
            if (!control._isDragging)
            {
                control.UpdateProgressVisual(control.Position);
            }
        }

        private void DrawWaveform()
        {
            if (WaveformData == null || WaveformData.Length == 0 || ActualWidth == 0 || ActualHeight == 0)
            {
                PathBackground.Data = null;
                PathForeground.Data = null;
                PositionThumb.Visibility = Visibility.Collapsed;
                return;
            }

            double width = ActualWidth;
            double height = ActualHeight;
            double midY = height / 2;

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(new Point(0, midY), true, true);

                // draw up
                for (int i = 0; i < WaveformData.Length; i++)
                {
                    double x = (i / (double)(WaveformData.Length - 1)) * width;
                    double y = midY - (WaveformData[i] * midY);
                    context.LineTo(new Point(x, y), true, false);
                }
                // draw down (mirrored)
                for (int i = WaveformData.Length - 1; i >= 0; i--)
                {
                    double x = (i / (double)(WaveformData.Length - 1)) * width;
                    double y = midY + (WaveformData[i] * midY);
                    context.LineTo(new Point(x, y), true, false);
                }
            }
            geometry.Freeze();
            PathBackground.Data = geometry;
            PathForeground.Data = geometry;

            UpdateProgressVisual(Position);
        }

        private void UpdateProgressVisual(double currentPosition)
        {
            //if (ActualWidth > 0 && ActualHeight > 0 && Duration > 0)
            //{
            //    double percentage = Math.Clamp(currentPosition / Duration, 0, 1);
            //    ProgressClip.Rect = new Rect(0, 0, ActualWidth * percentage, ActualHeight);
            //}
            if (ActualWidth <= 0 || ActualHeight <= 0 || Duration <= 0)
            {
                ProgressClip.Rect = new Rect(0, 0, 0, 0);
                PositionThumb.Visibility = Visibility.Collapsed;
                return;
            }

            double percentage = Math.Clamp(currentPosition / Duration, 0, 1);
            double x = ActualWidth * percentage;

            ProgressClip.Rect = new Rect(0, 0, x, ActualHeight);

            PositionThumb.X1 = x;
            PositionThumb.X2 = x;
            PositionThumb.Y1 = 0;
            PositionThumb.Y2 = ActualHeight;
            PositionThumb.Visibility = Visibility.Visible;
        }

        #region Mouse Events

        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && Duration > 0)
            {
                _dragCanceled = false;
                _isDragging = true;
                _dragCaptureElement = (UIElement)sender;
                _dragCaptureElement.CaptureMouse();

                Focus();
                Keyboard.Focus(this);

                SeekStarted?.Invoke(this, EventArgs.Empty); // notify the viewmodel to stop updating the position while dragging

                double mousex = e.GetPosition(this).X;
                UpdateVisualFromMouse(mousex);
                UpdateSeekPopup(mousex);

                e.Handled = true;
            }
        }

        private void Rectangle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed) {
                double mousex = e.GetPosition(this).X;
                UpdateVisualFromMouse(mousex);
                UpdateSeekPopup(mousex);
            }
        }

        private void Rectangle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging || e.ChangedButton != MouseButton.Left) return;

            _isDragging = false;
            HideSeekPopup();
            if (_dragCaptureElement is not null)
            {
                _dragCaptureElement.ReleaseMouseCapture();
                _dragCaptureElement = null;
            }
            if (_dragCanceled)
            {
                _dragCanceled = false;
                e.Handled = true;
                return;
            }
            double percentage = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0 ,1);
            double NewPosition = percentage * Duration;
            SeekEnded?.Invoke(this, NewPosition);
            e.Handled = true;
        }
        private void Rectangle_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                CancelSeek();
                e.Handled = true;
            }
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _isDragging)
            {
                CancelSeek();
                e.Handled = true;
            }
        }

        private void UpdateVisualFromMouse(double mouseX)
        {
            double percentage = Math.Clamp(mouseX / ActualWidth, 0, 1);
            double visualPosition = percentage * Duration;
            UpdateProgressVisual(visualPosition); // update mask
        }
        #endregion

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DrawWaveform();
            UpdateProgressVisual(Position);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawWaveform();
            UpdateProgressVisual(Position);
        }


        private static string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (ts.TotalHours >= 1)
            {
                return ts.ToString(@"h\:mm\:ss");
            }

            return ts.ToString(@"m\:ss");
        }

        private void UpdateSeekPopup(double mouseX)
        {
            if (Duration <= 0 || ActualWidth <= 0)
            {
                return;
            }

            double clampedX = Math.Clamp(mouseX, 0, ActualWidth);
            double percentage = clampedX / ActualWidth;
            double visualPosition = percentage * Duration;

            SeekPopupText.Text = FormatTime(visualPosition);

            SeekPopup.HorizontalOffset = clampedX + 19;
            SeekPopup.VerticalOffset = -34;
            SeekPopup.IsOpen = true;
        }

        private void HideSeekPopup()
        {
            SeekPopup.IsOpen = false;
        }

        private void CancelSeek()
        {
            if (!_isDragging)
            {
                return;
            }

            _dragCanceled = true;
            _isDragging = false;

            HideSeekPopup();

            if (_dragCaptureElement is not null)
            {
                _dragCaptureElement.ReleaseMouseCapture();
                _dragCaptureElement = null;
            }

            // Vuelve al valor real del player.
            UpdateProgressVisual(Position);

            SeekCanceled?.Invoke(this, EventArgs.Empty);
        }
    }
}


