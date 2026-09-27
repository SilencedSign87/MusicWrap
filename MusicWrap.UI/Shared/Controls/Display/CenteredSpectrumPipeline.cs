namespace MusicWrap.UI.Controls;

public sealed class CenteredSpectrumPipelineConfig
{
    //  FFT / frequency range

    public int FftSize { get; set; } = 16384;
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Total number of bars in the final output (must be even, split L/R).
    /// </summary>
    public int BarCount { get; set; } = 80;

    /// <summary>
    /// Lower bound of the analyzed spectrum.
    /// </summary>
    public float MinHz { get; set; } = 20f;

    /// <summary>
    /// Upper bound of the analyzed spectrum (the outer edges).
    /// </summary>
    public float MaxHz { get; set; } = 20000f;

    /// <summary>
    /// Transition frequency: everything from MinHz to TransitionHz is collapsed
    /// into a single bass value per channel. Above TransitionHz, spectrum expands normally.
    /// </summary>
    public float TransitionHz { get; set; } = 200f;

    /// <summary>
    /// How many bars the collapsed bass region "bleeds" into (affects neighbors).
    /// </summary>
    public int BleedingWidth { get; set; } = 5;

    /// <summary>
    /// Shape of the bleed falloff: 1 = Gaussian, >1 = exponential fall (sharp peak), 
    /// <1 = square/flat peak. Applied inversely (higher = faster falloff from center).
    /// </summary>
    public float BleedingGamma { get; set; } = 1.5f;

    // Dynamic range (dB)
    public float NoiseFloorDb { get; set; } = -80f;
    public float CeilingDb { get; set; } = 0f;

    // Noise gate
    //  0..1 (0 = off) 
    public float NoiseGateNorm { get; set; } = 0.05f;

    // Smoothing
    // EMA (0 = ignore new , 1 = no smoothing)
    public float SmoothingAlpha { get; set; } = 0.9f;
    // dead zone 0..1 (0 = off)
    public float ChangeThreshold { get; set; } = 0.0f;

    // Per-zone boosts (applied after normalization, before collapse/bleed)
    public float BassBoost { get; set; } = 1.2f;
    public float MidBoost { get; set; } = 1.0f;
    public float TrebleBoost { get; set; } = 1.2f;

    /// <summary>
    /// How the whole central band (MinHz to CenterHz) is merged into the
    /// single center value of the graph. Only affects the center point.
    /// </summary>
    public CenteredAggregation Aggregation { get; set; } = CenteredAggregation.PowerMean;

    public float AggregationPower { get; set; } = 6f;
}


public enum CenteredAggregation
{
    Max,
    Average,
    Rms,
    PowerMean
}

public sealed class CenteredSpectrumPipeline
{
    private readonly CenteredSpectrumPipelineConfig _config;
    private readonly SpectrumPipeline _leftPipeline;
    private readonly SpectrumPipeline _rightPipeline;
    private SpectrumPipelineConfig _childConfig;

    private int _sampleRate;
    private int _fftSize;
    private int _outputBandCount; // Must be even

    // Each side gets half the bars
    private int _halfBandCount => _outputBandCount / 2;

    // How many bands each child pipeline needs to produce
    private int _childBandCount;
    private float[] _boosts = [];

    private float[] _leftProcessed = [];
    private float[] _rightProcessed = [];
    private float[] _output = [];

    private float _effMinHz;
    private float _effMaxHz;
    private float _effTransitionHz;
    private int _transitionIndex;


    public CenteredSpectrumPipeline(CenteredSpectrumPipelineConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _sampleRate = Math.Max(1, config.SampleRate);
        _fftSize = Math.Max(2, config.FftSize);
        _outputBandCount = Math.Max(2, config.BarCount);

        // ensure even
        if ((_outputBandCount & 1) != 0)
            _outputBandCount++;

        _childConfig = new SpectrumPipelineConfig
        {
            SampleRate = _sampleRate,
            binCount = _fftSize,
            MinEqHz = Math.Max(1f, _config.MinHz),
            MaxEqHz = Math.Min(_config.MaxHz, _sampleRate * 0.5f),
            NoiseFloorDb = _config.NoiseFloorDb,
            CeilingDb = _config.CeilingDb,
            NoiseGateNorm = _config.NoiseGateNorm,
            SmoothingAlpha = _config.SmoothingAlpha,
            ChangeThreshold = _config.ChangeThreshold,
            HighShelfGain = 0f, // We'll handle boosts manually
            HighShelfCurve = 1f,
            ContrastGamma = 1f,
            EqGamma = 1f,
            GammaDelta = 0f,
            GammaFloor = 1f
        };

        _leftPipeline = new SpectrumPipeline(_childConfig);
        _rightPipeline = new SpectrumPipeline(_childConfig);

        RebuildLayout();
        RebuildBuffers();
        _leftPipeline.SetBandCount(_childBandCount);
        _rightPipeline.SetBandCount(_childBandCount);
    }

    public int BandCount => _outputBandCount;

    public void SetBandCount(int bandCount)
    {
        _outputBandCount = Math.Max(2, bandCount);
        if ((_outputBandCount & 1) != 0)
            _outputBandCount++;

        RebuildLayout();
        RebuildBuffers();
        _leftPipeline.SetBandCount(_childBandCount);
        _rightPipeline.SetBandCount(_childBandCount);
    }

    public void OnConfigurationChanged(int sampleRate, int fftSize)
    {
        _sampleRate = Math.Max(1, sampleRate);
        _fftSize = Math.Max(2, fftSize);

        _config.SampleRate = _sampleRate;
        _config.FftSize = _fftSize;

        _childConfig.SampleRate = _sampleRate;
        _childConfig.binCount = _fftSize;

        _leftPipeline.OnConfigurationChanged(_sampleRate, _fftSize);
        _rightPipeline.OnConfigurationChanged(_sampleRate, _fftSize);

        RebuildLayout();
        RebuildBuffers();

        _leftPipeline.SetBandCount(_childBandCount);
        _rightPipeline.SetBandCount(_childBandCount);
    }

    public float[] Process(float[] interleavedMagnitudes)
    {
        if (interleavedMagnitudes == null || interleavedMagnitudes.Length == 0)
            return _output;

        if (_leftProcessed.Length == 0)
            RebuildBuffers();

        _leftProcessed = _leftPipeline.ProcessChannel(interleavedMagnitudes, 0);
        _rightProcessed = _rightPipeline.ProcessChannel(interleavedMagnitudes, 1);

        // Apply per-zone boosts to child pipeline outputs
        ApplyZoneBoosts(_leftProcessed);
        ApplyZoneBoosts(_rightProcessed);

        // Collapse low range (MinHz to TransitionHz) and bleed
        CollapseAndBleed(_leftProcessed);
        CollapseAndBleed(_rightProcessed);

        // Mirror: invert left and append right
        BuildCenteredOutput();


        return _output;
    }

    // --------------------------------------------------------------------
    // Configuration / buffers
    // --------------------------------------------------------------------

    private void RebuildLayout()
    {
        _effMinHz = Math.Max(1f, _config.MinHz);
        _effMaxHz = Math.Min(_config.MaxHz, _sampleRate * 0.5f);
        _effTransitionHz = Math.Clamp(_config.TransitionHz, _effMinHz, _effMaxHz);

        if (_effMaxHz <= _effMinHz)
        {
            _childBandCount = _halfBandCount;
            _transitionIndex = 0;
            return;
        }

        double f = Math.Log(_effTransitionHz / _effMinHz) / Math.Log(_effMaxHz / _effMinHz);

        if (f >= 1.0)
            f = 0.999;

        int needed = _halfBandCount - 1;
        int n = Math.Max(1, (int)Math.Ceiling(needed / (1.0 - f)));

        for (int guard = 0; guard < 64 && n > 1; guard++)
        {
            if (n - 1 - CollapsedIndex(n, f) == needed)
                break;

            n--;
        }

        _childBandCount = n;
        _transitionIndex = n > 2 ? Math.Clamp(CollapsedIndex(n, f), 0, n - 2) : 0;
        RebuildBoostTable();
    }
    private static int CollapsedIndex(int bandCount, double collapsedFraction)
        => Math.Max(0, (int)Math.Ceiling(bandCount * collapsedFraction) - 1);

    private void RebuildBuffers()
    {
        if (_leftProcessed.Length != _childBandCount)
            _leftProcessed = new float[_childBandCount];
        if (_rightProcessed.Length != _childBandCount)
            _rightProcessed = new float[_childBandCount];
        if (_output.Length != _outputBandCount)
            _output = new float[_outputBandCount];
    }
    // --------------------------------------------------------------------
    // Processing stages
    // --------------------------------------------------------------------
    private void RebuildBoostTable()
    {
        _boosts = new float[_childBandCount];
        if (_childBandCount <= 1 || _effMaxHz <= _effMinHz)
            return;

        double logRange = Math.Log(_effMaxHz) - Math.Log(_effMinHz);
        float trebleStart = Math.Max(_config.TransitionHz, 8000f);

        for (int i = 0; i < _childBandCount; i++)
        {
            double t = (double)i / (_childBandCount - 1);
            float freq = (float)Math.Exp(Math.Log(_effMinHz) + logRange * t);

            _boosts[i] = freq < _config.TransitionHz ? _config.BassBoost
                   : freq < trebleStart ? _config.MidBoost
                   : _config.TrebleBoost;
        }
    }

    private void ApplyZoneBoosts(float[] bands)
    {
        //if (bands.Length == 0) return;

        //float minHz = Math.Max(1f, _config.MinHz);
        //float maxHz = Math.Min(_config.MaxHz, _sampleRate * 0.5f);

        //if (maxHz <= minHz) return;

        //double minLog = Math.Log(minHz);
        //double maxLog = Math.Log(maxHz);
        //double logRange = maxLog - minLog;

        //for (int i = 0; i < bands.Length; i++)
        //{
        //    double t = bands.Length <= 1 ? 0 : (double)i / (bands.Length - 1);
        //    double logFreq = minLog + logRange * t;
        //    float freq = (float)Math.Exp(logFreq);

        //    float boost = 1.0f;
        //    float transitionHz = _config.TransitionHz;
        //    float trebleStart = Math.Max(transitionHz, 8000f);

        //    if (freq < transitionHz)
        //        boost *= _config.BassBoost;
        //    else if (freq < trebleStart)
        //        boost *= _config.MidBoost;
        //    else
        //        boost *= _config.TrebleBoost;

        //    bands[i] *= boost;
        //}
        int n = Math.Min(bands.Length, _boosts.Length);
        for (int i = 0; i < n; i++)
        {
            bands[i] *= _boosts[i];
        }
    }

    private void CollapseAndBleed(float[] bands)
    {
        if (bands.Length == 0) return;

        int transitionIndex = _transitionIndex;

        int count = 0;

        for (int i = 0; i <= transitionIndex; i++)
            count++;

        float collapsedValue = Aggregate(bands, count, _config.Aggregation, _config.AggregationPower);


        // Store collapsed value at transitionIndex
        bands[transitionIndex] = Math.Max(bands[transitionIndex], collapsedValue);

        // Bleed to upper neighbors (transitionIndex + 1, +2, ... up to BleedingWidth)
        int bleedWidth = Math.Min(_config.BleedingWidth, bands.Length - transitionIndex - 1);
        float gamma = Math.Max(0.1f, _config.BleedingGamma);

        for (int b = 1; b <= bleedWidth; b++)
        {
            int idx = transitionIndex + b;
            if (idx >= bands.Length) break;

            // Distance from collapse point (normalized 0..1)
            float dist = (float)b / (bleedWidth + 1);
            // Inverse exponential: higher gamma = faster falloff
            float bleedFactor = (float)Math.Pow(1.0 - dist, gamma);

            // Add bleed contribution (additive, clamped)
            bands[idx] = Math.Min(1f, bands[idx] + collapsedValue * bleedFactor * 0.5f);
        }

        // Zero out the collapsed bands below transitionIndex (they're now represented at transitionIndex)
        for (int i = 0; i < transitionIndex; i++)
        {
            bands[i] = 0f;
        }
    }

    private void BuildCenteredOutput()
    {

        if (_childBandCount == 0 || _outputBandCount < 2)
        {
            Array.Fill(_output, 0f);
            return;
        }

        int t = _transitionIndex;
        int neededUpper = _halfBandCount - 1;

        // Center pair: the collapsed bass of each channel.
        _output[_halfBandCount - 1] = _leftProcessed[t];
        _output[_halfBandCount] = _rightProcessed[t];


        for (int side = 0; side < neededUpper; side++)
        {
            int srcIdx = t + 1 + side;

            _output[_halfBandCount + 1 + side] = _rightProcessed[srcIdx];
            _output[_halfBandCount - 2 - side] = _leftProcessed[srcIdx];
        }

    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------

    private float Aggregate(float[] bands, int count, CenteredAggregation mode, float power)
    {
        if (count <= 0)
            return 0f;

        switch (mode)
        {
            case CenteredAggregation.Max:
                {
                    float max = 0f;
                    for (int i = 0; i < count; i++)
                        max = MathF.Max(max, Math.Max(0f, bands[i]));
                    return max;
                }

            case CenteredAggregation.Average:
                {
                    float sum = 0f;
                    for (int i = 0; i < count; i++)
                        sum += Math.Max(0f, bands[i]);
                    return sum / count;
                }

            case CenteredAggregation.PowerMean:
                {
                    if (power < 1f) power = 1f;
                    double sum = 0.0;
                    for (int i = 0; i < count; i++)
                    {
                        double v = Math.Max(0f, bands[i]);
                        sum += Math.Pow(v, power);
                    }
                    return (float)Math.Pow(sum / count, 1.0 / power);
                }

            case CenteredAggregation.Rms:
            default:
                {
                    double sumSq = 0.0;
                    for (int i = 0; i < count; i++)
                    {
                        double v = Math.Max(0f, bands[i]);
                        sumSq += v * v;
                    }
                    return (float)Math.Sqrt(sumSq / count);
                }
        }
    }
}
