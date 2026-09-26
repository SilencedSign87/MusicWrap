using System.Runtime.Intrinsics.X86;
using System.Windows.Media;

namespace MusicWrap.UI.Controls;

public sealed class CenteredSpectrumPipelineConfig
{
    //  FFT / frequency range

    public int FftSize { get; set; } = 16384;
    public int SampleRate { get; set; } = 44100;

    public int BarCount { get; set; } = 80;

    /// <summary>
    /// Lower bound of the analyzed spectrum.
    /// </summary>
    public float MinHz { get; set; } = 30f;

    /// <summary>
    /// Upper bound of the analyzed spectrum (the outer edges).
    /// </summary>
    public float MaxHz { get; set; } = 16000f;

    /// <summary>
    /// Split point between the central bass band and the outer spectrum.
    /// Everything from MinHz to CenterHz is merged into a single value
    /// that sits at the center of the graph. From CenterHz to MaxHz the
    /// spectrum expands normally toward the outer edges.
    /// </summary>
    public float CenterHz { get; set; } = 150f;

    //  Dynamic range (dB)

    public float NoiseFloorDb { get; set; } = -80f;
    public float CeilingDb { get; set; } = -0f;

    //  Noise gate

    public float NoiseGateNorm { get; set; } = 0.05f;

    //  Smoothing

    public float SmoothingAlpha { get; set; } = 1.0f;
    public float ChangeThreshold { get; set; } = 0.0f;

    // Tilt

    public float TiltDb { get; set; } = 4.5f;
    public float CenterTrimDb { get; set; } = -3f;


    //  Mirroring

    public float EdgeSpreadPower { get; set; } = 0.5f;

    /// <summary>
    /// How the whole central band (MinHz to CenterHz) is merged into the
    /// single center value of the graph. Only affects the center point.
    /// </summary>
    public CenteredAggregation Aggregation { get; set; } = CenteredAggregation.Rms;
}


public enum CenteredAggregation
{
    Max,
    Average,
    Rms
}

internal sealed class ChannelState
{
    public float[] Analysis = [];
    public float[] Smoothed = [];
    public float[] Scratch = [];
}

public sealed class CenteredSpectrumPipeline
{
    private const int MinimumAnalysisBands = 128;
    private const int ChannelCount = 2;

    private readonly ChannelState[] _channels = [new ChannelState(), new ChannelState()];

    private CenteredSpectrumPipelineConfig _config;

    private float[] _bandFrequencies = [];
    private float[] _bandLowHz = [];
    private float[] _bandHighHz = [];
    private float[] _tiltGains = [];
    private float[] _radialFrequencies = [];

    private float[] _radialValues = [];
    private float[] _output = [];

    private int _sampleRate;
    private int _fftSize;
    private int _bandCount;

    private float[] _analysisValues = [];
    private float[] _smoothed = [];

    private float _centerValue;
    private float _dbRange;

    public CenteredSpectrumPipeline(CenteredSpectrumPipelineConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _sampleRate = Math.Max(1, config.SampleRate);
        _fftSize = Math.Max(2, config.FftSize);
        _bandCount = Math.Max(3, config.BarCount);

        EnsureOddBandCount();
        RebuildAnalysisBuffers();
    }

    public int BandCount => _bandCount;

    public void SetBandCount(int bandCount)
    {
        _bandCount = Math.Max(3, bandCount);
        EnsureOddBandCount();

        RebuildAnalysisBuffers();
    }

    public void OnConfigurationChanged(int sampleRate, int fftSize)
    {
        _sampleRate = Math.Max(1, sampleRate);
        _fftSize = Math.Max(2, fftSize);

        _config.SampleRate = _sampleRate;
        _config.FftSize = _fftSize;

        RebuildAnalysisBuffers();
    }

    public float[] Process(float[] magnitudes, int channelCount = 1)
    {
        if (magnitudes == null || magnitudes.Length == 0)
            return _output;

        if (_channels[0].Analysis.Length == 0)
            RebuildAnalysisBuffers();

        int channels = Math.Clamp(channelCount, 1, ChannelCount);
        int bins = Math.Min(magnitudes.Length / channels, _fftSize / 2);

        for (int c = 0; c < ChannelCount; c++)
        {
            int source = Math.Min(c, channels - 1);
            DeinterLeave(magnitudes, bins, channels, source, _channels[c].Scratch);

            BuildLogAnalysisBands(_channels[c]);
            NormalizeToDb(_channels[c]);
            ApplyGate(_channels[c]);
            ApplyTilt(_channels[c]);
            ApplyEmaSmoothing(_channels[c]);
        }

        ComputeCenterValue(magnitudes, bins, channels);
        BuildCenteredSpectrum();

        return _output;
    }

    // --------------------------------------------------------------------
    // Configuration / buffers
    // --------------------------------------------------------------------

    private void EnsureOddBandCount()
    {
        if ((_bandCount & 1) == 0)
            _bandCount++;
    }

    private static void DeinterLeave(float[] magnitudes, int bins, int channels, int source, float[] dest)
    {
        if (channels == 1)
        {
            Array.Copy(magnitudes, dest, bins);
            return;
        }
        for (int i = 0; i < bins; i++)
        {
            dest[i] = magnitudes[(i * channels) + source];
        }
    }

    private void RebuildAnalysisBuffers()
    {
        int count = Math.Max(MinimumAnalysisBands, _bandCount * 3);
        int half = (_bandCount + 1) / 2;
        int scratch = _fftSize / 2;

        for (int c = 0; c < ChannelCount; c++)
        {
            if (_channels[c].Analysis.Length != count)
            {
                _channels[c].Analysis = new float[count];
                _channels[c].Smoothed = new float[count];
            }

            if (_channels[c].Scratch.Length != scratch)
                _channels[c].Scratch = new float[scratch];
        }

        if (_bandFrequencies.Length != count)
        {
            _bandFrequencies = new float[count];
            _bandLowHz = new float[count];
            _bandHighHz = new float[count];
            _tiltGains = new float[count];
        }

        if (_radialFrequencies.Length != half)
            _radialFrequencies = new float[half];

        if (_radialValues.Length != half)
            _radialValues = new float[half];

        if (_output.Length != _bandCount)
            _output = new float[_bandCount];

        _dbRange = ComputeDbRange();

        BuildFrequencyTable();
        BuildBandEdges();
        BuildTiltTable();
        BuildRadialFrequencies();
    }

    private void BuildFrequencyTable()
    {
        int count = _bandFrequencies.Length;

        float minHz = Math.Max(1f, _config.MinHz);

        float nyquist = _sampleRate * 0.5f;

        float maxHz = _config.MaxHz > 0
            ? Math.Min(_config.MaxHz, nyquist)
            : nyquist;

        if (maxHz <= minHz)
            maxHz = Math.Max(minHz + 1f, nyquist);

        double minLog = Math.Log(minHz);
        double maxLog = Math.Log(maxHz);

        for (int i = 0; i < count; i++)
        {
            double t = count == 1
                ? 0
                : (double)i / (count - 1);

            double logFrequency =
                minLog + (maxLog - minLog) * t;

            _bandFrequencies[i] =
                (float)Math.Exp(logFrequency);
        }
    }

    /// <summary>
    /// Extracts the outer spectrum (CenterHz to MaxHz) as a normal
    /// per-band spectrum, plus the full range table used for sampling.
    /// </summary>
    private void BuildLogAnalysisBands(ChannelState ch)
    {
        float[] a = ch.Analysis;
        float[] scratch = ch.Scratch;

        for (int i = 0; i < a.Length; i++)
            a[i] = SampleBand(scratch, _bandFrequencies[i], _bandLowHz[i], _bandHighHz[i]);
    }

    /// <summary>
    /// Normal spectrum extraction: a single magnitude value sampled around
    /// the band's center frequency (like SpectrumPipeline), not a wide
    /// aggregated range. The central band is the only one that aggregates.
    /// </summary>
    private float SampleBand(
        float[] magnitudes,
        float centerHz,
        float lowHz,
        float highHz)
    {
        float centerValue =
            InterpolateMagnitude(
                magnitudes,
                centerHz);

        float leftValue =
            InterpolateMagnitude(
                magnitudes,
                lowHz);

        float rightValue =
            InterpolateMagnitude(
                magnitudes,
                highHz);

        return (leftValue + 2f * centerValue + rightValue) * 0.25f;
    }

    private float InterpolateMagnitude(
        float[] magnitudes,
        float frequency)
    {
        int fftBinCount = Math.Min(
            magnitudes.Length,
            Math.Max(1, _fftSize / 2));

        float binHz =
            _sampleRate * 0.5f / fftBinCount;

        double binPosition =
            Math.Max(frequency, 1f) / binHz;

        int bin0 = (int)Math.Floor(binPosition);
        int bin1 = bin0 + 1;

        bin0 = Clamp(bin0, 0, fftBinCount - 1);
        bin1 = Clamp(bin1, 0, fftBinCount - 1);

        float fraction = (float)(binPosition - bin0);

        return magnitudes[bin0] * (1f - fraction) +
               magnitudes[bin1] * fraction;
    }

    // --------------------------------------------------------------------
    // Processing stages
    // --------------------------------------------------------------------

    private void NormalizeToDb(ChannelState ch)
    {
        const float epsilon = 1e-8f;

        float floor = _config.NoiseFloorDb;
        float inv = _dbRange;

        for (int i = 0; i < ch.Analysis.Length; i++)
        {
            float magnitude = Math.Max(0f, ch.Analysis[i]);
            float db = 20f * MathF.Log10(magnitude + epsilon);
            ch.Analysis[i] = (db - floor) * inv;
        }
    }

    private void ApplyGate(ChannelState ch)
    {
        float gate = Math.Clamp(_config.NoiseGateNorm, 0f, 1f);
        if (gate <= 0f) return;

        for (int i = 0; i < ch.Analysis.Length; i++)
        {
            float norm = Math.Clamp(ch.Analysis[i], 0f, 1f);

            if (norm < gate)
            {
                float ratio = norm / gate;
                norm *= ratio;
            }

            ch.Analysis[i] = norm;
        }
    }

    private float MinHz() => MathF.Max(1f, _config.MinHz);
    private float MaxHz() => _config.MaxHz > 0 ? MathF.Min(_config.MaxHz, _sampleRate * 0.5f) : _sampleRate * 0.5f;

    private float ComputeDbRange()
    {
        float floor = _config.NoiseFloorDb;
        float ceiling = _config.CeilingDb;

        if (ceiling <= floor) ceiling = floor + 1f;

        return 1f / (ceiling - floor);
    }

    private void BuildBandEdges()
    {
        int count = _bandFrequencies.Length;
        float minHz = MinHz();
        float maxHz = MathF.Max(minHz, MaxHz());

        for (int i = 0; i < count; i++)
        {
            float c = _bandFrequencies[i];
            float lo = i == 0 ? minHz : GeometricMean(_bandFrequencies[i - 1], c);
            float hi = i == count - 1 ? maxHz : GeometricMean(c, _bandFrequencies[i + 1]);

            _bandLowHz[i] = MathF.Max(minHz, lo);
            _bandHighHz[i] = MathF.Min(maxHz, hi);
        }
    }

    private void BuildTiltTable()
    {
        int count = _bandFrequencies.Length;
        float tiltDb = _config.TiltDb;

        if (MathF.Abs(tiltDb) <= 0.01f)
        {
            Array.Fill(_tiltGains, 1f);
            return;
        }

        double pivot = Math.Log(Math.Max(1f, _config.CenterHz));
        double span = Math.Log(MathF.Max(MinHz(), MaxHz())) - pivot;

        if (Math.Abs(span) < 1e-9)
        {
            Array.Fill(_tiltGains, 1f);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            double t = (Math.Log(_bandFrequencies[i]) - pivot) / span;
            _tiltGains[i] = (float)Math.Pow(10.0, tiltDb * t / 20.0);
        }
    }

    private void BuildRadialFrequencies()
    {
        int half = _radialFrequencies.Length;

        double pivot = Math.Log(MathF.Max(1f, _config.CenterHz));
        double span = Math.Log(MathF.Max(MinHz(), MaxHz())) - pivot;
        double power = Math.Max(0.05, _config.EdgeSpreadPower);

        for (int radial = 0; radial < half; radial++)
        {
            if (radial == 0) { _radialFrequencies[0] = 0f; continue; }

            double t = half <= 1 ? 1.0 : (double)radial / (half - 1);
            _radialFrequencies[radial] = (float)Math.Exp(pivot + span * Math.Pow(t, power));
        }
    }

    private void ApplyTilt(ChannelState ch)
    {
        if (MathF.Abs(_config.TiltDb) <= 0.01f)
            return;

        float[] gains = _tiltGains;
        float[] a = ch.Analysis;

        if (gains.Length != a.Length)
            return;

        for (int i = 0; i < a.Length; i++)
            a[i] *= gains[i];
    }

    private void ApplyEmaSmoothing(ChannelState ch)
    {
        if (_config.SmoothingAlpha >= 1f && _config.ChangeThreshold <= 0f)
        {
            return;
        }

        float[] a = ch.Analysis;
        float[] s = ch.Smoothed;
        float alpha = _config.SmoothingAlpha;
        float threshold = _config.ChangeThreshold;

        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - s[i];

            if (MathF.Abs(diff) < threshold)
            {
                a[i] = s[i];
                continue;
            }

            s[i] += diff * alpha;
            a[i] = s[i];
        }
    }

    // --------------------------------------------------------------------
    // Central band aggregation
    // --------------------------------------------------------------------

    private void ComputeCenterValue(float[] magnitudes, int bins, int channels)
    {
        float left = AggregateLowBand(magnitudes, bins, channels, 0);

        float right = channels > 1 ? AggregateLowBand(magnitudes, bins, channels, 1) : left;

        float raw = (left + right) * 0.5f;

        float normalized = NormalizeSingleToDb(raw);
        normalized = ApplySingleGate(normalized);
        normalized = Math.Clamp(normalized + _config.CenterTrimDb * _dbRange, 0f, 1f);

        _centerValue = SmoothSingle(normalized, _config.SmoothingAlpha);
    }
    private float AggregateLowBand(float[] magnitudes, int bins, int channels, int channel)
    {
        float binHz = _sampleRate * 0.5f / bins;

        int lowBin = Clamp((int)MathF.Floor(MinHz() / binHz), 0, bins - 1);
        int highBin = Clamp(
            (int)MathF.Ceiling(MathF.Max(MinHz(), _config.CenterHz) / binHz),
            lowBin, bins - 1);

        float max = 0f;
        double sum = 0, sumSq = 0;

        for (int i = lowBin; i <= highBin; i++)
        {
            float v = MathF.Max(0f, magnitudes[(i * channels) + channel]);
            if (v > max) max = v;
            sum += v;
            sumSq += v * v;
        }

        int n = highBin - lowBin + 1;

        return _config.Aggregation switch
        {
            CenteredAggregation.Max => max,
            CenteredAggregation.Rms => (float)Math.Sqrt(sumSq / n),
            _ => (float)(sum / n),
        };
    }

    private float NormalizeSingleToDb(float magnitude)
    {
        const float epsilon = 1e-8f;

        float floor = _config.NoiseFloorDb;
        float ceiling = _config.CeilingDb;

        if (ceiling <= floor)
            ceiling = floor + 1f;

        float db = 20f * MathF.Log10(Math.Max(0f, magnitude) + epsilon);
        return (db - floor) / (ceiling - floor);
    }

    private float ApplySingleGate(float normalized)
    {
        float gate = Math.Clamp(_config.NoiseGateNorm, 0f, 1f);
        if (gate <= 0f) return normalized;

        float norm = Math.Clamp(normalized, 0f, 1f);

        if (norm < gate)
        {
            float ratio = norm / gate;
            norm *= ratio;
        }

        return norm;
    }

    private float SmoothSingle(float value, float alpha)
    {
        float diff = value - _centerValue;

        if (MathF.Abs(diff) < _config.ChangeThreshold)
            return _centerValue;

        return _centerValue + diff * alpha;
    }

    // --------------------------------------------------------------------
    // Mirrored spectrum building
    // --------------------------------------------------------------------

    private void BuildCenteredSpectrum()
    {
        int half = _radialValues.Length;
        int center = _bandCount / 2;

        _radialValues[0] = _centerValue;

        for (int ch = 0; ch < ChannelCount; ch++)
        {
            float[] analysis = _channels[ch].Analysis;

            for (int d = 1; d < half; d++)
                _radialValues[d] = SampleFrequency(_radialFrequencies[d], analysis);

            if (ch == 0)
            {
                for (int i = 0; i <= center; i++)
                    _output[i] = Clamp01(_radialValues[center - i]);
            }
            else
            {
                for (int i = center + 1; i < _bandCount; i++)
                    _output[i] = Clamp01(_radialValues[i - center]);
            }
        }
    }

    private float SampleFrequency(float frequency, float[] analysis)
    {
        if (frequency <= 0)
            return 0f;

        int count = _bandFrequencies.Length;

        if (count == 0)
            return 0f;

        if (frequency <= _bandFrequencies[0])
            return analysis[0];

        if (frequency >= _bandFrequencies[count - 1])
            return analysis[count - 1];

        int lo = 0;
        int hi = count - 1;

        while (hi - lo > 1)
        {
            int mid = lo + ((hi - lo) >> 1);

            if (_bandFrequencies[mid] < frequency)
                lo = mid;
            else
                hi = mid;
        }

        float f1 = _bandFrequencies[lo];
        float f2 = _bandFrequencies[hi];

        float v1 = analysis[lo];
        float v2 = analysis[hi];

        double logF =
            Math.Log(frequency);

        double logF1 =
            Math.Log(Math.Max(1f, f1));

        double logF2 =
            Math.Log(Math.Max(1f, f2));

        double denominator =
            logF2 - logF1;

        if (Math.Abs(denominator) < 1e-12)
            return v1;

        float t =
            (float)((logF - logF1) / denominator);

        return v1 + (v2 - v1) * t;
    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------

    private static float GeometricMean(
        float a,
        float b)
    {
        a = Math.Max(0.0001f, a);
        b = Math.Max(0.0001f, b);

        return (float)Math.Sqrt(a * b);
    }

    private static int Clamp(
        int value,
        int min,
        int max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }

    private static float Clamp01(float value)
    {
        if (value <= 0f)
            return 0f;

        if (value >= 1f)
            return 1f;

        return value;
    }
}
