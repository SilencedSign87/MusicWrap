using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.User.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicWrap.UI.Controls
{
    public partial class SpectrumVisualizerControl : UserControl
    {
        private readonly IMusicPlayerService _musicService;
        private readonly DispatcherTimer _timer;

        private float[] _currentHeights = [];
        private bool _isActive;

        private const float RiseSpeed = 0.8f;
        private const float FallSpeed = 0.8f;
        private const float HeightDecay = 0.1f;

        private float _valleyGamma = 1.5f;

        private readonly SpectrumPipeline _spectrumPipeline;
        private readonly CenteredSpectrumPipeline _centeredPipeline;
        private int _samplerate = 44100;
        private int _currentFFTSize = 16384;

        private Point[] _points = [];

        private readonly Brush _fadeMask;

        public SpectrumVisualizerControl()
        {
            InitializeComponent();

            _musicService = App.Services.GetRequiredService<IMusicPlayerService>();

            _spectrumPipeline = new SpectrumPipeline(new SpectrumPipelineConfig { SampleRate = _samplerate, FftSize = _currentFFTSize });
            _centeredPipeline = new CenteredSpectrumPipeline(new CenteredSpectrumPipelineConfig { SampleRate = _samplerate, FftSize = _currentFFTSize });

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _timer.Tick += OnTick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            _fadeMask = (Brush)FindResource("SpectrumFadeMask");
        }
        #region Dependency Properties
        public static readonly DependencyProperty BarCountProperty =
            DependencyProperty.Register(
                nameof(BarCount),
                typeof(int),
                typeof(SpectrumVisualizerControl),
                new PropertyMetadata(8, OnBarCountChanged));
        public int BarCount
        {
            get => (int)GetValue(BarCountProperty);
            set => SetValue(BarCountProperty, value);
        }
        private static void OnBarCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpectrumVisualizerControl widget)
            {
                widget._spectrumPipeline?.SetBandCount((int)e.NewValue);
                widget._centeredPipeline?.SetBandCount((int)e.NewValue);
                widget.ReinitArrays();
            }
        }
        public static readonly DependencyProperty VisualizerProperty =
            DependencyProperty.Register(
                nameof(Visualizer),
                typeof(PreferredVisualizer),
                typeof(SpectrumVisualizerControl),
                new PropertyMetadata(PreferredVisualizer.LineSpectrum, OnVisualizerChanged)
                );
        public PreferredVisualizer Visualizer
        {
            get => (PreferredVisualizer)GetValue(VisualizerProperty);
            set => SetValue(VisualizerProperty, value);
        }
        private static void OnVisualizerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpectrumVisualizerControl widget)
            {
                widget.RebuildPointCache();
                widget.Render();
            }
        }
        public SpectrumType SpectrumType
        {
            get { return (SpectrumType)GetValue(SpectrumTypeProperty); }
            set { SetValue(SpectrumTypeProperty, value); }
        }

        public static readonly DependencyProperty SpectrumTypeProperty =
            DependencyProperty.Register(nameof(SpectrumType), typeof(SpectrumType), typeof(SpectrumVisualizerControl), new PropertyMetadata(SpectrumType.Normal, OnSpectrumTypeChanged));
        private static void OnSpectrumTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpectrumVisualizerControl widget)
            {
                widget.RebuildPointCache();
                widget.Render();
            }
        }
        #endregion

        #region Lifecycle
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ReinitArrays();
            _timer.Start();
            _isActive = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _isActive = false;
        }
        private void OnTick(object? sender, EventArgs e)
        {
            if (!_isActive || !_musicService.IsPlaying)
            {
                DecayAll();
                return;
            }

            var (magnitudes, fftSize) = _musicService.GetSpectrumMagnitudes();

            if (magnitudes == null || magnitudes.Length == 0 || fftSize == 0)
            {
                DecayAll();
                return;
            }

            int sr = _musicService.GetCurrentOutputSampleRate();

            if (sr > 0 && sr != _samplerate)
            {
                _samplerate = sr;
                _currentFFTSize = fftSize;
                _spectrumPipeline.OnConfigurationChanged(_samplerate, _currentFFTSize);
                _centeredPipeline.OnConfigurationChanged(_samplerate, _currentFFTSize);
            }
            else if (fftSize != _currentFFTSize)
            {
                _currentFFTSize = fftSize;
                _spectrumPipeline.OnConfigurationChanged(_samplerate, _currentFFTSize);
                _centeredPipeline.OnConfigurationChanged(_samplerate, _currentFFTSize);
            }

            float[] displayBands = SpectrumType == SpectrumType.Centered
                ? _centeredPipeline.Process(magnitudes)
                : _spectrumPipeline.Process(magnitudes);

            if (_currentHeights.Length != displayBands.Length)
            {
                _currentHeights = new float[displayBands.Length];
                _points = new Point[displayBands.Length];
                RebuildPointCache();
            }

            UpdateHeights(displayBands);
            Render();
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // recalculate clip
            var width = e.NewSize.Width;
            var height = e.NewSize.Height;
            ProgressClip.Rect = new Rect(0, 0, width, height);
            // recaculate point cache
            RebuildPointCache();
            Render();
        }

        #endregion

        #region Rendering
        private void ReinitArrays()
        {
            int count = Math.Max(BarCount, 1);
            _currentHeights = new float[count];
            _points = new Point[count];
            RebuildPointCache();
        }
        private void DecayAll()
        {
            bool changed = false;
            for (int i = 0; i < _currentHeights.Length; i++)
            {
                if (_currentHeights[i] > 0) { _currentHeights[i] -= HeightDecay; changed = true; }
                _currentHeights[i] = Math.Max(_currentHeights[i], 0);
            }
            if (changed) Render();
        }
        private void UpdateHeights(float[] bands)
        {
            for (int i = 0; i < _currentHeights.Length && i < bands.Length; i++)
            {
                float target = Math.Clamp(bands[i], 0f, 1f);
                if (_valleyGamma != 1.0f)
                    target = MathF.Pow(Math.Max(target, 0f), _valleyGamma);

                float speed = target > _currentHeights[i] ? RiseSpeed : FallSpeed;
                _currentHeights[i] += (target - _currentHeights[i]) * speed;
            }
        }
        private void Render()
        {
            double width = BarContainer.ActualWidth;
            double height = BarContainer.ActualHeight;
            if (width <= 0 || height <= 0 || _currentHeights.Length == 0)
                return;

            switch (Visualizer)
            {
                case PreferredVisualizer.LineSpectrum:
                    {
                        DrawLines(width, height); break;
                    }
                case PreferredVisualizer.BarSpectrum:
                    {
                        DrawBars(width, height, false); break;
                    }
                case PreferredVisualizer.MirroredBarSpectrum:
                    {
                        DrawBars(width, height, true); break;
                    }
            }
        }

        private void DrawLines(double width, double height)
        {
            int bandCount = _currentHeights.Length;
            double baseY = height;

            for (int i = 0; i < bandCount; i++)
            {
                double normalized = Math.Clamp(_currentHeights[i], 0f, 1f);
                double y = height - Math.Max(normalized * height, 1.0);

                _points[i].Y = y;
            }

            void AppendSmoothCurve(StreamGeometryContext ctx)
            {
                if (_points.Length < 2)
                    return;

                for (int i = 0; i < _points.Length - 1; i++)
                {
                    Point p0 = i > 0 ? _points[i - 1] : _points[0];
                    Point p1 = _points[i];
                    Point p2 = _points[i + 1];
                    Point p3 = i + 2 < _points.Length ? _points[i + 2] : p2;

                    double c1X = p1.X + (p2.X - p0.X) / 6.0;
                    double c1Y = Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6.0, 0, height);

                    double c2X = p2.X - (p3.X - p1.X) / 6.0;
                    double c2Y = Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6.0, 0, height);

                    ctx.BezierTo(new Point(c1X, c1Y), new Point(c2X, c2Y), p2, true, false);
                }
            }

            var fillGeometry = new StreamGeometry();
            using (var ctx = fillGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, baseY), true, true);
                ctx.LineTo(_points[0], true, false);

                //for (int i = 1; i < _points.Length; i++)
                //    ctx.LineTo(_points[i], true, false);

                AppendSmoothCurve(ctx);

                ctx.LineTo(new Point(width, baseY), true, false);
            }
            fillGeometry.Freeze();

            var topGeometry = new StreamGeometry();
            using (var ctx = topGeometry.Open())
            {
                ctx.BeginFigure(_points[0], false, false);
                //for (int i = 1; i < _points.Length; i++)
                //    ctx.LineTo(_points[i], true, false);
                AppendSmoothCurve(ctx);
            }
            topGeometry.Freeze();

            SpectrumFillPath.Data = fillGeometry;
            SpectrumTopPath.Data = topGeometry;
            SpectrumFillPath.OpacityMask = _fadeMask;
        }

        private const double SegmentGap = 1.0;
        private void DrawBars(double width, double height, bool mirrored)
        {
            int bandCount = _currentHeights.Length;
            double slot = width / bandCount;
            double barWidth = slot * 0.8;
            double centerY = height / 2;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                for (int i = 0; i < bandCount; i++)
                {
                    double normalized = Math.Clamp(_currentHeights[i], 0f, 1f);

                    double barHeight = Math.Max(normalized * height, 1.0);
                    double x0 = slot * i + (slot - barWidth) / 2;
                    double x1 = x0 + barWidth;
                    double yTop, yBottom;

                    if (mirrored)
                    {
                        double half = barHeight / 2;
                        yTop = centerY - half;
                        yBottom = centerY + half;

                        double maxRadius = barWidth / 2.0;
                        double rx = Math.Min(maxRadius, barWidth / 2.0);
                        double ry = Math.Min(rx, barHeight / 2.0);

                        if (rx < 0.5 || ry < 0.5)
                        {
                            ctx.BeginFigure(new Point(x0, yTop), true, true);
                            ctx.LineTo(new Point(x1, yTop), true, false);
                            ctx.LineTo(new Point(x1, yBottom), true, false);
                            ctx.LineTo(new Point(x0, yBottom), true, false);
                        }
                        else
                        {
                            var cornerSize = new Size(rx, ry);
                            ctx.BeginFigure(new Point(x0 + rx, yTop), true, true);
                            ctx.LineTo(new Point(x1 - rx, yTop), true, false);
                            ctx.ArcTo(new Point(x1, yTop + ry), cornerSize, 0, false, SweepDirection.Clockwise, true, false);
                            ctx.LineTo(new Point(x1, yBottom - ry), true, false);
                            ctx.ArcTo(new Point(x1 - rx, yBottom), cornerSize, 0, false, SweepDirection.Clockwise, true, false);
                            ctx.LineTo(new Point(x0 + rx, yBottom), true, false);
                            ctx.ArcTo(new Point(x0, yBottom - ry), cornerSize, 0, false, SweepDirection.Clockwise, true, false);
                            ctx.LineTo(new Point(x0, yTop + ry), true, false);
                            ctx.ArcTo(new Point(x0 + rx, yTop), cornerSize, 0, false, SweepDirection.Clockwise, true, false);
                        }
                    }
                    else
                    {
                        double segmentHeight = Math.Max(barWidth * 0.45, 3.0);
                        double stride = segmentHeight + SegmentGap;
                        int segmentCount = (int)((barHeight + SegmentGap) / stride);

                        for (int s = 0; s < segmentCount; s++)
                        {
                            double segBottom = height - s * stride;
                            double segTop = segBottom - segmentHeight;

                            ctx.BeginFigure(new Point(x0, segTop), true, true);
                            ctx.LineTo(new Point(x1, segTop), true, false);
                            ctx.LineTo(new Point(x1, segBottom), true, false);
                            ctx.LineTo(new Point(x0, segBottom), true, false);
                        }
                    }
                }
            }

            geometry.Freeze();
            SpectrumFillPath.Data = geometry;
            SpectrumTopPath.Data = null;
            SpectrumFillPath.OpacityMask = null;
        }

        #endregion

        #region Computation

        private void RebuildPointCache()
        {
            int count = Math.Max(BarCount, 1);
            int totalPoints = Math.Max(_points.Length, 1);

            _points = new Point[totalPoints];

            double width = BarContainer.ActualWidth;

            double step =
                totalPoints > 1
                    ? width / (totalPoints - 1)
                    : width;

            for (int i = 0; i < totalPoints; i++)
                _points[i].X =
                    totalPoints > 1
                        ? i * step
                        : width * 0.5;
        }
        #endregion
    }

}
