using System.Windows.Media.Media3D;

namespace MusicWrap.UI.Controls;

public sealed class CenteredSpectrumPipelineConfig
{
    //  FFT / frequency range 

    public int FftSize { get; set; } = 16384;
    public int SampleRate { get; set; } = 44100;

    public int BarCount { get; set; } = 80;

    public float MinHz { get; set; } = 20f;
    public float MaxHz { get; set; } = 20000f;

    // Nyquist bias
    public float NyquistBias { get; set; } = 0.98f;


    //  Dynamic range (dB) 

    public float NoiseFloorDb { get; set; } = -90f;
    public float CeilingDb { get; set; } = -30f;

    /// <summary>
    /// The normalized magnitude below which the visual is fully suppressed.
    /// 
    /// 0.0 = no noise gate
    /// </summary>
    public float NoiseGateNorm { get; set; } = 0.09f;


    /// <summary>
    /// How the low-frequency region is compressed to allocate more visual resolution.
    /// </summary>
    public CenteredAggregation Aggregation { get; set; } = CenteredAggregation.Rms;


    /// <summary>
    /// How aggressively the visual contrast is applied to the normalized magnitude.
    /// 
    /// 1.0 = no change
    /// 2.0 = twice as much contrast
    /// 
    /// </summary>
    public float Gamma { get; set; } = 1.0f;


    /// <summary>
    /// How the radial distance from the center expands toward
    /// the edges.
    ///
    /// 1.0 = linear octave span (edges could reach far into
    ///       inactive ultra-low / ultra-high frequencies).
    /// lower = more radial resolution is concentrated near the
    ///         active center, so the edges stay visually alive.
    /// </summary>
    public float EdgeSpreadPower { get; set; } = 0.9f;


    /// <summary>
    /// How aggressively the center jumps toward the most active
    /// frequency.
    ///
    /// 0 = very sticky, only moves for a much stronger signal and
    ///     a long hold.
    /// 1 = very responsive, reacts to nearby bands quickly.
    /// </summary>
    public float CenterSensitivity { get; set; } = 0.70f;
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

    // Temporal behavior of the center selector.
    // These are intentionally independent from the visual Attack/Release.
    private const double PeakMemorySeconds = 0.90;
    private const double ActivityMemorySeconds = 0.35;
    private const double CenterHoldSeconds = 0.65;

    // Avoid constantly selecting a neighboring band.
    private const float MinimumCenterDistanceOctaves = 0.20f;

    private CenteredSpectrumPipelineConfig _config;

    private int _sampleRate;
    private int _fftSize;
    private int _bandCount;

    private float[] _analysisValues = Array.Empty<float>();
    private float[] _previousValues = Array.Empty<float>();
    private float[] _peakAverage = Array.Empty<float>();
    private float[] _activityAverage = Array.Empty<float>();

    private float[] _bandFrequencies = Array.Empty<float>();

    private float[] _radialValues = Array.Empty<float>();
    private float[] _output = Array.Empty<float>();

    private int _centerBandIndex = -1;
    private int _candidateCenterBandIndex = -1;

    private double _candidateTime;
    private long _lastTimestamp;

    private float _centerFrequency;
    private bool _initialized;

    public CenteredSpectrumPipeline(CenteredSpectrumPipelineConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _sampleRate = Math.Max(1, config.SampleRate);
        _fftSize = Math.Max(2, config.FftSize);
        _bandCount = Math.Max(3, config.BarCount);

        EnsureOddBandCount();
        RebuildAnalysisBuffers();
    }

    /// <summary>
    /// Changes the number of output points/bars.
    ///
    /// For a true center point the count is forced to odd.
    /// Example: 80 -> 81.
    /// </summary>
    public void SetBandCount(int bandCount)
    {
        _bandCount = Math.Max(3, bandCount);
        EnsureOddBandCount();

        _radialValues = new float[(_bandCount + 1) / 2];
        _output = new float[_bandCount];
    }

    /// <summary>
    /// Called whenever the audio configuration changes.
    /// </summary>
    public void OnConfigurationChanged(int sampleRate, int fftSize)
    {
        _sampleRate = Math.Max(1, sampleRate);
        _fftSize = Math.Max(2, fftSize);

        _config.SampleRate = _sampleRate;
        _config.FftSize = _fftSize;

        _centerBandIndex = -1;
        _candidateCenterBandIndex = -1;
        _candidateTime = 0;
        _centerFrequency = 0;
        _initialized = false;

        RebuildAnalysisBuffers();
    }

    /// <summary>
    /// Processes FFT magnitudes and returns the complete mirrored graph.
    ///
    /// The returned array has exactly BarCount points:
    ///
    ///     [outer ... inner ... CENTER ... inner ... outer]
    ///
    /// The caller can pass this directly to UpdateHeights().
    /// </summary>
    public float[] Process(float[] magnitudes)
    {
        if (magnitudes == null || magnitudes.Length == 0)
            return _output;

        if (_analysisValues.Length == 0)
            RebuildAnalysisBuffers();

        double dt = GetDeltaTime();

        BuildLogAnalysisBands(magnitudes);

        UpdateTemporalStatistics(dt);

        SelectCenter(dt);

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

        _analysisValues = new float[count];
        _previousValues = new float[count];
        _peakAverage = new float[count];
        _activityAverage = new float[count];

        _bandFrequencies = new float[count];

        _radialValues = new float[(_bandCount + 1) / 2];
        _output = new float[_bandCount];

        BuildFrequencyTable();

        _centerBandIndex = -1;
        _candidateCenterBandIndex = -1;
        _candidateTime = 0;
        _centerFrequency = 0;
        _initialized = false;
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

        double minLog = Math.Log(minHz);
        double maxLog = Math.Log(maxHz);

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
                AggregateFrequencyRange(
                    magnitudes,
                    lowHz,
                    highHz,
                    minLog,
                    maxLog);
        }
    }

    private float AggregateFrequencyRange(
        float[] magnitudes,
        float lowHz,
        float highHz,
        double minLog,
        double maxLog)
    {
        int fftBinCount = Math.Min(
            magnitudes.Length,
            Math.Max(1, _fftSize / 2));

        double lowT =
            (Math.Log(Math.Max(lowHz, 1f)) - minLog) /
            Math.Max(1e-12, maxLog - minLog);

        double highT =
            (Math.Log(Math.Max(highHz, 1f)) - minLog) /
            Math.Max(1e-12, maxLog - minLog);

        int lowBin = (int)Math.Floor(
            lowT * (fftBinCount - 1));

        int highBin = (int)Math.Ceiling(
            highT * (fftBinCount - 1));

        lowBin = Clamp(lowBin, 0, fftBinCount - 1);
        highBin = Clamp(highBin, lowBin, fftBinCount - 1);

        if (highBin == lowBin)
            return Math.Max(0f, magnitudes[lowBin]);

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

                    return max;
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

                    return n > 0
                        ? (float)Math.Sqrt(sum / n)
                        : 0f;
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

                    return n > 0
                        ? (float)(sum / n)
                        : 0f;
                }
        }
    }

    // --------------------------------------------------------------------
    // Peak + activity tracking
    // --------------------------------------------------------------------

    private void UpdateTemporalStatistics(double dt)
    {
        float peakAlpha = TimeAlpha(
            dt,
            PeakMemorySeconds);

        float activityAlpha = TimeAlpha(
            dt,
            ActivityMemorySeconds);

        for (int i = 0; i < _analysisValues.Length; i++)
        {
            float current = _analysisValues[i];

            if (!_initialized)
            {
                _previousValues[i] = current;
                _peakAverage[i] = current;
                _activityAverage[i] = 0f;
                continue;
            }

            float delta =
                Math.Abs(current - _previousValues[i]);

            _peakAverage[i] +=
                (current - _peakAverage[i]) * peakAlpha;

            _activityAverage[i] +=
                (delta - _activityAverage[i]) * activityAlpha;

            _previousValues[i] = current;
        }
    }

    // --------------------------------------------------------------------
    // Dynamic center selection
    // --------------------------------------------------------------------

    private void SelectCenter(double dt)
    {
        if (_analysisValues.Length == 0)
            return;

        int candidate = FindBestCenterCandidate();

        if (!_initialized || _centerBandIndex < 0)
        {
            _centerBandIndex = candidate;
            _centerFrequency =
                _bandFrequencies[_centerBandIndex];

            _candidateCenterBandIndex = -1;
            _candidateTime = 0;
            _initialized = true;

            return;
        }

        if (candidate == _centerBandIndex)
        {
            _candidateCenterBandIndex = -1;
            _candidateTime = 0;

            _centerFrequency =
                _bandFrequencies[_centerBandIndex];

            return;
        }

        float currentScore =
            GetCenterScore(_centerBandIndex);

        float candidateScore =
            GetCenterScore(candidate);

        float distanceOctaves =
            FrequencyDistanceOctaves(
                _bandFrequencies[_centerBandIndex],
                _bandFrequencies[candidate]);

        bool sufficientlyDifferent =
            distanceOctaves >=
            MinimumCenterDistanceOctaves;

        float centerHysteresis =
            1.0f + (1.0f - Clamp01(_config.CenterSensitivity)) * 0.40f;

        bool clearlyBetter =
            candidateScore >
            currentScore * centerHysteresis;

        if (!sufficientlyDifferent || !clearlyBetter)
        {
            _candidateCenterBandIndex = -1;
            _candidateTime = 0;

            return;
        }

        if (_candidateCenterBandIndex != candidate)
        {
            _candidateCenterBandIndex = candidate;
            _candidateTime = 0;
        }

        _candidateTime += dt;

        if (_candidateTime >= CenterHoldSeconds)
        {
            _centerBandIndex =
                _candidateCenterBandIndex;

            _centerFrequency =
                _bandFrequencies[_centerBandIndex];

            _candidateCenterBandIndex = -1;
            _candidateTime = 0;
        }
    }

    private int FindBestCenterCandidate()
    {
        int bestIndex = 0;
        float bestScore = float.MinValue;

        for (int i = 0; i < _analysisValues.Length; i++)
        {
            float score = GetCenterScore(i);

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private float GetCenterScore(int index)
    {
        float peak = Math.Max(0f, _peakAverage[index]);
        float activity = Math.Max(0f, _activityAverage[index]);

        return peak * 0.70f + activity * 0.30f;
    }

    private void BuildMirroredSpectrum()
    {
        int halfCount = (_bandCount + 1) / 2;

        if (_centerBandIndex < 0)
        {
            Array.Clear(_output, 0, _output.Length);
            return;
        }

        float centerHz =
            _bandFrequencies[_centerBandIndex];

        float minHz =
            Math.Max(1f, _config.MinHz);

        float maxHz =
            _config.MaxHz > 0
                ? Math.Min(
                    _config.MaxHz,
                    _sampleRate * 0.5f)
                : _sampleRate * 0.5f;

        double lowerDistance =
            Math.Log(centerHz / minHz, 2.0);

        double upperDistance =
            Math.Log(maxHz / centerHz, 2.0);

        double maximumDistance =
            Math.Max(
                Math.Max(0.001, lowerDistance),
                Math.Max(0.001, upperDistance));

        double spreadPower =
            Math.Max(0.05, _config.EdgeSpreadPower);

        for (int radial = 0; radial < halfCount; radial++)
        {
            if (radial == 0)
            {
                _radialValues[radial] =
                    GetCenterValue();

                continue;
            }

            double t =
                halfCount <= 1
                    ? 1.0
                    : (double)radial / (halfCount - 1);
            
            double distanceOctaves =
                 Math.Pow(t, spreadPower) * maximumDistance;

            _radialValues[radial] =
                SampleAtRadialDistance(
                    centerHz,
                    distanceOctaves,
                    minHz,
                    maxHz);
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

            float value =
                _radialValues[radialIndex];

            _output[i] =
                ApplyDisplayTransform(value);
        }
    }

    private float GetCenterValue()
    {
        float value =
            Math.Max(
                0f,
                _analysisValues[_centerBandIndex]);

        return value;
    }

    private float SampleAtRadialDistance(
        float centerHz,
        double distanceOctaves,
        float minHz,
        float maxHz)
    {
        double lowerHz =
            centerHz *
            Math.Pow(2.0, -distanceOctaves);

        double upperHz =
            centerHz *
            Math.Pow(2.0, distanceOctaves);

        bool hasLower =
            lowerHz >= minHz;

        bool hasUpper =
            upperHz <= maxHz;

        float lowerValue = 0f;
        float upperValue = 0f;

        if (hasLower)
        {
            lowerValue =
                SampleFrequency(
                    (float)lowerHz);
        }

        if (hasUpper)
        {
            upperValue =
                SampleFrequency(
                    (float)upperHz);
        }

        /*
         * Both sides of the frequency spectrum contribute to the
         * same radial point.
         *
         * This is what makes the resulting graph genuinely mirrored.
         */
        if (hasLower && hasUpper)
        {
            return CombineMirroredValues(
                lowerValue,
                upperValue);
        }

        if (hasLower)
            return lowerValue;

        if (hasUpper)
            return upperValue;

        /*
         * Once one physical side has run out of spectrum,
         * continue using the available side.
         */
        if (lowerHz < minHz && upperHz <= maxHz)
            return upperValue;

        if (upperHz > maxHz && lowerHz >= minHz)
            return lowerValue;

        return 0f;
    }

    private float CombineMirroredValues(
        float lower,
        float upper)
    {
        switch (_config.Aggregation)
        {
            case CenteredAggregation.Max:
                return Math.Max(lower, upper);

            case CenteredAggregation.Rms:
                return (float)Math.Sqrt(
                    (lower * lower +
                     upper * upper) * 0.5);

            default:
                return (lower + upper) * 0.5f;
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
    // Display processing
    // --------------------------------------------------------------------

    private float ApplyDisplayTransform(float value)
    {
        value = Math.Max(0f, value);

        /*
         * The input magnitude is assumed to already be normalized
         * by the FFT stage in the same way as the original pipeline.
         *
         * Convert to dB.
         */
        const float epsilon = 1e-12f;

        float db =
            20f *
            (float)Math.Log10(
                Math.Max(epsilon, value));

        float floor =
            _config.NoiseFloorDb;

        float ceiling =
            _config.CeilingDb;

        if (ceiling <= floor)
            ceiling = floor + 1f;

        float normalized =
            (db - floor) /
            (ceiling - floor);

        normalized =
            Clamp01(normalized);

        // Soft noise gate.
        float gate =
            Clamp01(_config.NoiseGateNorm);

        if (gate > 0f)
        {
            if (normalized <= gate)
            {
                normalized = 0f;
            }
            else
            {
                normalized =
                    (normalized - gate) /
                    Math.Max(
                        1e-6f,
                        1f - gate);
            }
        }

        // Gamma / contrast.
        float gamma =
            Math.Max(0.01f, _config.Gamma);

        normalized =
            (float)Math.Pow(
                normalized,
                gamma);

        return Clamp01(normalized);
    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------

    private static float TimeAlpha(
        double dt,
        double timeConstant)
    {
        if (dt <= 0)
            return 0f;

        if (timeConstant <= 0)
            return 1f;

        return (float)(
            1.0 -
            Math.Exp(-dt / timeConstant));
    }

    private double GetDeltaTime()
    {
        long now =
            System.Diagnostics.Stopwatch.GetTimestamp();

        if (_lastTimestamp == 0)
        {
            _lastTimestamp = now;
            return 1.0 / 60.0;
        }

        long elapsed =
            now - _lastTimestamp;

        _lastTimestamp = now;

        double dt =
            elapsed /
            (double)System.Diagnostics.Stopwatch.Frequency;

        // Protect the temporal filters against stalls.
        return Math.Min(0.25, Math.Max(0.0001, dt));
    }

    private static float GeometricMean(
        float a,
        float b)
    {
        a = Math.Max(0.0001f, a);
        b = Math.Max(0.0001f, b);

        return (float)Math.Sqrt(a * b);
    }

    private static float FrequencyDistanceOctaves(
        float a,
        float b)
    {
        if (a <= 0 || b <= 0)
            return float.MaxValue;

        return Math.Abs(
            (float)Math.Log(
                a / b,
                2.0));
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