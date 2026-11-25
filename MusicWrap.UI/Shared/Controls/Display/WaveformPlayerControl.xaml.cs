using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para WaveformPlayerControl.xaml
    /// </summary>
    public partial class WaveformPlayerControl : UserControl
    {
        // Services
        private readonly MusicPlayerService _musicService;
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

        private UIElement? _dragCaptureElement;

        private CancellationTokenSource? _waveformCts;
        private int _loadedTrackId;
        private int _loadedPoints;
        private int _requestedTrackId;
        private int _requestedPoints;


        public WaveformPlayerControl()
        {
            InitializeComponent();
            _musicService = App.Services.GetRequiredService<MusicPlayerService>();
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



        public int WavePoints
        {
            get { return (int)GetValue(WavePointsProperty); }
            set { SetValue(WavePointsProperty, value); }
        }

        public static readonly DependencyProperty WavePointsProperty =
            DependencyProperty.Register(nameof(WavePoints), typeof(int), typeof(WaveformPlayerControl), new PropertyMetadata(0, OnWaveformSourceChanged));


        public WaveformPlayerRenderMode RenderMode
        {
            get { return (WaveformPlayerRenderMode)GetValue(RenderModeProperty); }
            set { SetValue(RenderModeProperty, value); }
        }

        public static readonly DependencyProperty RenderModeProperty =
            DependencyProperty.Register(nameof(RenderMode), typeof(WaveformPlayerRenderMode), typeof(WaveformPlayerControl), new PropertyMetadata(WaveformPlayerRenderMode.Continuous, OnWaveformSourceChanged));



        public bool MirrorRender
        {
            get { return (bool)GetValue(MirrorRenderProperty); }
            set { SetValue(MirrorRenderProperty, value); }
        }

        public static readonly DependencyProperty MirrorRenderProperty =
            DependencyProperty.Register(nameof(MirrorRender), typeof(bool), typeof(WaveformPlayerControl), new PropertyMetadata(true, OnVisualChanged));



        public string? DominantColorHex
        {
            get => (string?)GetValue(DominantColorHexProperty);
            set => SetValue(DominantColorHexProperty, value);
        }

        private static void OnWaveformSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((WaveformPlayerControl)d).Refreshwaveform();
        private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (WaveformPlayerControl)d;
            if (!control.IsLoaded) return;
            control.DrawWaveform();
            control.UpdateProgressVisual(control._position);
        }
        #endregion

        #region Lifecycle

        private void WaveformPlayerControl_Loaded(object sender, RoutedEventArgs e)
        {
            _musicService.PositionChanged += OnServicePositionChanged;
            _musicService.TrackChanged += OnServiceTrackChanged;
            _musicService.PlaybackStateChanged += OnServicePlaybackStateChanged;

            _duration = _musicService.Duration;
            _isPlaying = _musicService.IsPlaying;
            SyncBaseline();

            Refreshwaveform();
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


            CancelWaveformRequest();
            _waveformData = [];
            _loadedTrackId = 0;
            _loadedPoints = 0;
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
        private void OnServiceTrackChanged(object? sender, string e)
        {
            Dispatcher.Invoke(() =>
            {
                _position = 0;
                _duration = _musicService.Duration;
                SyncBaseline();

                _waveformData = [];
                _loadedTrackId = 0;
                _loadedPoints = 0;

                Refreshwaveform();
                UpdateProgressVisual(0);
                UpdateFormattedPosition(0);
                UpdateFormattedDuration();
            });
        }
        private void Refreshwaveform()
        {
            if (!IsLoaded) return;

            int trackId = _musicService.CurrentTrackId;
            int points = WavePoints;

            bool wantsWave =
                RenderMode != WaveformPlayerRenderMode.None &&
                points > 0 &&
                trackId > 0;

            if (!wantsWave)
            {
                CancelWaveformRequest();
                _waveformData = [];
                _loadedTrackId = 0;
                _loadedPoints = 0;
                DrawWaveform();
                return;
            }

            if (_waveformData.Length > 0 &&
                _loadedTrackId == trackId &&
                _loadedPoints == points)
            {
                DrawWaveform();
                return;
            }

            if (_waveformCts is not null &&
                _requestedTrackId == trackId &&
                _requestedPoints == points)
            {
                return;
            }

            CancelWaveformRequest();

            // instant fallback
            _waveformData = CreateFlatWaveform(points);
            _loadedTrackId = 0;
            _loadedPoints = 0;
            DrawWaveform();

            // request new waveform
            var cts = new CancellationTokenSource();
            _waveformCts = cts;
            _requestedTrackId = trackId;
            _requestedPoints = points;

            _ = LoadWaveformAsync(trackId, points, cts);
        }
        private async Task LoadWaveformAsync(int trackId, int points, CancellationTokenSource cts)
        {
            try
            {
                var data = await _musicService.GetWaveformAsync(trackId, points, cts.Token);

                if (cts.Token.IsCancellationRequested || trackId != _musicService.CurrentTrackId)
                    return;

                _waveformData = data;
                _loadedTrackId = trackId;
                _loadedPoints = points;
                DrawWaveform();
            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                if (ReferenceEquals(_waveformCts, cts))
                    _waveformCts = null;

                cts.Dispose();
            }
        }

        private void OnServicePlaybackStateChanged(object? sender, ManagedBass.PlaybackState state)
        {
            Dispatcher.Invoke(() =>
            {
                _isPlaying = state == ManagedBass.PlaybackState.Playing;

                if (_isPlaying)
                    SyncBaseline();
            });
        }
        #endregion

        private void SyncBaseline()
        {
            _lastEnginePosition = _musicService.CurrentPosition;
            _lastEnginePositionAtUTC = DateTime.UtcNow;
            _position = _lastEnginePosition;
        }
        private void CancelWaveformRequest()
        {
            var cts = _waveformCts;
            _waveformCts = null;
            if (cts is null) return;

            cts.Cancel();
            cts.Dispose();
        }

        #region Rendering

        private void DrawWaveform()
        {
            double width = WaveformContainer.ActualWidth;
            double height = WaveformContainer.ActualHeight;

            if (width <= 0 || height <= 0)
                return;

            if (RenderMode == WaveformPlayerRenderMode.None)
            {
                var track = BuildPillGeometry(width, height);
                PathBackground.Data = track;
                PathForeground.Data = track;
                UpdateProgressVisual(_position);
                return;
            }

            Geometry? geometry = RenderMode switch
            {
                WaveformPlayerRenderMode.Continuous => BuildContinuousGeometry(width, height),
                WaveformPlayerRenderMode.Discrete => BuildDiscreteGeometry(width, height),
                _ => null,
            };

            PathBackground.Data = geometry;
            PathForeground.Data = geometry;

            if (geometry is null)
            {
                PositionThumb.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateProgressVisual(_position);

        }

        private Geometry BuildPillGeometry(double width, double height)
        {
            var geometry = new RectangleGeometry(
                    new Rect(0, 0, width, height),
                    height / 2,
                    height / 2);

            geometry.Freeze();
            return geometry;
        }

        private Geometry? BuildDiscreteGeometry(double width, double height)
        {
            var data = _waveformData;
            int count = data.Length;
            if (count == 0) return null;

            double slot = width / count;
            double barWidth = Math.Max(1.0, slot * 0.6);
            double offset = (slot - barWidth) / 2;
            double baseY = MirrorRender ? height / 2 : height;
            double maxAmplitude = MirrorRender ? height / 2 : height;

            var geometry = new StreamGeometry();

            using (var ctx = geometry.Open())
            {
                for (int i = 0; i < count; i++)
                {
                    double x = i * slot + offset;
                    double amp = Math.Max(data[i] * maxAmplitude, 0.5);

                    if (MirrorRender)
                    {
                        ctx.BeginFigure(new Point(x, baseY - amp), true, true);
                        ctx.LineTo(new Point(x + barWidth, baseY - amp), true, false);
                        ctx.LineTo(new Point(x + barWidth, baseY + amp), true, false);
                        ctx.LineTo(new Point(x, baseY + amp), true, false);
                    }
                    else
                    {
                        ctx.BeginFigure(new Point(x, baseY), true, true);
                        ctx.LineTo(new Point(x + barWidth, baseY), true, false);
                        ctx.LineTo(new Point(x + barWidth, baseY - amp), true, false);
                        ctx.LineTo(new Point(x, baseY - amp), true, false);
                    }
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private Geometry? BuildContinuousGeometry(double width, double height)
        {
            var data = _waveformData;
            int count = data.Length;
            if (count == 0) return null;

            double baseY = MirrorRender ? height / 2 : height;
            double maxAmplitude = MirrorRender ? height / 2 : height;

            if (count == 1)
            {
                double amp = Math.Max(data[0] * maxAmplitude, 0.5);
                var single = MirrorRender
                    ? new RectangleGeometry(new Rect(0, baseY - amp, width, amp * 2))
                    : new RectangleGeometry(new Rect(0, baseY - amp, width, amp));
                single.Freeze();
                return single;
            }

            double step = width / (count - 1);
            var geometry = new StreamGeometry();

            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, baseY), true, true);

                for (int i = 0; i < count; i++)
                {
                    double x = i * step;
                    double amp = Math.Max(data[i] * maxAmplitude, 0.5);
                    ctx.LineTo(new Point(x, baseY - amp), true, false);
                }

                if (MirrorRender)
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        double x = i * step;
                        double amp = Math.Max(data[i] * maxAmplitude, 0.5);
                        ctx.LineTo(new Point(x, baseY + amp), true, false);
                    }
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private static float[] CreateFlatWaveform(int points)
        {
            var data = new float[points];
            Array.Fill(data, 0.5f);
            return data;
        }

        private void UpdateProgressVisual(double currentPosition)
        {
            double width = WaveformContainer.ActualWidth;
            double height = WaveformContainer.ActualHeight;

            if (width <= 0 || height <= 0 || _duration <= 0)
            {
                ProgressClip.Rect = new Rect(0, 0, 0, 0);
                PositionThumb.Visibility = Visibility.Collapsed;
                return;
            }

            double percentage = Math.Clamp(currentPosition / _duration, 0, 1);
            double x = width * percentage;

            ProgressClip.Rect = new Rect(0, 0, x, height);

            PositionThumb.X1 = x;
            PositionThumb.X2 = x;
            PositionThumb.Y1 = 0;
            PositionThumb.Y2 = height;
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

                double mousex = e.GetPosition(WaveformContainer).X;
                UpdateVisualFromMouse(mousex);
                UpdateSeekPopup(mousex);

                e.Handled = true;
            }
        }

        private void Rectangle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                double mousex = e.GetPosition(WaveformContainer).X;
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
            double percentage = Math.Clamp(e.GetPosition(WaveformContainer).X / WaveformContainer.ActualWidth, 0, 1);
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
            switch (e.Key)
            {
                case Key.Escape when _isDragging:
                    CancelSeek();
                    e.Handled = true;
                    break;
                case Key.Up:
                case Key.Right:
                    {
                        double newPosition = Math.Min(_position + 5, _duration);
                        _musicService.Seek(newPosition);
                        SyncBaseline();
                        UpdateProgressVisual(newPosition);
                        UpdateFormattedPosition(newPosition);
                        e.Handled = true;
                        break;
                    }

                case Key.Down:
                case Key.Left:
                    {
                        double newPosition = Math.Max(_position - 5, 0);
                        _musicService.Seek(newPosition);
                        SyncBaseline();
                        UpdateProgressVisual(newPosition);
                        UpdateFormattedPosition(newPosition);
                        e.Handled = true;
                        break;
                    }
            }


        }

        private void UpdateVisualFromMouse(double mouseX)
        {
            double percentage = Math.Clamp(mouseX / WaveformContainer.ActualWidth, 0, 1);
            double visualPosition = percentage * _duration;
            UpdateProgressVisual(visualPosition); // update mask
        }
        #endregion

        #region Seek Popup

        private void UpdateSeekPopup(double mouseX)
        {
            if (_duration <= 0 || WaveformContainer.ActualWidth <= 0)
            {
                return;
            }

            double clampedX = Math.Clamp(mouseX, 0, WaveformContainer.ActualWidth);
            double percentage = clampedX / WaveformContainer.ActualWidth;
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
        private void WaveformContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawWaveform();
            UpdateProgressVisual(_position);
        }
        #endregion

        #region Formatting

        private static string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return FormatHelpers.FormatDuration(ts);
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
    #region Models
    public enum WaveformPlayerRenderMode
    {
        None, // 0
        Discrete, // 1
        Continuous, // 2
    }
    #endregion
}


