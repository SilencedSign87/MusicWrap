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
    public float MinHz { get; set; } = 40f;

    /// <summary>
    /// Upper bound of the analyzed spectrum (the outer edges).
    /// </summary>
    public float MaxHz { get; set; } = 20000f;

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

    //  Per-zone boosts (bass / mid / treble)

    public float BassBoost { get; set; } = 1.0f;
    public float MidBoost { get; set; } = 1.3f;
    public float TrebleBoost { get; set; } = 1.6f;

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

public sealed class CenteredSpectrumPipeline
{
    private const int MinimumAnalysisBands = 128;

    private CenteredSpectrumPipelineConfig _config;

    private int _sampleRate;
    private int _fftSize;
    private int _bandCount;

    private float[] _analysisValues = Array.Empty<float>();
    private float[] _smoothed = Array.Empty<float>();

    private float[] _bandFrequencies = Array.Empty<float>();

    private float[] _radialValues = Array.Empty<float>();
    private float[] _output = Array.Empty<float>();

    private float _centerValue;

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

    public float[] Process(float[] magnitudes)
    {
        if (magnitudes == null || magnitudes.Length == 0)
            return _output;

        if (_analysisValues.Length == 0)
            RebuildAnalysisBuffers();

        BuildLogAnalysisBands(magnitudes);

        NormalizeToDb();
        ApplyGate();
        ApplyZoneBoosts();
        ApplyEmaSmoothing();

        ComputeCenterValue(magnitudes);

        BuildMirroredSpectrum();

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

    private void RebuildAnalysisBuffers()
    {
        int count = Math.Max(MinimumAnalysisBands, _bandCount * 3);

        if (_analysisValues.Length != count)
        {
            _analysisValues = new float[count];
            _smoothed = new float[count];
            _bandFrequencies = new float[count];
        }

        if (_radialValues.Length != (_bandCount + 1) / 2)
            _radialValues = new float[(_bandCount + 1) / 2];

        if (_output.Length != _bandCount)
            _output = new float[_bandCount];

        BuildFrequencyTable();
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
    private void BuildLogAnalysisBands(float[] magnitudes)
    {
        int count = _analysisValues.Length;

        float minHz = Math.Max(1f, _config.MinHz);

        float nyquist = _sampleRate * 0.5f;

        float maxHz = _config.MaxHz > 0
            ? Math.Min(_config.MaxHz, nyquist)
            : nyquist;

        if (maxHz <= minHz)
            maxHz = Math.Max(minHz + 1f, nyquist);

        for (int i = 0; i < count; i++)
        {
            float centerHz = _bandFrequencies[i];

            float lowHz;
            float highHz;

            if (i == 0)
            {
                lowHz = minHz;
            }
            else
            {
                lowHz =
                    GeometricMean(
                        _bandFrequencies[i - 1],
                        centerHz);
            }

            if (i == count - 1)
            {
                highHz = maxHz;
            }
            else
            {
                highHz =
                    GeometricMean(
                        centerHz,
                        _bandFrequencies[i + 1]);
            }

            lowHz = Math.Max(minHz, lowHz);
            highHz = Math.Min(maxHz, highHz);

            _analysisValues[i] =
                SampleBand(
                    magnitudes,
                    centerHz,
                    lowHz,
                    highHz);
        }
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

    private void NormalizeToDb()
    {
        const float epsilon = 1e-8f;

        float floor = _config.NoiseFloorDb;
        float ceiling = _config.CeilingDb;

        if (ceiling <= floor)
            ceiling = floor + 1f;

        for (int i = 0; i < _analysisValues.Length; i++)
        {
            float magnitude = Math.Max(0f, _analysisValues[i]);
            float db = 20f * MathF.Log10(magnitude + epsilon);
            _analysisValues[i] = (db - floor) / (ceiling - floor);
        }
    }

    private void ApplyGate()
    {
        float gate = Math.Clamp(_config.NoiseGateNorm, 0f, 1f);
        if (gate <= 0f) return;

        for (int i = 0; i < _analysisValues.Length; i++)
        {
            float norm = Math.Clamp(_analysisValues[i], 0f, 1f);

            if (norm < gate)
            {
                float ratio = norm / gate;
                norm *= ratio;
            }

            _analysisValues[i] = norm;
        }
    }

    private void ApplyZoneBoosts()
    {
        float midStart = Math.Max(1f, _config.CenterHz);
        float trebleStart = Math.Max(midStart, 8000f);

        for (int i = 0; i < _analysisValues.Length; i++)
        {
            float freq = _bandFrequencies[i];

            float boost = 1.0f;

            if (freq < midStart)
            {
                boost *= _config.BassBoost;
            }
            else if (freq < trebleStart)
            {
                boost *= _config.MidBoost;
            }
            else
            {
                boost *= _config.TrebleBoost;
            }

            _analysisValues[i] *= boost;
        }
    }

    private void ApplyEmaSmoothing()
    {
        for (int i = 0; i < _analysisValues.Length; i++)
        {
            float diff =
                _analysisValues[i] - _smoothed[i];

            if (MathF.Abs(diff) < _config.ChangeThreshold)
            {
                _analysisValues[i] = _smoothed[i];
                continue;
            }

            _smoothed[i] += diff * _config.SmoothingAlpha;
            _analysisValues[i] = _smoothed[i];
        }
    }

    // --------------------------------------------------------------------
    // Central band aggregation
    // --------------------------------------------------------------------

    /// <summary>
    /// Aggregates the whole central band (MinHz to CenterHz) into a single
    /// value. This is the only place Aggregation mode is applied.
    /// </summary>
    private void ComputeCenterValue(float[] magnitudes)
    {
        float minHz = Math.Max(1f, _config.MinHz);
        float centerHz = Math.Max(minHz, _config.CenterHz);

        float nyquist = _sampleRate * 0.5f;
        float maxHz =
            _config.MaxHz > 0
                ? Math.Min(_config.MaxHz, nyquist)
                : nyquist;

        if (maxHz <= minHz)
            maxHz = Math.Max(minHz + 1f, nyquist);

        centerHz = Math.Min(centerHz, maxHz);

        int fftBinCount = Math.Min(
            magnitudes.Length,
            Math.Max(1, _fftSize / 2));

        float binHz =
            _sampleRate * 0.5f / fftBinCount;

        int lowBin = (int)Math.Floor(
            Math.Max(minHz, 1f) / binHz);

        int highBin = (int)Math.Ceiling(
            Math.Max(centerHz, 1f) / binHz);

        lowBin = Clamp(lowBin, 0, fftBinCount - 1);
        highBin = Clamp(highBin, lowBin, fftBinCount - 1);

        float raw;

        switch (_config.Aggregation)
        {
            case CenteredAggregation.Max:
                {
                    float max = 0f;

                    for (int i = lowBin; i <= highBin; i++)
                    {
                        float value = Math.Max(0f, magnitudes[i]);

                        if (value > max)
                            max = value;
                    }

                    raw = max;
                    break;
                }

            case CenteredAggregation.Rms:
                {
                    double sum = 0;
                    int n = 0;

                    for (int i = lowBin; i <= highBin; i++)
                    {
                        double value = Math.Max(0f, magnitudes[i]);
                        sum += value * value;
                        n++;
                    }

                    raw = n > 0
                        ? (float)Math.Sqrt(sum / n)
                        : 0f;
                    break;
                }

            default:
                {
                    double sum = 0;
                    int n = 0;

                    for (int i = lowBin; i <= highBin; i++)
                    {
                        sum += Math.Max(0f, magnitudes[i]);
                        n++;
                    }

                    raw = n > 0
                        ? (float)(sum / n)
                        : 0f;
                    break;
                }
        }

        // Normalize, gate, boost and smooth the center value the same way
        // as the rest of the spectrum.
        float normalized = NormalizeSingleToDb(raw);
        normalized = ApplySingleGate(normalized);
        normalized = Math.Clamp(normalized * _config.BassBoost, 0f, 1f);

        _centerValue = SmoothSingle(normalized, _config.SmoothingAlpha);
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

    private void BuildMirroredSpectrum()
    {
        int halfCount = (_bandCount + 1) / 2;

        if (_analysisValues.Length == 0)
        {
            Array.Clear(_output, 0, _output.Length);
            return;
        }

        float minHz =
            Math.Max(1f, _config.MinHz);

        float maxHz =
            _config.MaxHz > 0
                ? Math.Min(
                    _config.MaxHz,
                    _sampleRate * 0.5f)
                : _sampleRate * 0.5f;

        float centerHz =
            Math.Max(minHz, _config.CenterHz);

        if (maxHz <= centerHz)
            maxHz = Math.Max(centerHz + 1f, maxHz);

        /*
         * Radial 0 is the CENTER of the graph: it holds the single
         * aggregated bass value of the MinHz..CenterHz band.
         *
         * Radial k (k >= 1) is a normal spectrum point that expands
         * logarithmically from CenterHz outward to MaxHz. A frequency
         * sweep from 20..400Hz stays collapsed in the middle, and from
         * 400..MaxHz it travels smoothly toward the outer edges.
         */
        _radialValues[0] = _centerValue;

        double centerLog = Math.Log(centerHz);
        double maxLog = Math.Log(maxHz);

        double spreadPower =
            Math.Max(0.05, _config.EdgeSpreadPower);

        for (int radial = 1; radial < halfCount; radial++)
        {
            double t =
                halfCount <= 1
                    ? 1.0
                    : (double)radial / (halfCount - 1);

            double logT =
                Math.Pow(t, spreadPower);

            double logFrequency =
                centerLog + (maxLog - centerLog) * logT;

            float frequency =
                (float)Math.Exp(logFrequency);

            _radialValues[radial] =
                SampleFrequency(frequency);
        }

        int centerOutput =
            _bandCount / 2;

        for (int i = 0; i < _bandCount; i++)
        {
            int distance =
                Math.Abs(i - centerOutput);

            int radialIndex =
                Math.Min(
                    distance,
                    _radialValues.Length - 1);

            _output[i] =
                Clamp01(
                    _radialValues[radialIndex]);
        }
    }

    private float SampleFrequency(float frequency)
    {
        if (frequency <= 0)
            return 0f;

        int count = _bandFrequencies.Length;

        if (count == 0)
            return 0f;

        if (frequency <= _bandFrequencies[0])
            return _analysisValues[0];

        if (frequency >= _bandFrequencies[count - 1])
            return _analysisValues[count - 1];

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

        float v1 = _analysisValues[lo];
        float v2 = _analysisValues[hi];

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
