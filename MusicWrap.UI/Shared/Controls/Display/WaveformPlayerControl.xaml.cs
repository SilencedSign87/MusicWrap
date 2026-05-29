using Acornima;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Threading;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para WaveformPlayerControl.xaml
    /// </summary>
    public partial class WaveformPlayerControl : UserControl
    {
        // Services
        private readonly IMusicPlayerService _musicService;
        private readonly DispatcherTimer _positionTimer;
        // Data
        private float[] _waveformData = Array.Empty<float>();

        // State
        private double _position = 0;
        private double _duration = 0;
        private double _lastEnginePosition = 0;
        private DateTime _lastEnginePositionAtUTC = DateTime.MinValue;

        private bool _isSeeking = false;
        private bool _isPlaying = false;

        private bool _isDragging = false;
        private bool _dragCanceled = false;

        private const double minWavePointHeight = 0.5;
        private UIElement? _dragCaptureElement;

        private bool _disposed = false;

        public WaveformPlayerControl()
        {
            InitializeComponent();
            _musicService = App.Services.GetRequiredService<IMusicPlayerService>();
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _positionTimer.Tick += _positionTimer_Tick;

            Loaded += WaveformPlayerControl_Loaded;
            Unloaded += WaveformPlayerControl_Unloaded;
        }

        #region Dependency Properties

        private static readonly DependencyPropertyKey FormattedPositionPropertyKey =
          DependencyProperty.RegisterReadOnly(nameof(FormattedPosition), typeof(string), typeof(WaveformPlayerControl),
              new PropertyMetadata("0:00"));
        public string FormattedPosition => (string)GetValue(FormattedPositionPropertyKey.DependencyProperty);

        private static readonly DependencyPropertyKey FormattedDurationPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(FormattedDuration), typeof(string), typeof(WaveformPlayerControl),
                new PropertyMetadata("0:00"));
        public string FormattedDuration => (string)GetValue(FormattedDurationPropertyKey.DependencyProperty);

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

        public static readonly DependencyProperty UsePlaceholderWaveformProperty =
            DependencyProperty.Register(
            nameof(UsePlaceholderWaveform),
            typeof(bool),
            typeof(WaveformPlayerControl),
            new PropertyMetadata(false));
        public bool UsePlaceholderWaveform
        {
            get => (bool)GetValue(UsePlaceholderWaveformProperty);
            set => SetValue(UsePlaceholderWaveformProperty, value);
        }

        #endregion

        #region Lifecycle

        private void WaveformPlayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            _musicService.PositionChanged += OnServicePositionChanged;
            _musicService.TrackChanged += OnServiceTrackChanged;
            _musicService.PlaybackStateChanged += OnServicePlaybackStateChanged;

            if (!UsePlaceholderWaveform)
            {
                _musicService.WaveformDataChanged += OnServiceWaveformDataChanged;
                _waveformData = _musicService.CurrentWaveformData ?? Array.Empty<float>(); // initial load
            }
            else
            {
                _waveformData = Enumerable.Repeat(1f, 1000).ToArray();
            }

            _duration = _musicService.Duration;
            _isPlaying = _musicService.IsPlaying;
            SyncBaseline();

            DrawWaveform();
            UpdateProgressVisual(_position);
            UpdateFormattedDuration();
            _positionTimer.Start();
        }

        private void WaveformPlayerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _positionTimer.Stop();
            _musicService.PositionChanged -= OnServicePositionChanged;
            _musicService.TrackChanged -= OnServiceTrackChanged;
            _musicService.PlaybackStateChanged -= OnServicePlaybackStateChanged;

            if (!UsePlaceholderWaveform)
                _musicService.WaveformDataChanged -= OnServiceWaveformDataChanged;

        }

        private void _positionTimer_Tick(object? sender, EventArgs e)
        {
            if (_isSeeking || _isDragging || _duration <= 0 || !_isPlaying) return;
            var elapsed = (DateTime.UtcNow - _lastEnginePositionAtUTC).TotalSeconds;
            var predicted = _lastEnginePosition + elapsed;
            predicted = Math.Clamp(predicted, 0, _duration);
            if (Math.Abs(predicted - _position) >= 0.01)
            {
                _position = predicted;
                UpdateProgressVisual(predicted);
                UpdateFormattedPosition(predicted);
            }
        }
        #endregion

        #region Service Events
        private void OnServicePositionChanged(object? sender, double position)
        {
            Dispatcher.Invoke(() =>
            {
                _lastEnginePosition = position;
                _lastEnginePositionAtUTC = DateTime.UtcNow;

                if (!_isSeeking && !_isDragging)
                {
                    _position = position;
                    UpdateProgressVisual(position);
                    UpdateFormattedPosition(position);
                }
            });
        }
        private void OnServiceWaveformDataChanged(object? sender, float[] e)
        {
            Dispatcher.Invoke(() =>
            {
                _waveformData = e.Length > 0 ? e : Array.Empty<float>();
                DrawWaveform();
            });
        }
        private void OnServiceTrackChanged(object? sender, string e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!UsePlaceholderWaveform)
                {
                    _waveformData = _musicService.CurrentWaveformData;
                }
                _position = 0;
                _duration = _musicService.Duration;
                SyncBaseline();
                DrawWaveform();
                UpdateProgressVisual(0);
                UpdateFormattedPosition(0);
                UpdateFormattedDuration();
            });
        }
        private void OnServicePlaybackStateChanged(object? sender, Data.Library.Models.PlaybackState state)
        {
            Dispatcher.Invoke(() =>
            {
                _isPlaying = state == Data.Library.Models.PlaybackState.Playing;

                if (_isPlaying)
                    SyncBaseline();
            });
        }
        #endregion

        private void SyncBaseline()
        {
            _lastEnginePosition = _musicService.CurrentPosition;
            _lastEnginePositionAtUTC = DateTime.UtcNow;
        }

        #region Rendering


        private void DrawWaveform()
        {
            if (_waveformData == null || _waveformData.Length == 0 || ActualWidth == 0 || ActualHeight == 0)
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
                for (int i = 0; i < _waveformData.Length; i++)
                {
                    double x = (i / (double)(_waveformData.Length - 1)) * width;
                    double amplitude = Math.Max(_waveformData[i] * midY, minWavePointHeight); // ensure minimum height
                    double y = midY - amplitude;
                    context.LineTo(new Point(x, y), true, false);
                }
                // draw down (mirrored)
                for (int i = _waveformData.Length - 1; i >= 0; i--)
                {
                    double x = (i / (double)(_waveformData.Length - 1)) * width;
                    double amplitude = Math.Max(_waveformData[i] * midY, minWavePointHeight); // ensure minimum height
                    double y = midY + amplitude;
                    context.LineTo(new Point(x, y), true, false);
                }
            }
            geometry.Freeze();
            PathBackground.Data = geometry;
            PathForeground.Data = geometry;

            UpdateProgressVisual(_position);
        }

        private void UpdateProgressVisual(double currentPosition)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0 || _duration <= 0)
            {
                ProgressClip.Rect = new Rect(0, 0, 0, 0);
                PositionThumb.Visibility = Visibility.Collapsed;
                return;
            }

            double percentage = Math.Clamp(currentPosition / _duration, 0, 1);
            double x = ActualWidth * percentage;

            ProgressClip.Rect = new Rect(0, 0, x, ActualHeight);

            PositionThumb.X1 = x;
            PositionThumb.X2 = x;
            PositionThumb.Y1 = 0;
            PositionThumb.Y2 = ActualHeight;
            PositionThumb.Visibility = Visibility.Visible;
        }
        #endregion

        #region Mouse Events

        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _duration > 0)
            {
                _dragCanceled = false;
                _isDragging = true;
                _dragCaptureElement = (UIElement)sender;
                _dragCaptureElement.CaptureMouse();

                Focus();
                Keyboard.Focus(this);

                double mousex = e.GetPosition(this).X;
                UpdateVisualFromMouse(mousex);
                UpdateSeekPopup(mousex);

                e.Handled = true;
            }
        }

        private void Rectangle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
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
                _isSeeking = false;
                e.Handled = true;
                return;
            }
            double percentage = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1);
            double target = percentage * _duration;

            _musicService.Seek(target);
            SyncBaseline();
            _isSeeking = false;
            _position = target;
            UpdateProgressVisual(target);
            UpdateFormattedPosition(target);

            e.Handled = true;
        }
        private void Rectangle_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            CancelSeek();
            e.Handled = true;

        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || !_isDragging) return;

            CancelSeek();
            e.Handled = true;

        }

        private void UpdateVisualFromMouse(double mouseX)
        {
            double percentage = Math.Clamp(mouseX / ActualWidth, 0, 1);
            double visualPosition = percentage * _duration;
            UpdateProgressVisual(visualPosition); // update mask
        }
        #endregion

        #region Seek Popup

        private void UpdateSeekPopup(double mouseX)
        {
            if (_duration <= 0 || ActualWidth <= 0)
            {
                return;
            }

            double clampedX = Math.Clamp(mouseX, 0, ActualWidth);
            double percentage = clampedX / ActualWidth;
            double visualPosition = percentage * _duration;

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
            if (!_isDragging) return;


            _dragCanceled = true;
            _isDragging = false;
            _isSeeking = false;

            HideSeekPopup();

            if (_dragCaptureElement is not null)
            {
                _dragCaptureElement.ReleaseMouseCapture();
                _dragCaptureElement = null;
            }

            UpdateProgressVisual(_position);
        }

        #endregion

        #region Loaded / Sizing
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawWaveform();
            UpdateProgressVisual(_position);
        }
        #endregion

        #region Formatting

        private static string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
        }
        private void UpdateFormattedPosition(double position)
        {
            SetValue(FormattedPositionPropertyKey, FormatTime(position));
        }
        private void UpdateFormattedDuration()
        {
            SetValue(FormattedDurationPropertyKey, FormatTime(_duration));
        }
        #endregion

    }
}


