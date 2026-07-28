using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para BarVisualizerControl.xaml
    /// </summary>
    public partial class BarVisualizerControl : UserControl
    {
        private readonly IMusicPlayerService _musicService;
        private readonly DispatcherTimer _timer;

        private Brush? _fillBrush;
        private Brush? _topBrush;
        private string? _lastDominantColor;

        private float[] _currentHeights = [];
        private float[] _peakHold = [];
        private bool _isActive;

        private const int AssumedSampleRate = 44100;
        private const float Amplification = 1.0f;
        private const float RiseSpeed = 0.6f; // how quickly the bars rise to the target value (0-1)
        private const float FallSpeed = 0.4f; // how quickly the bars fall to the target value (0-1)
        private const float PeakDecay = 0.03f; // how quickly the peak hold value falls (0-1)
        private const float HeightDecay = 0.1f; // how quickly the current height value falls (0-1)

        private const float MinEqHz = 20f; // lowest frequency
        private const float MaxEqHz = 20000f; // highest frequency
        private const float NoiseFloorDb = -90f; // lowest dB value to consider (anything below this is treated as silence)
        private const float CeilingDb = -12f; // highest dB value to consider (anything above this is treated as max volume)
        private const float EqGamma = 1.0f; // 1 = linear, <1 = more energy on the low end, >1 = more energy on the high end

        private const float HighShelfGain = 0.1f;     // 0 = off, 0.3 = subtle, 0.8 = agressive
        private const float HighShelfCurve = 0.4f;     // <1 = more energy only on the high end
        private const float GammaDelta = 0.2f;        // how much to reduce gamma for high frequencies (0 = off, 0.5 = strong)

        private const int FFTSize = 16384;

        private readonly float[] _pcmBuffer = new float[FFTSize * 2]; // stereo
        private float[] _fftInput = [];
        private Complex[] _fftComplex = [];
        private float[] _magnitudes = [];
        private Point[] _points = [];
        private BandMapping[] _bandMap = [];

        public BarVisualizerControl()
        {
            InitializeComponent();

            SpectrumTopPath.StrokeThickness = 2;
            SpectrumTopPath.StrokeStartLineCap = PenLineCap.Round;
            SpectrumTopPath.StrokeEndLineCap = PenLineCap.Round;
            SpectrumTopPath.StrokeLineJoin = PenLineJoin.Round;

            _musicService = App.Services.GetRequiredService<IMusicPlayerService>();

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _timer.Tick += OnTick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        #region Dependency Properties
        public static readonly DependencyProperty BarCountProperty =
            DependencyProperty.Register(
                nameof(BarCount),
                typeof(int),
                typeof(BarVisualizerControl),
                new PropertyMetadata(8, OnBarCountChanged));
        public int BarCount
        {
            get => (int)GetValue(BarCountProperty);
            set => SetValue(BarCountProperty, value);
        }
        public static readonly DependencyProperty DominantColorHexProperty =
            DependencyProperty.Register(
                nameof(DominantColorHex),
                typeof(string),
                typeof(BarVisualizerControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public string? DominantColorHex
        {
            get => (string?)GetValue(DominantColorHexProperty);
            set => SetValue(DominantColorHexProperty, value);
        }
        private static void OnBarCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BarVisualizerControl widget)
                widget.ReinitArrays();
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
            int capturedFloats = _musicService.GetCapturedPCMData(_pcmBuffer);

            if (capturedFloats <= 0 || capturedFloats < FFTSize * 2)
            {
                DecayAll();
                return;
            }

            int monoAvailable = capturedFloats / 2;

            int fftSize = Math.Min(FFTSize, PrevPow2(monoAvailable));

            //Debug.WriteLine("[BarVisualizer] Captured PCM floats: {0} \n Mono available: {1} \n FFT size: {2} \n target: {3}", capturedFloats, monoAvailable, fftSize, FFTSize);

            if (fftSize < FFTSize)
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

            float[] bands = MapFFTToBands(_magnitudes, BarCount);
            UpdateHeights(bands);
            DrawBars();
        }
        #endregion
        #region Rendering
        private void ReinitArrays()
        {
            int count = Math.Max(BarCount, 1);
            _currentHeights = new float[count];
            _peakHold = new float[count];
            _points = new Point[count];
            RebuildPointCache();
            RebuildBandMapping();
        }
        private void DecayAll()
        {
            bool changed = false;
            for (int i = 0; i < _peakHold.Length; i++)
            {
                if (_peakHold[i] > 0) { _peakHold[i] -= PeakDecay; changed = true; }
                if (_currentHeights[i] > 0) { _currentHeights[i] -= HeightDecay; changed = true; }
                _peakHold[i] = Math.Max(_peakHold[i], 0);
                _currentHeights[i] = Math.Max(_currentHeights[i], 0);
            }
            if (changed) DrawBars();
        }
        private void UpdateHeights(float[] bands)
        {
            for (int i = 0; i < _currentHeights.Length && i < bands.Length; i++)
            {
                float target = Math.Clamp(bands[i] * Amplification, 0f, 1f);
                if (target >= _peakHold[i])
                    _peakHold[i] = target;
                float speed = target > _currentHeights[i] ? RiseSpeed : FallSpeed;
                _currentHeights[i] += (target - _currentHeights[i]) * speed;
                if (_currentHeights[i] > _peakHold[i])
                    _currentHeights[i] = _peakHold[i];
            }
        }

        private float[] MapFFTToBands(float[] spectrum, int bandCount)
        {
            var result = new float[bandCount];

            for (int i = 0; i < bandCount; i++)
            {
                var map = _bandMap[i];

                float mLeft = InterpolateSpectrum(spectrum, map.Left);
                float mCenter = InterpolateSpectrum(spectrum, map.Center);
                float mRight = InterpolateSpectrum(spectrum, map.Right);

                float magnitude = (mLeft + 2f * mCenter + mRight) * 0.25f;

                // high-shelf boost
                float t = (float)i / (bandCount - 1);
                float shelfGain = 1.0f + HighShelfGain * MathF.Pow(t, HighShelfCurve);
                magnitude *= shelfGain;

                // dB + normalization
                float db = 20f * MathF.Log10(magnitude + 1e-8f);
                float norm = (db - NoiseFloorDb) / (CeilingDb - NoiseFloorDb);
                norm = Math.Clamp(norm, 0f, 1f);

                // variable gamma
                float bandGamma = Math.Max(EqGamma - GammaDelta * t, 0.4f);
                result[i] = MathF.Pow(norm, bandGamma);

                //float db = 20f * MathF.Log10(magnitude + 1e-8f);
                //float norm = (db - NoiseFloorDb) / (CeilingDb - NoiseFloorDb);
                //norm = Math.Clamp(norm, 0f, 1f);
                //result[i] = MathF.Pow(norm, EqGamma);
            }

            return result;
        }
        private static float InterpolateSpectrum(float[] spectrum, InterpolatedSample sample)
        {
            return spectrum[sample.Bin0] * (1f - sample.Fraction) +
                   spectrum[sample.Bin1] * sample.Fraction;
        }

        private void DrawBars()
        {
            double width = BarContainer.ActualWidth;
            double height = BarContainer.ActualHeight;
            if (width <= 0 || height <= 0 || _currentHeights.Length == 0)
                return;

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

            EnsureBrushes();
            SpectrumFillPath.Data = fillGeometry;
            SpectrumFillPath.Fill = _fillBrush;

            SpectrumTopPath.Data = topGeometry;
            SpectrumTopPath.Stroke = _topBrush;

        }
        private void EnsureBrushes()
        {
            if (_lastDominantColor == DominantColorHex &&
        _fillBrush != null &&
        _topBrush != null)
                return;

            _lastDominantColor = DominantColorHex;

            _fillBrush = CreateSpectrumFillBrush();
            _topBrush = CreateSpectrumTopBrush();

            SpectrumFillPath.Fill = _fillBrush;
            SpectrumTopPath.Stroke = _topBrush;
        }
        private Brush CreateSpectrumFillBrush()
        {
            Color baseColor = GetBaseColor();

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0.0),
                EndPoint = new Point(0.5, 1.0)
            };

            brush.GradientStops.Add(new GradientStop(Color.FromArgb(180, baseColor.R, baseColor.G, baseColor.B), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(20, baseColor.R, baseColor.G, baseColor.B), 0.65));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1.0));
            brush.Freeze();
            return brush;
        }

        private Brush CreateSpectrumTopBrush()
        {
            Color baseColor = GetBaseColor();
            var brush = new SolidColorBrush(Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B));
            brush.Freeze();
            return brush;
        }
        private Color GetBaseColor()
        {
            if (!string.IsNullOrWhiteSpace(DominantColorHex))
            {
                try
                {
                    var hex = DominantColorHex.StartsWith("#") ? DominantColorHex : "#" + DominantColorHex;
                    return (Color)ColorConverter.ConvertFromString(hex);
                }
                catch
                {
                }
            }

            return Colors.White;
        }
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
        private void RebuildBandMapping()
        {
            int bandCount = Math.Max(BarCount, 1);
            _bandMap = new BandMapping[bandCount];

            int usableBins = FFTSize / 2;
            float nyquist = AssumedSampleRate * 0.5f;
            float binHz = nyquist / usableBins;

            if (binHz <= 0f)
                return;

            double minHz = Math.Max(MinEqHz, binHz);
            double maxHz = Math.Min(MaxEqHz, nyquist * 0.98);
            if (maxHz <= minHz)
                maxHz = nyquist * 0.98;
            double ratio = Math.Pow(maxHz / minHz, 1.0 / bandCount);
            double lowHz = minHz;

            for (int i = 0; i < bandCount; i++)
            {
                double highHz = lowHz * ratio;
                double center = Math.Sqrt(lowHz * highHz);
                double left = Math.Sqrt(lowHz * center);
                double right = Math.Sqrt(center * highHz);
                _bandMap[i] = new BandMapping(
                    ComputeInterpolatedSample(left, binHz, usableBins),
                    ComputeInterpolatedSample(center, binHz, usableBins),
                    ComputeInterpolatedSample(right, binHz, usableBins)
                );
                lowHz = highHz;
            }
        }
        private static InterpolatedSample ComputeInterpolatedSample(double frequency, float binHz, int maxBin)
        {
            double bin = frequency / binHz;
            int i0 = (int)Math.Floor(bin);
            int i1 = i0 + 1;
            i0 = Math.Clamp(i0, 0, maxBin - 1);
            i1 = Math.Clamp(i1, 0, maxBin - 1);
            float fraction = (float)(bin - i0);
            return new InterpolatedSample(i0, i1, fraction);
        }

        #endregion

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // recalculate clip
            var width = e.NewSize.Width;
            var height = e.NewSize.Height;
            ProgressClip.Rect = new Rect(0, 0, width, height);
            // recaculate point cache
            RebuildPointCache();
            DrawBars();
        }

        private readonly struct InterpolatedSample
        {
            public readonly int Bin0;
            public readonly int Bin1;
            public readonly float Fraction;

            public InterpolatedSample(int bin0, int bin1, float fraction)
            {
                Bin0 = bin0;
                Bin1 = bin1;
                Fraction = fraction;
            }
        }

        private readonly struct BandMapping(InterpolatedSample left, InterpolatedSample center, InterpolatedSample right)
        {
            public readonly InterpolatedSample Left = left;
            public readonly InterpolatedSample Center = center;
            public readonly InterpolatedSample Right = right;
        }
    }

}
