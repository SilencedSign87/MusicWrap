using MusicWrap.Data.User.Models;
using System.Diagnostics;
using ManagedBass;
using ManagedBass.Mix;
using System.Runtime.InteropServices;

#if WINDOWS
using ManagedBass.Wasapi;
#endif

namespace MusicWrap.Core
{
    public class AudioEngine : IDisposable
    {
        private const int PreferredSrcQuality = 6;

        private bool _isInitialized;

#if WINDOWS
        private bool _isWasapiInitialized;
        private WasapiProcedure? _wasapiProc;
#endif

        private int _flacPluginHandle;
        private int _opusPluginHandle;

        private OutputMode _currentOutputMode = OutputMode.WasapiShared;

        private int _mixerStream;
        private int _mixerSampleRate;
        private int _mixerChannels;

        private int _lastDeviceIndex = -1;
        private int _lastSampleRate = 44100;
        private int _lastRequestedSampleRate = 44100;
        private int _lastRequestedChannels = 2;

        private int _currentOutputSampleRate = 44100;
        private int _currentOutputChannels = 2;

        public OutputMode CurrentOutputMode => _currentOutputMode;
        public int CurrentOutputSampleRate => _currentOutputSampleRate;
        public int CurrentOutputChannels => _currentOutputChannels;
        public int CurrentMixerSampleRate => _mixerSampleRate;
        public bool IsMixerActive => _mixerStream != 0;

        private int _writePos;
        private const int CaptureSamples = 262144 * 2; // 320khz at stereo
        private DSPProcedure? _fftDspCallback;
        private float[] _fftCaptureBuffer = Array.Empty<float>();
        private float[] _dspTempBuffer = Array.Empty<float>();
        private int _fftCaptureFloats;
        private readonly object _fftLock = new();
        private bool _fftCaptureActive;
        private int _fftDspHandle;

        // FFT buffers for spectrum analysis
        private const int _spectrumFftSize = 32768;
        private float[] _spectrumWorkBuffer = Array.Empty<float>();
        private float[] _spectrumWindowBuffer = Array.Empty<float>();
        private System.Numerics.Complex[] _spectrumComplexBuffer = Array.Empty<System.Numerics.Complex>();
        private float[] _spectrumMagnitudes = Array.Empty<float>();

        private static string GetNativeLibExtension()
        {
#if WINDOWS
            return ".dll";
#elif ANDROID
            return ".so";
#else
            return ".dll";
#endif
        }

        public bool Initialize(int deviceIndex = -1, int sampleRate = 44100, OutputMode outputmode = OutputMode.WasapiShared)
        {
            if (_isInitialized)
                return true;

            _lastDeviceIndex = deviceIndex;
            _lastSampleRate = sampleRate > 0 ? sampleRate : 44100;
            _lastRequestedSampleRate = _lastSampleRate;
            _lastRequestedChannels = 2;
            _currentOutputMode = outputmode;

            Bass.PlaybackBufferLength = 90;
            Bass.UpdatePeriod = 5;
            Bass.NetTimeOut = 7000;
            Bass.NetReadTimeOut = 7000;
            Bass.NetPreBuffer = 0;

            _isInitialized = Bass.Init(deviceIndex, sampleRate, DeviceInitFlags.Default);
            if (!_isInitialized) return false;

            _flacPluginHandle = Bass.PluginLoad("bassflac" + GetNativeLibExtension());
            _opusPluginHandle = Bass.PluginLoad("bassopus" + GetNativeLibExtension());

            if (_flacPluginHandle == 0)
                Debug.WriteLine($"Failed to load FLAC plugin: {Bass.LastError}");
            if (_opusPluginHandle == 0)
                Debug.WriteLine($"Failed to load Opus plugin: {Bass.LastError}");

#if WINDOWS
            if (!InitializeWasapiForCurrentMode(_lastSampleRate))
            {
                Teardown();
                return false;
            }
#endif

            return _isInitialized;
        }

        public bool Reinitialize(int deviceIndex = -1, int sampleRate = 44100, OutputMode outputmode = OutputMode.WasapiShared)
        {
            Teardown();
            _lastDeviceIndex = deviceIndex;
            _lastSampleRate = sampleRate > 0 ? sampleRate : 44100;
            _lastRequestedSampleRate = _lastSampleRate;
            _lastRequestedChannels = 2;
            return Initialize(deviceIndex, sampleRate, outputmode);
        }

        #region STREAM FORMAT

        public (int SampleRate, int Channels) GetStreamFormat(int stream)
        {
            Bass.ChannelGetInfo(stream, out var info);
            return ((int)info.Frequency, info.Channels);
        }

        #endregion

        #region MIXER

        public bool EnsureMixer(int sampleRate, int channels = 2)
        {
            int requestedRate = sampleRate > 0 ? sampleRate : 44100;
            int requestedChannels = channels > 0 ? channels : 2;

            int effectiveRate = PrepareOutputForTrack(requestedRate, requestedChannels);
            int mixRate = effectiveRate > 0 ? effectiveRate : requestedRate;
            const int mixChannels = 2;

            if (_mixerStream != 0 && _mixerSampleRate == mixRate && _mixerChannels == mixChannels)
                return true;

            DestroyMixer();

            var flags = BassFlags.Float | BassFlags.MixerNonStop;

            flags |= (BassFlags)0x100000; // BASS_MIXER_QUEUE

            if (IsWasapiMode())
                flags |= BassFlags.Decode;

            _mixerStream = BassMix.CreateMixerStream(mixRate, 2, flags);
            if (_mixerStream == 0)
            {
                Debug.WriteLine($"[AudioEngine] Failed to create mixer: {Bass.LastError}");
                _mixerSampleRate = 0;
                _mixerChannels = 0;
                return false;
            }

            _mixerSampleRate = mixRate;
            _mixerChannels = mixChannels;

            RebuildSpectrumBuffers();
            StartFFTCapture();

            return true;
        }
        public bool IsInitialized => _isInitialized;
        public void EnsureMixerStarted()
        {
            if (_mixerStream != 0 && Bass.ChannelIsActive(_mixerStream) != PlaybackState.Playing)
                StartMixer();
        }

        public bool PrepareTrack(int trackStream, int preferredSampleRate = 0)
        {
            var (actualRate, actualChannels) = GetStreamFormat(trackStream);
            int targetRate = preferredSampleRate > 0 ? preferredSampleRate : actualRate;

            if (!EnsureMixer(targetRate, actualChannels))
                return false;

            Bass.ChannelSetAttribute(trackStream, ChannelAttribute.SampleRateConversion, PreferredSrcQuality);
            return BassMix.MixerAddChannel(_mixerStream, trackStream, BassFlags.MixerChanNoRampin);
        }

        public bool AttachTrackToMixer(int trackStream)
        {
            if (_mixerStream == 0) return false;
            Bass.ChannelSetAttribute(trackStream, ChannelAttribute.SampleRateConversion, PreferredSrcQuality);
            return BassMix.MixerAddChannel(_mixerStream, trackStream, BassFlags.MixerChanNoRampin);
        }

        public bool DetachTrack(int trackStream)
        {
            return BassMix.MixerRemoveChannel(trackStream);
        }

        public bool StartMixer()
        {
            if (_mixerStream == 0) return false;
            RebuildSpectrumBuffers();
            StartFFTCapture();
#if WINDOWS
            if (IsWasapiMode())
                return _isWasapiInitialized && BassWasapi.Start();
#endif
            return Bass.ChannelPlay(_mixerStream, false);
        }

        public bool PauseMixer()
        {
#if WINDOWS
            if (IsWasapiMode())
                return _isWasapiInitialized && BassWasapi.Stop(false);
#endif
            return _mixerStream != 0 && Bass.ChannelPause(_mixerStream);
        }

        public bool StopMixer()
        {
#if WINDOWS
            if (IsWasapiMode())
                return _isWasapiInitialized && BassWasapi.Stop(true);
#endif
            return _mixerStream != 0 && Bass.ChannelStop(_mixerStream);
        }

        public int CreateMixer(int sampleRate = 44100)
        {
            var flags = BassFlags.Float | BassFlags.MixerNonStop;
            if (IsWasapiMode())
                flags |= BassFlags.Decode;

            int mixRate = sampleRate > 0 ? sampleRate : 44100;
            if (IsWasapiMode() && _currentOutputSampleRate > 0)
                mixRate = _currentOutputSampleRate;

            return BassMix.CreateMixerStream(mixRate, 2, flags);
        }

        public int CreateDecodeStream(string filePath)
        {
            return Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Float);
        }

        public int CreateDecodeStreamFromUrl(string url)
        {
            if (!_isInitialized) throw new InvalidOperationException("Audio engine not initialized.");
            if (string.IsNullOrWhiteSpace(url)) return 0;

            var flags = BassFlags.Decode | BassFlags.Float | BassFlags.Prescan | BassFlags.StreamDownloadBlocks;
            var task = Task.Run(() => Bass.CreateStream(url, 0, flags, null, IntPtr.Zero));
            if (!task.Wait(TimeSpan.FromSeconds(10)))
            {
                Debug.WriteLine($"[AudioEngine] Timeout creating stream from URL: {url}");
                return 0;
            }
            return task.Result;
        }

        public bool AddToMixer(int mixerStream, int stream, BassFlags flags = BassFlags.Default)
        {
            return BassMix.MixerAddChannel(mixerStream, stream, flags);
        }

        public bool RemoveFromMixer(int stream)
        {
            return BassMix.MixerRemoveChannel(stream);
        }

        public double GetMixerPosition(int stream)
        {
            long pos = BassMix.ChannelGetPosition(stream);
            if (pos < 0) return 0.0;
            return Bass.ChannelBytes2Seconds(stream, pos);
        }

        private void DestroyMixer()
        {
            StopFFTCapture();
            if (_mixerStream != 0)
            {
                Bass.StreamFree(_mixerStream);
                _mixerStream = 0;
            }
            _mixerSampleRate = 0;
            _mixerChannels = 0;
        }

        #endregion

        public int PrepareOutputForTrack(int sampleRate, int channels = 2)
        {
            int requestedRate = sampleRate > 0 ? sampleRate : 44100;
            int requestedChannels = channels > 0 ? channels : 2;

            if (!IsWasapiMode())
            {
                _currentOutputSampleRate = requestedRate;
                _currentOutputChannels = requestedChannels;
                return _currentOutputSampleRate;
            }

#if WINDOWS
            bool requiresReopen = !_isWasapiInitialized
                || _lastRequestedSampleRate != requestedRate
                || _lastRequestedChannels != requestedChannels;

            if (requiresReopen)
            {
                _lastSampleRate = requestedRate;
                _lastRequestedSampleRate = requestedRate;
                _lastRequestedChannels = requestedChannels;
                ReopenWasapi(requestedRate, requestedChannels);
            }
#endif
            return _currentOutputSampleRate > 0 ? _currentOutputSampleRate : requestedRate;
        }

        public int GetDeviceLatencyMs()
        {
#if WINDOWS
            if (IsWasapiMode() && _isWasapiInitialized)
            {
                var info = BassWasapi.Info;
                var bufferSeconds = Convert.ToDouble(info.BufferLength);
                if (bufferSeconds > 0)
                    return Math.Max(1, (int)Math.Round(bufferSeconds * 1000.0));
            }
#endif
            var bassInfo = Bass.Info;
            return Math.Max(0, bassInfo.Latency);
        }

        public int CreateStream(string filePath)
        {
            if (!_isInitialized) throw new InvalidOperationException("Audio engine not initialized.");
            return Bass.CreateStream(filePath, 0, 0, BassFlags.Default);
        }

        public static async Task<float[]> GetWaveFromDataAsync(string filePath, int dataPoints = 2000)
        {
            return await Task.Run(() =>
            {
                int stream = Bass.CreateStream(filePath, 0, 0,
                    BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
                if (stream == 0)
                    return Array.Empty<float>();

                try
                {
                    long lengthBytes = Bass.ChannelGetLength(stream);
                    if (lengthBytes <= 0) return Array.Empty<float>();

                    long bytesPerChunk = lengthBytes / dataPoints;
                    if (bytesPerChunk <= 0) bytesPerChunk = 4;

                    int floatsPerChunk = (int)(bytesPerChunk / 4);
                    float[] buffer = new float[floatsPerChunk];
                    float[] waveform = new float[dataPoints];

                    for (int i = 0; i < dataPoints; i++)
                    {
                        int bytesRead = Bass.ChannelGetData(stream, buffer, (int)bytesPerChunk);
                        if (bytesRead <= 0) break;

                        int floatsRead = bytesRead / 4;
                        if (floatsRead <= 0) break;

                        double sumSq = 0;
                        for (int j = 0; j < floatsRead; j++)
                            sumSq += buffer[j] * buffer[j];

                        waveform[i] = (float)Math.Sqrt(sumSq / floatsRead);
                    }

                    var nonZero = waveform.Where(v => v > 0).OrderBy(v => v).ToArray();
                    float refLevel = nonZero.Length > 0
                        ? nonZero[(int)Math.Clamp(nonZero.Length * 0.98, 0, nonZero.Length - 1)]
                        : 0f;

                    if (refLevel <= 1e-6f)
                        refLevel = waveform.Max();

                    if (refLevel > 0f)
                    {
                        for (int i = 0; i < waveform.Length; i++)
                        {
                            float v = Math.Clamp(waveform[i] / refLevel, 0f, 1f);
                            waveform[i] = Math.Clamp(MathF.Pow(v, 0.85f) * 0.9f, 0f, 1f);
                        }
                    }

                    return waveform;
                }
                finally
                {
                    if (stream != 0)
                        Bass.StreamFree(stream);
                }
            });
        }

        #region PLAYBACK

        public bool Play(int stream, bool restart = false)
        {
#if WINDOWS
            if (IsWasapiMode())
                return _isWasapiInitialized && BassWasapi.Start();
#endif
            return Bass.ChannelPlay(stream, restart);
        }

        public bool Pause(int stream)
        {
#if WINDOWS
            if (IsWasapiMode())
                return _isWasapiInitialized && BassWasapi.Stop(false);
#endif
            return Bass.ChannelPause(stream);
        }

        public bool Stop(int stream)
        {
#if WINDOWS
            if (IsWasapiMode())
                return _isWasapiInitialized && BassWasapi.Stop(true);
#endif
            return Bass.ChannelStop(stream);
        }

        public bool Free(int stream) => Bass.StreamFree(stream);

        public PlaybackState GetChannelState(int stream)
        {
            return Bass.ChannelIsActive(stream);
        }

        public bool SetVolume(int stream, float volume)
        {
            return Bass.ChannelSetAttribute(stream, ChannelAttribute.Volume, Math.Clamp(volume, 0f, 1f));
        }

        public float GetVolume(int stream)
        {
            Bass.ChannelGetAttribute(stream, ChannelAttribute.Volume, out var volume);
            return volume;
        }

        public double GetDuration(int stream)
        {
            long length = Bass.ChannelGetLength(stream);
            if (length < 0) return 0.0;
            return Bass.ChannelBytes2Seconds(stream, length);
        }

        public double GetPosition(int stream)
        {
            long pos = Bass.ChannelGetPosition(stream, PositionFlags.Decode);
            if (pos < 0) return 0.0;
            return Bass.ChannelBytes2Seconds(stream, pos);
        }

        public bool SetPosition(int stream, double seconds)
        {
            if (_mixerStream != 0 && Bass.ChannelIsActive(_mixerStream) != PlaybackState.Playing)
            {
                StartMixer();
                System.Threading.Thread.Sleep(5); // sleep a bit to allow the mixer to start
            }
            long bytePos = Bass.ChannelSeconds2Bytes(stream, seconds);

            bool ok = BassMix.ChannelSetPosition(stream, bytePos);
            if (!ok)
                ok = Bass.ChannelSetPosition(stream, bytePos);

            if (_mixerStream != 0 && Bass.ChannelIsActive(_mixerStream) == PlaybackState.Playing)
            {

            }

            return ok;
        }

        public OutputMode GetCurrentOutputMode() => _currentOutputMode;
        public int GetOutputSampleRate()
        {
            if (_mixerStream == 0) return 44100;

            if (Bass.ChannelGetInfo(_mixerStream, out var info))
                return info.Frequency;

            return 44100;
        }

        public bool SlideVolume(int stream, float volume, int timeMS)
        {
            return Bass.ChannelSlideAttribute(stream, ChannelAttribute.Volume, Math.Clamp(volume, 0f, 1f), timeMS);
        }

        public void SetPositionSync(int stream, double seconds, SyncProcedure callback)
        {
            long bytePos = Bass.ChannelSeconds2Bytes(stream, seconds);
            Bass.ChannelSetSync(stream, SyncFlags.Position | SyncFlags.Mixtime, bytePos, callback, IntPtr.Zero);
        }

        public void SetSlideSync(int stream, SyncProcedure callback)
        {
            Bass.ChannelSetSync(stream, SyncFlags.Slided | SyncFlags.Mixtime, 0, callback, IntPtr.Zero);
        }

        public (int Index, DeviceInfo Info)[] GetOutputDevices()
        {
            var devices = new List<(int, DeviceInfo)>();
            int index = 1;
            while (true)
            {
                try
                {
                    var info = Bass.GetDeviceInfo(index);
                    if (info.IsEnabled)
                        devices.Add((index, info));
                    index++;
                }
                catch
                {
                    break;
                }
            }
            return [.. devices];
        }

        public void SetEndCallback(int stream, SyncProcedure callback, bool mixTime = false)
        {
            var flags = SyncFlags.End | (mixTime ? SyncFlags.Mixtime : 0);
            Bass.ChannelSetSync(stream, flags, 0, callback, IntPtr.Zero);
        }

        public Errors GetLastError() => Bass.LastError;

        public void StartFFTCapture()
        {
            if (_mixerStream == 0 || _fftCaptureActive) return;

            _fftCaptureBuffer = new float[CaptureSamples];
            _writePos = 0;
            _fftCaptureFloats = 0;
            _fftDspCallback = OnFFTDspCapture;
            _fftDspHandle = Bass.ChannelSetDSP(_mixerStream, _fftDspCallback, IntPtr.Zero, 0);
            if (_fftDspHandle == 0)
            {
                Debug.WriteLine($"[AudioEngine] Failed to set FFT DSP: {Bass.LastError}");
                _fftDspCallback = null;
                _fftCaptureBuffer = Array.Empty<float>();
                return;
            }
            _fftCaptureActive = true;
        }
        public void StopFFTCapture()
        {
            if (!_fftCaptureActive) return;
            if (_fftDspHandle != 0)
            {
                Bass.ChannelRemoveDSP(_mixerStream, _fftDspHandle);
                _fftDspHandle = 0;
            }
            _fftCaptureActive = false;
            _fftDspCallback = null;
            _fftCaptureFloats = 0;
        }
        private void RebuildSpectrumBuffers()
        {
            //_spectrumFftSize = ComputeFftSize(_mixerSampleRate > 0 ? _mixerSampleRate : 44100);
            int halfSize = _spectrumFftSize / 2;
            int captureFloats = _spectrumFftSize * 2; // stereo

            if (_spectrumWorkBuffer.Length < captureFloats)
                _spectrumWorkBuffer = new float[captureFloats];
            if (_spectrumWindowBuffer.Length < _spectrumFftSize)
                _spectrumWindowBuffer = new float[_spectrumFftSize];
            if (_spectrumComplexBuffer.Length < _spectrumFftSize)
                _spectrumComplexBuffer = new System.Numerics.Complex[_spectrumFftSize];
            if (_spectrumMagnitudes.Length < halfSize)
                _spectrumMagnitudes = new float[halfSize];
        }
        private void OnFFTDspCapture(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            if (length <= 0) return;
            int floatsRead = length / sizeof(float);
            if (floatsRead <= 0) return;

            if (_dspTempBuffer.Length < floatsRead)
                _dspTempBuffer = new float[floatsRead];

            Marshal.Copy(buffer, _dspTempBuffer, 0, floatsRead);

            lock (_fftLock)
            {
                for (int i = 0; i < floatsRead; i++)
                {
                    _fftCaptureBuffer[_writePos++] = _dspTempBuffer[i];

                    if (_writePos >= _fftCaptureBuffer.Length)
                        _writePos = 0;
                }

                _fftCaptureFloats =
                    Math.Min(
                        _fftCaptureFloats + floatsRead,
                        _fftCaptureBuffer.Length);
            }
        }
        public (float[] Magnitudes, int FftSize) GetSpectrumMagnitudes()
        {
            if (_mixerStream == 0 || _spectrumFftSize == 0)
                return (Array.Empty<float>(), 0);

            int captureFloats = _spectrumFftSize * 2;
            int available;

            // 1. Copy the last fftSize stereo frames from the ring under lock.
            lock (_fftLock)
            {
                available = Math.Min(_fftCaptureFloats, _fftCaptureBuffer.Length);
                if (available < captureFloats)
                    return (Array.Empty<float>(), 0);

                int start = _writePos - captureFloats;
                if (start < 0) start += _fftCaptureBuffer.Length;

                if (start + captureFloats <= _fftCaptureBuffer.Length)
                {
                    Array.Copy(_fftCaptureBuffer, start, _spectrumWorkBuffer, 0, captureFloats);
                }
                else
                {
                    int first = _fftCaptureBuffer.Length - start;
                    Array.Copy(_fftCaptureBuffer, start, _spectrumWorkBuffer, 0, first);
                    Array.Copy(_fftCaptureBuffer, 0, _spectrumWorkBuffer, first, captureFloats - first);
                }
            }

            // 2. Mix to mono + Hanning window (outside lock).
            for (int i = 0; i < _spectrumFftSize; i++)
            {
                float l = _spectrumWorkBuffer[i * 2];
                float r = _spectrumWorkBuffer[i * 2 + 1];
                double w = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (_spectrumFftSize - 1)));
                _spectrumWindowBuffer[i] = (float)((l + r) * 0.5 * w);
            }

            // 3. FFT.
            ComputeSpectrumFFT(_spectrumWindowBuffer, _spectrumComplexBuffer, _spectrumFftSize);

            // 4. Magnitudes.
            int half = _spectrumFftSize / 2;
            for (int i = 0; i < half; i++)
            {
                double re = _spectrumComplexBuffer[i].Real;
                double im = _spectrumComplexBuffer[i].Imaginary;
                double power = (re * re + im * im) / (_spectrumFftSize * (double)_spectrumFftSize);
                _spectrumMagnitudes[i] = (float)Math.Sqrt(power);
            }

            return (_spectrumMagnitudes, _spectrumFftSize);
        }
        public int GetCapturedPCMData(float[] destination)
        {
            lock (_fftLock)
            {
                if (_fftCaptureFloats == 0) return 0;

                int available = Math.Min(_fftCaptureFloats, _fftCaptureBuffer.Length);
                int requested = Math.Min(destination.Length, available);

                int start = _writePos - requested;

                if (start < 0)
                {
                    start += _fftCaptureBuffer.Length;
                }

                if (start + requested <= _fftCaptureBuffer.Length)
                {
                    // no wrap
                    Array.Copy(_fftCaptureBuffer, start, destination, 0, requested);
                }
                else
                {
                    int first = _fftCaptureBuffer.Length - start;

                    Array.Copy(
                        _fftCaptureBuffer,
                        start,
                        destination,
                        0,
                        first);

                    Array.Copy(
                        _fftCaptureBuffer,
                        0,
                        destination,
                        first,
                        requested - first);
                }
                return requested;
            }
        }

        //internal static int ComputeFftSize(int sampleRate)
        //{
        //    //const double targetBinHz = 2.5;
        //    //int desired = (int)Math.Ceiling(sampleRate / targetBinHz);
        //    //int pow2 = 1;
        //    //while (pow2 < desired) pow2 <<= 1;
        //    //return Math.Clamp(pow2, 16384, 131072);
        //    return 32768;
        //}
        private static void ComputeSpectrumFFT(
        float[] input,
        System.Numerics.Complex[] output,
        int length)
        {
            for (int i = 0; i < length; i++)
                output[i] = new System.Numerics.Complex(input[i], 0);

            int bits = (int)Math.Log2(length);
            for (int i = 0; i < length; i++)
            {
                int j = BitReverse(i, bits);
                if (i < j)
                    (output[i], output[j]) = (output[j], output[i]);
            }

            for (int stage = 1; stage <= bits; stage++)
            {
                int halfSize = 1 << (stage - 1);
                int fullSize = 1 << stage;
                double angle = -2.0 * Math.PI / fullSize;
                var wn = new System.Numerics.Complex(Math.Cos(angle), Math.Sin(angle));
                for (int k = 0; k < length; k += fullSize)
                {
                    var w = System.Numerics.Complex.One;
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

        #endregion

        #region WASAPI

#if WINDOWS
        private bool InitializeWasapiForCurrentMode(int sampleRate)
        {
            if (!IsWasapiMode())
            {
                _isWasapiInitialized = false;
                return true;
            }

            _wasapiProc = WasapiDataProc;

            bool exclusive = _currentOutputMode == OutputMode.WasapiExclusive;
            var initFlags = exclusive ? WasapiInitFlags.Exclusive : WasapiInitFlags.Shared;

            _isWasapiInitialized = BassWasapi.Init(
                -1, sampleRate, 2,
                initFlags,
                0.05f, 0.01f,
                _wasapiProc, IntPtr.Zero
            );

            if (!_isWasapiInitialized && !exclusive)
            {
                _isWasapiInitialized = BassWasapi.Init(
                    -1, sampleRate, 2,
                    WasapiInitFlags.AutoFormat,
                    0.05f, 0.01f,
                    _wasapiProc, IntPtr.Zero
                );
            }

            if (!_isWasapiInitialized && exclusive)
            {
                Debug.WriteLine("[AudioEngine] WASAPI Exclusive failed, falling back to Shared...");
                _currentOutputMode = OutputMode.WasapiShared;

                _isWasapiInitialized = BassWasapi.Init(
                    -1, sampleRate, 2,
                    WasapiInitFlags.AutoFormat,
                    0.05f, 0.01f,
                    _wasapiProc, IntPtr.Zero
                );
            }

            if (_isWasapiInitialized)
            {
                var info = BassWasapi.Info;
                _currentOutputSampleRate = info.Frequency > 0 ? info.Frequency : sampleRate;
                _currentOutputChannels = info.Channels > 0 ? info.Channels : 2;
                _lastSampleRate = _currentOutputSampleRate;

                Debug.WriteLine($"[AudioEngine] WASAPI initialized: {info.Frequency}Hz / {info.Channels}ch " +
                    $"{(info.IsExclusive ? "Exclusive" : "Shared")}");
            }
            else
            {
                Debug.WriteLine($"[AudioEngine] WASAPI Init failed: {Bass.LastError}");
            }

            return _isWasapiInitialized;
        }

        private void ReopenWasapi(int samplerate, int channels)
        {
            if (!IsWasapiMode()) return;

            if (_isWasapiInitialized)
            {
                BassWasapi.Stop(true);
                BassWasapi.Free();
                _isWasapiInitialized = false;
            }

            InitializeWasapiForCurrentMode(samplerate);
        }

        private int WasapiDataProc(IntPtr buffer, int length, IntPtr user)
        {
            if (_mixerStream == 0) return 0;
            return Bass.ChannelGetData(_mixerStream, buffer, length | (int)DataFlags.Float);
        }
#endif

        private bool IsWasapiMode()
        {
#if WINDOWS
            return _currentOutputMode == OutputMode.WasapiShared
                || _currentOutputMode == OutputMode.WasapiExclusive;
#else
            return false;
#endif
        }

        #endregion

        private void Teardown()
        {
            StopFFTCapture();
#if WINDOWS
            if (_isWasapiInitialized)
            {
                BassWasapi.Stop(true);
                BassWasapi.Free();
                _isWasapiInitialized = false;
            }
#endif

            if (_flacPluginHandle != 0)
            {
                Bass.PluginFree(_flacPluginHandle);
                _flacPluginHandle = 0;
            }
            if (_opusPluginHandle != 0)
            {
                Bass.PluginFree(_opusPluginHandle);
                _opusPluginHandle = 0;
            }

            if (_isInitialized)
            {
                Bass.Free();
                _isInitialized = false;
            }

            DestroyMixer();
#if WINDOWS
            _wasapiProc = null;
#endif
            _lastRequestedSampleRate = 44100;
            _lastRequestedChannels = 2;
            _currentOutputSampleRate = 44100;
            _currentOutputChannels = 2;
        }

        public void Dispose() => Teardown();
    }
}
