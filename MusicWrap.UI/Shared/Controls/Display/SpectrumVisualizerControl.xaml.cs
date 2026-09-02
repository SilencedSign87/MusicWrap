using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.User.Models;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MusicWrap.UI.Controls
{
    public partial class SpectrumVisualizerControl : UserControl
    {
        private readonly IMusicPlayerService _musicService;
        private readonly DispatcherTimer _timer;

        private float[] _currentHeights = [];
        private bool _isActive;

        private const float Amplification = 1.0f;
        private const float RiseSpeed = 0.6f; // how quickly the bars rise to the target value (0-1)
        private const float FallSpeed = 0.6f; // how quickly the bars fall to the target value (0-1)
        private const float HeightDecay = 0.03f; // how quickly the current height value falls (0-1)

        private const int BaseFFTSize = 16384;      // at 48 kHz
        private const int MaxFFTSize = 16384;       // cap for 192+ kHz
        private readonly float[] _pcmBuffer = new float[MaxFFTSize * 2];

        private readonly SpectrumPipeline _pipeline;
        private int _samplerate = 44100;
        private int _currentFFTSize = BaseFFTSize;

        private float[] _fftInput = [];
        private Complex[] _fftComplex = [];
        private float[] _magnitudes = [];
        private Point[] _points = [];

        private readonly Brush _fadeMask;

        public SpectrumVisualizerControl()
        {
            InitializeComponent();

            _musicService = App.Services.GetRequiredService<IMusicPlayerService>();

            _pipeline = new SpectrumPipeline(new SpectrumPipelineConfig { SampleRate = _samplerate, FftSize = _currentFFTSize });

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(30)
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
                widget._pipeline?.SetBandCount((int)e.NewValue);
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

            int sr =_musicService.GetCurrentOutputSampleRate();
            if(sr > 0 && sr != _samplerate)
            {
                _samplerate = sr;
                _currentFFTSize = ComputeFftSize(_samplerate);
                _pipeline.OnConfigurationChanged(_samplerate, _currentFFTSize);
                UpdateTimerInterval();
            }

            int capturedFloats = _musicService.GetCapturedPCMData(_pcmBuffer);

            if (capturedFloats <= 0 || capturedFloats < _currentFFTSize * 2)
            {
                DecayAll();
                return;
            }

            int monoAvailable = capturedFloats / 2;

            int fftSize = Math.Min(_currentFFTSize, PrevPow2(monoAvailable));

            if (fftSize < 1024)
            {
                DecayAll();
                return;
            }

            if (_fftInput.Length != fftSize)
            {
                _fftInput = new float[fftSize];
                _fftComplex = new Complex[fftSize];
                _magnitudes = new float[fftSize / 2];
            }

            for (int i = 0; i < fftSize; i++)
            {
                _fftInput[i] = (_pcmBuffer[i * 2] + _pcmBuffer[i * 2 + 1]) * 0.5f;
            }

            ApplyHanningWindow(_fftInput, fftSize);

            ComputeFFT(_fftInput, _fftComplex, fftSize);

            for (int i = 0; i < _magnitudes.Length; i++)
            {
                double re = _fftComplex[i].Real;
                double im = _fftComplex[i].Imaginary;
                double power = (re * re + im * im) / (fftSize * fftSize);

                _magnitudes[i] = (float)Math.Sqrt(power);
            }

            float[] displayBands = _pipeline.Process(_magnitudes);
            UpdateHeights(displayBands);
            Render();
        }
        private void UpdateTimerInterval()
        {
            if (_currentFFTSize >= 32768)
                _timer.Interval = TimeSpan.FromMilliseconds(33); // 30 FPS
            else if (_currentFFTSize >= 16384)
                _timer.Interval = TimeSpan.FromMilliseconds(29); // 35 FPS
            else
                _timer.Interval = TimeSpan.FromMilliseconds(25); // 40 FPS
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
                float target = Math.Clamp(bands[i] * Amplification, 0f, 1f);
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

            var fillGeometry = new StreamGeometry();
            using (var ctx = fillGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, baseY), true, true);
                ctx.LineTo(_points[0], true, false);

                for (int i = 1; i < _points.Length; i++)
                    ctx.LineTo(_points[i], true, false);

                ctx.LineTo(new Point(width, baseY), true, false);
            }
            fillGeometry.Freeze();

            var topGeometry = new StreamGeometry();
            using (var ctx = topGeometry.Open())
            {
                ctx.BeginFigure(_points[0], false, false);
                for (int i = 1; i < _points.Length; i++)
                    ctx.LineTo(_points[i], true, false);
            }
            topGeometry.Freeze();

            SpectrumFillPath.Data = fillGeometry;
            SpectrumTopPath.Data = topGeometry;
            SpectrumFillPath.OpacityMask = _fadeMask;
        }

        private void DrawBars(double width, double height, bool mirrored)
        {
            int bandCount = _currentHeights.Length;
            double slot = width / bandCount;
            // separation
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
                    }
                    else
                    {
                        yTop = height - barHeight;
                        yBottom = height;
                    }
                    //ctx.BeginFigure(new Point(x0, yTop), true, true);
                    //ctx.LineTo(new Point(x1, yTop), true, false);
                    //ctx.LineTo(new Point(x1, yBottom), true, false);
                    ctx.BeginFigure(new Point(x0, yTop), true, true);
                    ctx.LineTo(new Point(x1, yTop), true, false);
                    ctx.LineTo(new Point(x1, yBottom), true, false);
                    ctx.LineTo(new Point(x0, yBottom), true, false);
                }
            }

            geometry.Freeze();
            SpectrumFillPath.Data = geometry;
            SpectrumTopPath.Data = null;
            SpectrumFillPath.OpacityMask = null;
        }
        #endregion

        #region Computation
        private static void ApplyHanningWindow(float[] data, int length)
        {
            for (int i = 0; i < length; i++)
            {
                double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1)));
                data[i] *= (float)window;
            }
        }
        private static void ComputeFFT(float[] input, Complex[] output, int length)
        {
            // Copy input to output as complex numbers
            for (int i = 0; i < length; i++)
                output[i] = new Complex(input[i], 0);
            // Bit-reversal permutation
            int bits = (int)Math.Log2(length);
            for (int i = 0; i < length; i++)
            {
                int j = BitReverse(i, bits);
                if (i < j)
                    (output[i], output[j]) = (output[j], output[i]);
            }
            // Butterfly stages
            for (int stage = 1; stage <= bits; stage++)
            {
                int halfSize = 1 << (stage - 1);
                int fullSize = 1 << stage;
                double angle = -2.0 * Math.PI / fullSize;
                var wn = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (int k = 0; k < length; k += fullSize)
                {
                    var w = Complex.One;
                    for (int j = 0; j < halfSize; j++)
                    {
                        var t = w * output[k + j + halfSize];
                        var u = output[k + j];
                        output[k + j] = u + t;
                        output[k + j + halfSize] = u - t;
                        w *= wn;
                    }
                }
            }
        }
        private static int BitReverse(int value, int bits)
        {
            int result = 0;
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (value & 1);
                value >>= 1;
            }
            return result;
        }

        private static int PrevPow2(int v)
        {
            int p = 1;
            while ((p << 1) <= v) p <<= 1;
            return p;
        }
        private void RebuildPointCache()
        {
            int count = Math.Max(BarCount, 1);

            _points = new Point[count];

            double width = BarContainer.ActualWidth;

            double step =
                count > 1
                    ? width / (count - 1)
                    : width;

            for (int i = 0; i < count; i++)
                _points[i].X =
                    count > 1
                        ? i * step
                        : width * 0.5;
        }
        private static int ComputeFftSize(int sampleRate)
        {
            // Target ~2 Hz/bin resolution: binHz = (sampleRate/2) / (fftSize/2) = sampleRate / fftSize
            // So fftSize = sampleRate / targetBinHz
            const int targetBinHz = 2;
            int desired = (int)Math.Ceiling((double)sampleRate / targetBinHz);
            // Round up to next power of 2
            int pow2 = 1;
            while (pow2 < desired) pow2 <<= 1;
            return Math.Clamp(pow2, BaseFFTSize, MaxFFTSize);
        }

        #endregion
    }

}
