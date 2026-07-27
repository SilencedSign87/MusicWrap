using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        private float[] _currentHeights = Array.Empty<float>();
        private float[] _peakHold = Array.Empty<float>();
        private bool _isActive;

        private const int AssumedSampleRate = 44100;
        private const float Amplification = 1.0f;
        private const float RiseSpeed = 0.6f;
        private const float FallSpeed = 0.35f;
        private const float PeakDecay = 0.03f;
        private const float HeightDecay = 0.1f;

        private const float MinEqHz = 10f;
        private const float MaxEqHz = 18000f;
        private const float NoiseFloorDb = -72f;
        private const float CeilingDb = -12f;
        private const float EqGamma = 1.0f;

        private const int MaxFftSize = 16384;
        private const int MinFftSize = 256; // 44.1kHz

        private readonly float[] _pcmBuffer = new float[MaxFftSize * 2]; // stereo
        private float[] _fftInput = Array.Empty<float>();
        private Complex[] _fftComplex = Array.Empty<Complex>();
        private float[] _magnitudes = Array.Empty<float>();


        public BarVisualizerControl()
        {
            InitializeComponent();

            _musicService = App.Services.GetRequiredService<IMusicPlayerService>();

            _timer = new DispatcherTimer
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
            //Debug.WriteLine($"[Visualizer] Captured Floats: {capturedFloats}");
            if (capturedFloats <= 0)
            {
                DecayAll();
                return;
            }

            int monoAvailable = capturedFloats / 2;
            if (monoAvailable < MinFftSize)
            {
                DecayAll();
                return;
            }

            int targetFft = ChooseTargetFftSize(BarCount);
            int fftSize = Math.Min(targetFft, prevPow2(monoAvailable));
            fftSize = Math.Clamp(fftSize, MinFftSize, MaxFftSize);

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

            float[] bands = MapFFTToBands(_magnitudes, BarCount, AssumedSampleRate);
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

        private static float[] MapFFTToBands(float[] spectrum, int bandCount, int sampleRate)
        {
            var result = new float[bandCount];
            if (spectrum.Length == 0 || bandCount <= 0)
                return result;
            int usableBins = spectrum.Length;
            float nyquist = sampleRate * 0.5f;
            float binHz = nyquist / usableBins;
            if (binHz <= 0f) return result;
            float minHz = MathF.Max(MinEqHz, binHz);
            float maxHz = MathF.Min(MaxEqHz, nyquist * 0.98f);
            if (maxHz <= minHz) maxHz = nyquist * 0.98f;
            // Mel scale mapping
            float melMin = HzToMel(minHz);
            float melMax = HzToMel(maxHz);
            float melSpan = melMax - melMin;
            for (int i = 0; i < bandCount; i++)
            {
                float t0 = (float)i / bandCount;
                float t1 = (float)(i + 1) / bandCount;
                float lowHz = MelToHz(melMin + melSpan * t0);
                float highHz = MelToHz(melMin + melSpan * t1);
                int lowBin = Math.Clamp((int)(lowHz / binHz), 1, usableBins - 1);
                int highBin = Math.Clamp((int)(highHz / binHz), lowBin + 1, usableBins);
                double sumPower = 0.0;
                int count = 0;
                for (int b = lowBin; b < highBin; b++)
                {
                    double m = spectrum[b];
                    sumPower += m * m;
                    count++;
                }
                float rms = count > 0 ? (float)Math.Sqrt(sumPower / count) : 0f;
                float db = 20f * MathF.Log10(rms + 1e-8f);
                float norm = (db - NoiseFloorDb) / (CeilingDb - NoiseFloorDb);
                norm = Math.Clamp(norm, 0f, 1f);
                result[i] = MathF.Pow(norm, EqGamma);
            }
            return result;
        }
        private void DrawBars()
        {
            double width = BarContainer.ActualWidth;
            double height = BarContainer.ActualHeight;
            if (width <= 0 || height <= 0 || _currentHeights.Length == 0)
                return;

            int bandCount = _currentHeights.Length;
            double baseY = height;
            double stepX = bandCount > 1 ? width / (bandCount - 1) : width;

            var topPoints = new Point[bandCount];
            for (int i = 0; i < bandCount; i++)
            {
                double x = bandCount > 1 ? i * stepX : width * 0.5;
                double normalized = Math.Clamp(_currentHeights[i], 0f, 1f);
                double y = height - Math.Max(normalized * height, 1.0);

                topPoints[i] = new Point(x, y);
            }

            var fillGeometry = new StreamGeometry();
            using (var ctx = fillGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, baseY), true, true);
                ctx.LineTo(topPoints[0], true, false);

                for (int i = 1; i < topPoints.Length; i++)
                    ctx.LineTo(topPoints[i], true, false);

                ctx.LineTo(new Point(width, baseY), true, false);
            }
            fillGeometry.Freeze();

            var topGeometry = new StreamGeometry();
            using (var ctx = topGeometry.Open())
            {
                ctx.BeginFigure(topPoints[0], false, false);
                for (int i = 1; i < topPoints.Length; i++)
                    ctx.LineTo(topPoints[i], true, false);
            }
            topGeometry.Freeze();

            SpectrumFillPath.Data = fillGeometry;
            SpectrumFillPath.Fill = CreateSpectrumFillBrush();

            SpectrumTopPath.Data = topGeometry;
            SpectrumTopPath.Stroke = CreateSpectrumTopBrush();
            SpectrumTopPath.StrokeThickness = 2.0;
            SpectrumTopPath.StrokeStartLineCap = PenLineCap.Round;
            SpectrumTopPath.StrokeEndLineCap = PenLineCap.Round;
            SpectrumTopPath.StrokeLineJoin = PenLineJoin.Round;

            ProgressClip.Rect = new Rect(0, 0, width, height);
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
        private static float HzToMel(float hz)
        {
            return 2595f * MathF.Log10(1f + hz / 700f);
        }
        private static float MelToHz(float mel)
        {
            return 700f * (MathF.Pow(10f, mel / 2595f) - 1f);
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

        private static int ChooseTargetFftSize(int barCount)
        {
            int target = NextPow2(Math.Max(MinFftSize, barCount * 64));
            return Math.Clamp(target, MinFftSize, MaxFftSize);
        }

        private static int NextPow2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        private static int prevPow2(int v)
        {
            int p = 1;
            while ((p << 1) <= v) p <<= 1;
            return p;
        }

        #endregion

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawBars();
        }

    }
}
