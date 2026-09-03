namespace MusicWrap.UI.Controls;

public sealed class CenteredSpectrumPipelineConfig
{
    //  FFT / frequency range 

    public int FftSize { get; set; } = 16384;
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Number of output points/bands.
    ///
    /// The returned array is ordered from low frequency
    /// at the center toward high frequencies at the edges.
    ///
    /// The renderer decides how these points are drawn.
    /// </summary>
    public int BarCount { get; set; } = 80;

    public float MinHz { get; set; } = 20f;
    public float MaxHz { get; set; } = 20000f;

    public float NyquistBias { get; set; } = 0.98f;


    //  Dynamic range (dB) 

    public float NoiseFloorDb { get; set; } = -70f;
    public float CeilingDb { get; set; } = -10f;


    //  Noise gate 

    public float NoiseGateNorm { get; set; } = 0.08f;


    //  Spectral aggregation 

    /// <summary>
    /// How FFT bins inside each visual frequency range
    /// are combined.
    /// </summary>
    public CenteredAggregation Aggregation { get; set; }
        = CenteredAggregation.Rms;


    //  Temporal smoothing 

    /// <summary>
    /// Smoothing applied to rising values.
    ///
    /// 0 = completely frozen.
    /// 1 = immediate response.
    /// </summary>
    public float Attack { get; set; } = 0.4f;

    /// <summary>
    /// Smoothing applied to falling values.
    ///
    /// Lower values produce longer decay.
    /// </summary>
    public float Release { get; set; } = 0.8f;


    //  Contrast 

    public float Gamma { get; set; } = 1.15f;


    //  Frequency distribution 

    /// <summary>
    /// Frequency around which the low-frequency region
    /// transitions into the more expanded high-frequency
    /// region.
    ///
    /// Around 400 Hz gives a compact center similar to
    /// the MusicBee centered spectrum.
    /// </summary>
    public float LowFrequencyCompressionHz { get; set; } = 400f;

    /// <summary>
    /// Controls the amount of additional compression
    /// toward the center.
    ///
    /// 1.0 = standard compression curve.
    /// >1.0 = stronger concentration toward the center.
    /// <1.0 = wider low-frequency region.
    /// </summary>
    public float LowFrequencyCompressionPower { get; set; } = 1.0f;

    /// <summary>
    /// Additional merge strength toward the center
    /// (low frequencies).
    ///
    /// 0 = no extra merge (uniform smoothing).
    /// 1 = center bands fully merged into one curve.
    /// </summary>
    public float LowFrequencyMerge { get; set; } = 0.8f;


    // frequency weighting

    /// <summary>
    /// Optional additional boost for low frequencies.
    ///
    /// 0 = disabled.
    /// </summary>
    public float LowFrequencyBoost { get; set; } = 0.15f;
}


public enum CenteredAggregation
{
    Max,
    Average,
    Rms
}


public sealed class CenteredSpectrumPipeline
{
    private readonly CenteredSpectrumPipelineConfig _cfg;

    private FrequencyBand[] _bandMap = [];

    private float[] _smoothed = [];


    public CenteredSpectrumPipeline(
        CenteredSpectrumPipelineConfig config)
    {
        _cfg = config;

        _smoothed =
            new float[Math.Max(1, config.BarCount)];

        RebuildBandMapping();
    }


    public int BarCount =>
        _cfg.BarCount;


    public void OnConfigurationChanged(
        int sampleRate,
        int fftSize)
    {
        _cfg.SampleRate = sampleRate;
        _cfg.FftSize = fftSize;

        RebuildBandMapping();
        ResetSmoothing();
    }


    public void ResetSmoothing()
    {
        Array.Clear(_smoothed);
    }


    public void SetBarCount(int barCount)
    {
        _cfg.BarCount =
            Math.Max(1, barCount);

        _smoothed =
            new float[_cfg.BarCount];

        RebuildBandMapping();
    }

    public float[] Process(float[] magnitudes)
    {
        if (magnitudes == null ||
            magnitudes.Length == 0)
        {
            return _smoothed;
        }

        int count =
            Math.Min(
                _cfg.BarCount,
                _bandMap.Length);

        if (_smoothed.Length != _cfg.BarCount)
        {
            _smoothed =
                new float[_cfg.BarCount];
        }


        for (int i = 0; i < count; i++)
        {
            FrequencyBand band =
                _bandMap[i];


            // -----------------------------------------
            // 1. Aggregate FFT bins belonging to this band
            // -------------------------------------------------

            float magnitude =
                Aggregate(
                    magnitudes,
                    band.StartBin,
                    band.EndBin);


            // -------------------------------------------------
            // 2. Convert magnitude to dB
            // -------------------------------------------------

            float db =
                20f *
                MathF.Log10(
                    magnitude + 1e-8f);


            // -------------------------------------------------
            // 3. Normalize dynamic range
            // -------------------------------------------------

            float value =
                (db - _cfg.NoiseFloorDb) /
                (_cfg.CeilingDb -
                 _cfg.NoiseFloorDb);

            value =
                Math.Clamp(
                    value,
                    0f,
                    1f);


            // -------------------------------------------------
            // 4. Noise gate
            // -------------------------------------------------

            value =
                ApplyGate(value);


            // -------------------------------------------------
            // 5. Optional frequency weighting
            // -------------------------------------------------

            value =
                ApplyFrequencyWeight(
                    value,
                    band.Position);


            // -------------------------------------------------
            // 6. Contrast
            // -------------------------------------------------

            value =
                MathF.Pow(
                    Math.Clamp(value, 0f, 1f),
                    _cfg.Gamma);


            // -------------------------------------------------
            // 7. Temporal smoothing
            // -------------------------------------------------

            SmoothValue(
                i,
                value);
        }


        return _smoothed;
    }


    #region Processing stages


    private float ApplyGate(float value)
    {
        float gate =
            _cfg.NoiseGateNorm;

        if (gate <= 0f)
            return value;

        if (value >= gate)
            return value;

        float ratio =
            value / gate;

        /*
         * Soft gate.
         *
         * At value == gate:
         *
         *     result == gate
         *
         * At value == 0:
         *
         *     result == 0
         */

        return value * ratio;
    }


    private float ApplyFrequencyWeight(
        float value,
        float position)
    {
        float boost =
            _cfg.LowFrequencyBoost;

        if (boost <= 0f)
            return value;


        /*
         * position:
         *
         * 0 = lowest frequency
         * 1 = highest frequency
         */

        float factor =
            1f +
            boost *
            (1f - position);

        return value * factor;
    }


    private void SmoothValue(
        int index,
        float target)
    {
        float current =
            _smoothed[index];


        float alpha =
            target > current
                ? _cfg.Attack
                : _cfg.Release;


        alpha =
            Math.Clamp(
                alpha,
                0f,
                1f);


        _smoothed[index] =
            current +
            (target - current) *
            alpha;
    }


    #endregion


    #region FFT aggregation


    private float Aggregate(
        float[] spectrum,
        int startBin,
        int endBin)
    {
        if (spectrum.Length == 0)
            return 0f;


        startBin =
            Math.Clamp(
                startBin,
                0,
                spectrum.Length - 1);


        endBin =
            Math.Clamp(
                endBin,
                startBin,
                spectrum.Length - 1);


        switch (_cfg.Aggregation)
        {
            case CenteredAggregation.Max:
                {
                    float max = 0f;

                    for (int i = startBin;
                         i <= endBin;
                         i++)
                    {
                        if (spectrum[i] > max)
                            max = spectrum[i];
                    }

                    return max;
                }


            case CenteredAggregation.Average:
                {
                    float sum = 0f;

                    int count =
                        endBin -
                        startBin +
                        1;

                    for (int i = startBin;
                         i <= endBin;
                         i++)
                    {
                        sum += spectrum[i];
                    }

                    return sum / count;
                }


            case CenteredAggregation.Rms:
                {
                    double sum = 0.0;

                    int count =
                        endBin -
                        startBin +
                        1;

                    for (int i = startBin;
                         i <= endBin;
                         i++)
                    {
                        double value =
                            spectrum[i];

                        sum +=
                            value * value;
                    }

                    return (float)Math.Sqrt(
                        sum / count);
                }


            default:
                return 0f;
        }
    }


    #endregion


    #region Frequency mapping


    private void RebuildBandMapping()
    {
        int count =
            Math.Max(
                1,
                _cfg.BarCount);


        _bandMap =
            new FrequencyBand[count];


        int usableBins =
            _cfg.FftSize / 2;


        if (usableBins <= 0)
            return;


        float nyquist =
            _cfg.SampleRate * 0.5f;


        float binHz =
            nyquist / usableBins;


        if (binHz <= 0f)
            return;


        float minHz =
            Math.Max(
                _cfg.MinHz,
                binHz);


        float maxHz =
            Math.Min(
                _cfg.MaxHz,
                nyquist *
                _cfg.NyquistBias);


        if (maxHz <= minHz)
        {
            maxHz =
                nyquist *
                _cfg.NyquistBias;
        }

        // Centered visual frequency mapping

        double compressionHz =
            Math.Max(
                1.0,
                _cfg.LowFrequencyCompressionHz);


        double compressionPower =
            Math.Max(
                0.01,
                _cfg.LowFrequencyCompressionPower);


        double denominator =
            Math.Log(
                1.0 +
                maxHz /
                compressionHz);


        for (int i = 0;
             i < count;
             i++)
        {
            /*
             * Uniform visual position.
             *
             * Each output point represents an equal amount
             * of screen space.
             */

            double p0 =
                (double)i / count;

            double p1 =
                (double)(i + 1) / count;


            /*
             * Additional control over how quickly the
             * low-frequency region expands.
             */

            p0 =
                Math.Pow(
                    p0,
                    compressionPower);

            p1 =
                Math.Pow(
                    p1,
                    compressionPower);


            /*
             * Inverse mapping.
             *
             * Math.Expm1(x) would be ideal numerically for
             * very small x, but System.Math does not expose it,
             * so use:
             *
             *     Math.Exp(x) - 1
             */

            double lowHz =
                compressionHz *
                (Math.Exp(
                    p0 * denominator) - 1.0);


            double highHz =
                compressionHz *
                (Math.Exp(
                    p1 * denominator) - 1.0);


            lowHz =
                Math.Clamp(
                    lowHz,
                    minHz,
                    maxHz);


            highHz =
                Math.Clamp(
                    highHz,
                    minHz,
                    maxHz);


            if (highHz < lowHz)
                highHz = lowHz;


            int startBin =
                FrequencyToBin(
                    lowHz,
                    binHz,
                    usableBins);


            int endBin =
                FrequencyToBin(
                    highHz,
                    binHz,
                    usableBins);


            if (endBin < startBin)
                endBin = startBin;


            float position =
                count > 1
                    ? (float)i /
                      (count - 1)
                    : 0f;


            _bandMap[i] =
                new FrequencyBand(
                    startBin,
                    endBin,
                    position);
        }
    }


    private static int FrequencyToBin(
        double frequency,
        float binHz,
        int maxBins)
    {
        int bin =
            (int)Math.Floor(
                frequency / binHz);


        return Math.Clamp(
            bin,
            0,
            maxBins - 1);
    }


    #endregion


    private readonly struct FrequencyBand
    {
        public readonly int StartBin;
        public readonly int EndBin;

        /// <summary>
        /// 0 = lowest frequency / center.
        /// 1 = highest frequency / edge.
        /// </summary>
        public readonly float Position;


        public FrequencyBand(
            int startBin,
            int endBin,
            float position)
        {
            StartBin = startBin;
            EndBin = endBin;
            Position = position;
        }
    }
}