using MusicWrap.Data.User.Models;
using System.Diagnostics;
using ManagedBass;
using ManagedBass.Mix;
using NetVips;


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


        // FFT buffers for spectrum analysis
        private const int _spectrumFftSize = 8192;
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

#if WINDOWS
            if (IsWasapiMode())
                return _isWasapiInitialized && BassWasapi.Start();
#endif
            return Bass.ChannelPlay(_mixerStream, false);
        }

        public void FlushOutputBuffer()
        {
#if WINDOWS
            if (!IsWasapiMode() || !_isWasapiInitialized || !BassWasapi.IsStarted) return;
            BassWasapi.Stop(true);
            BassWasapi.Start();
#endif
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
            long bytePos = Bass.ChannelSeconds2Bytes(stream, seconds);
            bool ok = false;
#if WINDOWS
            if (IsWasapiMode() && _isWasapiInitialized && BassWasapi.IsStarted)
            {
                BassWasapi.Stop(true);
                ok = BassMix.ChannelSetPosition(stream, bytePos);
                if (!ok)
                    ok = Bass.ChannelSetPosition(stream, bytePos);
                BassWasapi.Start();
                return ok;
            }
#endif
            if (_mixerStream != 0 && Bass.ChannelIsActive(_mixerStream) != PlaybackState.Playing)
            {
                StartMixer();
                Thread.Sleep(1); // sleep a bit to allow the mixer to start
            }

            ok = BassMix.ChannelSetPosition(stream, bytePos);

            if (!ok)
                ok = Bass.ChannelSetPosition(stream, bytePos);

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

        public (float[] Magnitudes, int FftSize) GetSpectrumMagnitudes()
        {
            if (_mixerStream == 0) return (Array.Empty<float>(), 0);

#if WINDOWS
            if (_isWasapiInitialized && IsWasapiMode())
                return GetWasapiSpectrum();
#endif

            return GetMixerSpectrum();
        }

        private static int GetFFTFlag(int fftSize) => (int) DataFlags.FFT256 | (int) Math.Log2(fftSize / 256);

#if WINDOWS
        private (float[] Magnitudes, int FftSize) GetWasapiSpectrum()
        {
            if (_spectrumMagnitudes.Length < _spectrumFftSize / 2)
                _spectrumMagnitudes = new float[_spectrumFftSize / 2];

            int flags = GetFFTFlag(_spectrumFftSize) | (int)DataFlags.Float;

            if (BassWasapi.GetData(_spectrumMagnitudes, flags) < 0)
            {
                Debug.WriteLine($"[AudioEngine] GetData FFT failed: {Bass.LastError}");
                return (Array.Empty<float>(), 0);
            }

            return (_spectrumMagnitudes, _spectrumFftSize);
        }
#endif
        private (float[] Magnitudes, int FftSize) GetMixerSpectrum()
        {
            if (_spectrumMagnitudes.Length < _spectrumFftSize / 2)
                _spectrumMagnitudes = new float[_spectrumFftSize / 2];

            int flags = GetFFTFlag(_spectrumFftSize) | (int)DataFlags.Float;

            if (Bass.ChannelGetData(_mixerStream, _spectrumMagnitudes, flags) <= 0)
                return (Array.Empty<float>(), 0);

            return (_spectrumMagnitudes, _spectrumFftSize);
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
            var initFlags = exclusive ? (WasapiInitFlags.Exclusive | WasapiInitFlags.Buffer) : (WasapiInitFlags.Shared | WasapiInitFlags.Buffer);

            float fftBufferSeconds = (float)_spectrumFftSize / sampleRate + 0.03f;
            const float periodSeconds = 0.02f;

            _isWasapiInitialized = BassWasapi.Init(
                Device: -1,
                Frequency: sampleRate,
                Channels: 2,
                Flags: initFlags,
                Buffer: fftBufferSeconds,
                Period: periodSeconds,
                Procedure: _wasapiProc,
                User: IntPtr.Zero
            );

            if (!_isWasapiInitialized && !exclusive)
            {
                _isWasapiInitialized = BassWasapi.Init(
                    -1,
                    sampleRate,
                    2,
                    WasapiInitFlags.AutoFormat | WasapiInitFlags.Buffer,
                    fftBufferSeconds,
                    periodSeconds,
                    _wasapiProc,
                    IntPtr.Zero
                );
            }

            if (!_isWasapiInitialized && exclusive)
            {
                Debug.WriteLine("[AudioEngine] WASAPI Exclusive failed, falling back to Shared...");
                _currentOutputMode = OutputMode.WasapiShared;

                _isWasapiInitialized = BassWasapi.Init(
                    -1,
                    sampleRate,
                    2,
                    WasapiInitFlags.AutoFormat | WasapiInitFlags.Buffer,
                    fftBufferSeconds,
                    periodSeconds,
                    _wasapiProc,
                    IntPtr.Zero
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
