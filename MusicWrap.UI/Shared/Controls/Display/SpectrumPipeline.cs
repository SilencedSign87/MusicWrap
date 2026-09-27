
namespace MusicWrap.UI.Controls
{
    public sealed class SpectrumPipelineConfig
    {
        // ---- Frequencies / mapping ----
        public int BandCount { get; set; } = 8;
        public int BinCount { get; set; } = 16384;
        public int SampleRate { get; set; } = 44100;
        public float MinEqHz { get; set; } = 20f;
        public float MaxEqHz { get; set; } = 20000f;
        public float NyquistBias { get; set; } = 0.98f;    // % de Nyquist as ceiling

        // ---- Stage: Dynamic range (dB) ----
        public float NoiseFloorDb { get; set; } = -65f;
        public float CeilingDb { get; set; } = -0f;

        // ---- Stage: Noise gate ----
        public float NoiseGateNorm { get; set; } = 0.2f;   //  0..1 (0 = off) 

        // ---- Stage: boosting ----
        public float BassBoost { get; set; } = .95f;
        public float MidBoost { get; set; } = 1.2f;
        public float TrebleBoost { get; set; } = 1.65f;
        public float BassMaxHz { get; set; } = 200f;      // bass | mid boundary
        public float TrebleMinHz { get; set; } = 4000f;
        public float ZoneBlendOctaves { get; set; } = 0.5f; // widht of the crossfade; 0 = sharp

        // ---- Stage: Smoothing ----
        public float SmoothingAlpha { get; set; } = 0.8f;  // EMA (0 = ignore new , 1 = no smoothing)
        public float ChangeThreshold { get; set; } = 0.01f;// dead zone 0..1 (0 = off)

        // ---- Stage: Gamma and Contrast ----
        public float ContrastGamma { get; set; } = 1.5f;
    }

    public sealed class SpectrumPipeline
    {
        private readonly SpectrumPipelineConfig _cfg;

        private float[] _smoothed;
        private float[] _channelScratch = [];
        private BandMapping[] _bandMap = [];

        public SpectrumPipeline(SpectrumPipelineConfig config)
        {
            _cfg = config;
            _smoothed = new float[config.BandCount];
            RebuildBandMapping();
        }

        public int BandCount => _cfg.BandCount;

        public void OnConfigurationChanged(int sampleRate, int fftSize)
        {
            _cfg.SampleRate = sampleRate;
            _cfg.BinCount = fftSize;
            RebuildBandMapping();
            ResetSmoothing();
        }

        public void ResetSmoothing()
        {
            Array.Clear(_smoothed);
        }

        public void SetBandCount(int bandCount)
        {
            //_cfg.BandCount = Math.Max(bandCount, 1);
            //_smoothed = new float[_cfg.BandCount];

            int n = Math.Max(bandCount, 1);
            if (n == _cfg.BandCount)
                return;

            _cfg.BandCount = n;
            _smoothed = new float[n];

            RebuildBandMapping();
        }

        // channel 0 = left, 1 = right, -1 = mono (average)
        public float[] ProcessChannel(float[] interleavedMagnitudes, int channel)
        {
            //int fftSize = interleavedMagnitudes.Length / 2;
            //var channelData = new float[fftSize];
            int binCount = interleavedMagnitudes.Length / 2;

            if (_channelScratch.Length != binCount)
                _channelScratch = new float[binCount];

            if (channel < 0)
            {
                for (int i = 0; i < binCount; i++)
                {
                    int idx = i * 2;
                    _channelScratch[i] = (interleavedMagnitudes[idx] + interleavedMagnitudes[idx + 1]) * 0.5f;
                }
            }
            else
            {
                int startIndex = channel == 0 ? 0 : 1;
                for (int i = 0; i < binCount; i++)
                {
                    _channelScratch[i] = interleavedMagnitudes[startIndex + i * 2];
                }
            }

            return Process(_channelScratch);
        }

        public float[] Process(float[] magnitudes)
        {
            var raw = ExtractRawBands(magnitudes);
            var gated = ApplyGate(raw);
            
            var boosted = ApplyZoneBoost(gated);
            SmoothBands(boosted);
            var display = ApplyGamma(_smoothed);
            return display;
        }

        #region Stages

        private float[] ExtractRawBands(float[] spectrum)
        {
            int count = _cfg.BandCount;
            var result = new float[count];
            float span = Math.Max(1e-3f, _cfg.CeilingDb - _cfg.NoiseFloorDb);

            for (int i = 0; i < count; i++)
            {
                var map = _bandMap[i];

                float mLeft = InterpolateSpectrum(spectrum, map.Left);
                float mCenter = InterpolateSpectrum(spectrum, map.Center);
                float mRight = InterpolateSpectrum(spectrum, map.Right);

                float magnitude = (mLeft + 2f * mCenter + mRight) * 0.25f;

                float db = 20f * MathF.Log10(magnitude + 1e-8f);
                //result[i] = (db - _cfg.NoiseFloorDb) / (_cfg.CeilingDb - _cfg.NoiseFloorDb);
                result[i] = (db - _cfg.NoiseFloorDb) / span;
            }

            return result;
        }

        private float[] ApplyGate(float[] input)
        {
            var result = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                float norm = Math.Clamp(input[i], 0f, 1f);
                if (norm < _cfg.NoiseGateNorm)
                {
                    float ratio = norm / _cfg.NoiseGateNorm;
                    norm *= ratio;
                }
                result[i] = norm;
            }
            return result;
        }

        private float[] ApplyZoneBoost(float[] input)
        {
            var result = new float[input.Length];
            int count = input.Length;
            for (int i = 0; i < count; i++)
            {
                result[i] = input[i] * ResolveZoneBoost(_bandMap[i].CenterHz);
            }
            return result;
        }

        private float ResolveZoneBoost(float hz)
        {
            float toMid = SmoothRamp(hz, _cfg.BassMaxHz, _cfg.ZoneBlendOctaves);
            float toTreble = SmoothRamp(hz, _cfg.TrebleMinHz, _cfg.ZoneBlendOctaves);

            float bass = _cfg.BassBoost;
            float mid = bass + (_cfg.MidBoost - bass) * toMid;
            float treble = _cfg.MidBoost + (_cfg.TrebleBoost - _cfg.MidBoost) * toTreble;

            return mid + (treble - mid) * toTreble;
        }
        private static float SmoothRamp(float hz, float boundaryHz, float blendOctaves)
        {
            if (blendOctaves <= 0f)
                return hz < boundaryHz ? 0f : 1f;

            float t = (MathF.Log2(hz) - MathF.Log2(boundaryHz)) / blendOctaves + 0.5f;
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);   // smoothstep
        }

        private void SmoothBands(float[] raw)
        {
            if (_smoothed.Length != raw.Length)
            {
                _smoothed = new float[raw.Length];
            }

            for (int i = 0; i < raw.Length; i++)
            {
                float diff = raw[i] - _smoothed[i];
                if (MathF.Abs(diff) < _cfg.ChangeThreshold)
                    continue;
                _smoothed[i] += diff * _cfg.SmoothingAlpha;
            }
        }

        private float[] ApplyGamma(float[] input)
        {
            float gamma = _cfg.ContrastGamma;
            var result = new float[input.Length];

            for (int i = 0; i < input.Length; i++)
                result[i] = gamma == 1f ? input[i] : MathF.Pow(input[i], gamma);

            return result;
        }

        #endregion

        #region Frequency mapping

        private void RebuildBandMapping()
        {
            int count = _cfg.BandCount;
            _bandMap = new BandMapping[count];

            int usableBins = Math.Max(1, _cfg.BinCount / 2);
            float nyquist = _cfg.SampleRate * 0.5f;
            float binHz = nyquist / usableBins;

            if (binHz <= 0f)
                return;

            float minHz = Math.Max(_cfg.MinEqHz, binHz);
            float maxHz = Math.Min(_cfg.MaxEqHz, nyquist * _cfg.NyquistBias);
            if (maxHz <= minHz)
                maxHz = nyquist * _cfg.NyquistBias;

            double ratio = Math.Pow(maxHz / minHz, 1.0 / count);
            double lowHz = minHz;

            for (int i = 0; i < count; i++)
            {
                double highHz = lowHz * ratio;
                double center = Math.Sqrt(lowHz * highHz);
                double left = Math.Sqrt(lowHz * center);
                double right = Math.Sqrt(center * highHz);
                _bandMap[i] = new BandMapping(
                    (float)center,
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

        private static float InterpolateSpectrum(float[] spectrum, InterpolatedSample sample)
        {
            return spectrum[sample.Bin0] * (1f - sample.Fraction) +
                   spectrum[sample.Bin1] * sample.Fraction;
        }

        #endregion

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

        private readonly struct BandMapping(float centerHz,InterpolatedSample left, InterpolatedSample center, InterpolatedSample right)
        {
            public readonly float CenterHz = centerHz;
            public readonly InterpolatedSample Left = left;
            public readonly InterpolatedSample Center = center;
            public readonly InterpolatedSample Right = right;
        }
    }
}
