using Microsoft.Extensions.Logging;
using MusicWrap.Data.User.Models;
using SixLabors.ImageSharp.Metadata;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Interop;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Flac;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.BassWasapi;


namespace MusicWrap.Core
{
    public class AudioEngine : IDisposable
    {

        private bool _isInitialized;
        private bool _isWasapiInitialized;
        private int _flacPluginHandle;
        private int _opusPluginHandle;

        private OutputMode _currentOutputMode = OutputMode.WasapiShared;

        private WASAPIPROC? _wasapiProc;

        private int _outputMixerStream;
        private int _lastDeviceIndex = -1;
        private int _lastSampleRate = 44100;
        private int _lastRequestedSampleRate = 44100;
        private int _lastRequestedChannels = 2;
        private int _currentOutputSampleRate = 44100;
        private int _currentOutputChannels = 2;

        public OutputMode CurrentOutputMode => _currentOutputMode;
        public int CurrentOutputSampleRate => _currentOutputSampleRate;
        public int CurrentOutputChannels => _currentOutputChannels;

        public bool Initialize(int deviceIndex = -1, int sampleRate = 44100, OutputMode outputmode = OutputMode.WasapiShared)
        {
            if (_isInitialized)
                return true;
            _lastDeviceIndex = deviceIndex;
            _lastSampleRate = sampleRate > 0 ? sampleRate : 44100;
            _lastRequestedSampleRate = _lastSampleRate;
            _lastRequestedChannels = 2;

            _currentOutputMode = outputmode;

            // Configure buffer
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_BUFFER, 90);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATEPERIOD, 5);
            // Configure network timeouts
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_TIMEOUT, 7000);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_READTIMEOUT, 7000);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PREBUF, 0);

            _isInitialized = Bass.BASS_Init(deviceIndex, sampleRate, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            if (!_isInitialized) return false;

            _flacPluginHandle = Bass.BASS_PluginLoad("bassflac.dll");
            _opusPluginHandle = Bass.BASS_PluginLoad("bassopus.dll");

            if (_flacPluginHandle == 0)
            {
                var err = Bass.BASS_ErrorGetCode();
                Debug.WriteLine($"Failed to load FLAC plugin: {err}");
            }
            if (_opusPluginHandle == 0)
            {
                var err = Bass.BASS_ErrorGetCode();
                Debug.WriteLine($"Failed to load Opus plugin: {err}");
            }

            // initialize wasapi
            if (!InitializeWasapiForCurrentMode(_lastSampleRate))
            {
                Teardown();
                return false;
            }

            return _isInitialized;
        }
        public bool Reinitialize(int deviceIndex = -1, int sampleRate = 44100, OutputMode outputmode = OutputMode.WasapiShared)
        {
            Teardown();
            return Initialize(deviceIndex, sampleRate, outputmode);
        }
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

            return _currentOutputSampleRate > 0 ? _currentOutputSampleRate : requestedRate;
        }
        public void AttachOutputToMixer(int mixerStream, int sampleRate, int channels = 2)
        {
            _outputMixerStream = mixerStream;
            _ = sampleRate;
            _ = channels;
        }

        #region MIXER

        public int CreateMixer(int sampleRate = 44100)
        {
            var flags = BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_MIXER_NONSTOP; // gapless mixing
            if (IsWasapiMode())
            {
                flags |= BASSFlag.BASS_STREAM_DECODE;
            }

            int mixRate = sampleRate > 0 ? sampleRate : 44100;
            if (IsWasapiMode() && _currentOutputSampleRate > 0)
            {
                mixRate = _currentOutputSampleRate;
            }

            return BassMix.BASS_Mixer_StreamCreate(mixRate, 2, flags); // gapless mixing
        }
        public int CreateDecodeStream(string filePath)
        {
            return Bass.BASS_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);
        }
        public int CreateDecodeStreamFromUrl(string url)
        {
            if (!_isInitialized) throw new InvalidOperationException("Audio engine not initialized.");
            if (string.IsNullOrWhiteSpace(url)) return 0;

            //nint UserAgentPtr = Marshal.StringToHGlobalAnsi("MusicWrap/1.0");
            //Bass.BASS_SetConfigPtr(BASSConfig.BASS_CONFIG_NET_AGENT, UserAgentPtr);

            var flags = BASSFlag.BASS_STREAM_DECODE | 
                        BASSFlag.BASS_SAMPLE_FLOAT | 
                        BASSFlag.BASS_STREAM_PRESCAN | 
                        BASSFlag.BASS_STREAM_BLOCK;
            var task = Task.Run(()=> Bass.BASS_StreamCreateURL(url, 0, flags, null, IntPtr.Zero));
            if (!task.Wait(TimeSpan.FromSeconds(10))){
                Debug.WriteLine($"[AudioEngine] Timeout creating stream from URL: {url}");
                return 0;
            }
            return task.Result;
        }
        public bool AddToMixer(int mixerStream, int stream, BASSFlag flags = BASSFlag.BASS_DEFAULT)
        {
            return BassMix.BASS_Mixer_StreamAddChannel(mixerStream, stream, flags);
        }
        public bool RemoveFromMixer(int stream)
        {
            return BassMix.BASS_Mixer_ChannelRemove(stream);
        }
        public double GetMixerPosition(int stream)
        {
            long pos = BassMix.BASS_Mixer_ChannelGetPosition(stream);
            if (pos < 0) return 0.0;
            return Bass.BASS_ChannelBytes2Seconds(stream, pos);
        }

        #endregion

        public int GetDeviceLatencyMs()
        {
            if (IsWasapiMode() && _isWasapiInitialized)
            {
                var info = BassWasapi.BASS_WASAPI_GetInfo();
                var bufferSeconds = Convert.ToDouble(info.buflen);
                if (bufferSeconds > 0)
                {
                    return Math.Max(1, (int)Math.Round(bufferSeconds * 1000.0));
                }
            }
            var bassInfo = Bass.BASS_GetInfo();
            return Math.Max(0, bassInfo.latency);
        }

        public int CreateStream(string filePath)
        {
            if (!_isInitialized) throw new InvalidOperationException("Audio engine not initialized.");

            return Bass.BASS_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_DEFAULT);
        }
        public static async Task<float[]> GetWaveFromDataAsync(string filePath, int dataPoints = 2000)
        {
            // Run the audio processing in a separate thread to avoid blocking the UI
            return await Task.Run(() =>
            {
                int stream = Bass.BASS_StreamCreateFile(filePath, 0, 0,
                    BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_PRESCAN);
                if (stream == 0)
                    return Array.Empty<float>();

                try
                {
                    long lenghtBytes = Bass.BASS_ChannelGetLength(stream);
                    if (lenghtBytes <= 0) return Array.Empty<float>();

                    long bytesPerChunck = lenghtBytes / dataPoints;

                    if (bytesPerChunck <= 0) bytesPerChunck = 4;

                    int floatsPerChunk = (int)(bytesPerChunck / 4); // 4 bytes per float
                    float[] buffer = new float[floatsPerChunk];
                    float[] waveform = new float[dataPoints];

                    float maxPeakInFile = 0f;

                    for (int i = 0; i < dataPoints; i++)
                    {
                        int bytesRead = Bass.BASS_ChannelGetData(stream, buffer, (int)bytesPerChunck);
                        if (bytesRead <= 0) break;

                        int floatsRead = bytesRead / 4;
                        if (floatsRead <= 0) break;

                        float peak = 0f;

                        double sumSq = 0;
                        for (int j = 0; j < floatsRead; j++)
                        {
                            float s = buffer[j];
                            sumSq += s * s;
                        }
                        float rms = (float)Math.Sqrt(sumSq / floatsRead);
                        waveform[i] = rms;

                        if (peak > maxPeakInFile)
                            maxPeakInFile = peak;
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
                            float v = waveform[i] / refLevel;
                            v = Math.Clamp(v, 0f, 1f);

                            v = MathF.Pow(v, 0.85f) * 0.9f;

                            waveform[i] = Math.Clamp(v, 0f, 1f);
                        }
                    }

                    return waveform;

                }
                finally
                {
                    if (stream != 0)
                        Bass.BASS_StreamFree(stream);
                }
            });
        }

        #region PLAYBACK
        public bool Play(int stream, bool restart = false)
        {
            if (IsWasapiMode())
            {
                return _isWasapiInitialized && BassWasapi.BASS_WASAPI_Start();
            }
            return Bass.BASS_ChannelPlay(stream, restart);
        }
        public bool Pause(int stream)
        {
            if (IsWasapiMode())
            {
                return _isWasapiInitialized && BassWasapi.BASS_WASAPI_Stop(false);
            }
            return Bass.BASS_ChannelPause(stream);
        }
        public bool Stop(int stream)
        {
            if (IsWasapiMode())
            {
                return _isWasapiInitialized && BassWasapi.BASS_WASAPI_Stop(true);
            }
            return Bass.BASS_ChannelStop(stream);
        }
        public bool Free(int stream) => Bass.BASS_StreamFree(stream);

        public BASSActive GetChannelState(int stream)
        {
            return Bass.BASS_ChannelIsActive(stream);
        }
        public bool SetVolume(int stream, float volume)
        {
            return Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, Math.Clamp(volume, 0f, 1f));
        }
        public float GetVolume(int stream)
        {
            float volume = 0f;
            Bass.BASS_ChannelGetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, ref volume);
            return volume;
        }
        public double GetDuration(int stream)
        {
            long length = Bass.BASS_ChannelGetLength(stream);
            if (length < 0) return 0.0;
            return Bass.BASS_ChannelBytes2Seconds(stream, length);
        }
        public double GetPosition(int stream)
        {
            long pos = Bass.BASS_ChannelGetPosition(stream);
            if (pos < 0) return 0.0;
            return Bass.BASS_ChannelBytes2Seconds(stream, pos);
        }
        public bool SetPosition(int stream, double seconds)
        {
            long bytePos = Bass.BASS_ChannelSeconds2Bytes(stream, seconds);
            if (BassMix.BASS_Mixer_ChannelSetPosition(stream, bytePos))
            {
                return true;
            }
            return Bass.BASS_ChannelSetPosition(stream, bytePos);
        }
        public OutputMode GetCurrentOutputMode() => _currentOutputMode;
        public bool SlideVolume(int stream, float volume, int timeMS)
        {
            return Bass.BASS_ChannelSlideAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, Math.Clamp(volume, 0f, 1f), timeMS);
        }
        public void SetPositionSync(int stream, double seconds, SYNCPROC callback)
        {
            long bytePos = Bass.BASS_ChannelSeconds2Bytes(stream, seconds);
            Bass.BASS_ChannelSetSync(stream, BASSSync.BASS_SYNC_POS | BASSSync.BASS_SYNC_MIXTIME, bytePos, callback, IntPtr.Zero);
        }
        public void SetSlideSync(int stream, SYNCPROC callback)
        {
            Bass.BASS_ChannelSetSync(stream, BASSSync.BASS_SYNC_SLIDE | BASSSync.BASS_SYNC_MIXTIME, 0, callback, IntPtr.Zero);
        }
        public (int Index, BASS_DEVICEINFO Info)[] GetOutputDevices()
        {
            var devices = new List<(int, BASS_DEVICEINFO)>();
            int index = 1;
            BASS_DEVICEINFO info;
            while (true)
            {
                try
                {
                    info = Bass.BASS_GetDeviceInfo(index);
                }
                catch
                {
                    break;
                }
                if (info == null)
                    break;
                if (info.IsEnabled)
                    devices.Add((index, info));
                index++;
            }
            return [.. devices];
        }
        public void SetEndCallback(int stream, SYNCPROC callback, bool mixTime = false)
        {
            var flags = BASSSync.BASS_SYNC_END | (mixTime ? BASSSync.BASS_SYNC_MIXTIME : 0);
            Bass.BASS_ChannelSetSync(stream, flags, 0, callback, IntPtr.Zero);
        }
        public BASSError GetLastError()
        {
            return Bass.BASS_ErrorGetCode();
        }
        private bool InitializeWasapiForCurrentMode(int sampleRate)
        {
            if (!IsWasapiMode())
            {
                _isWasapiInitialized = false;
                return true;
            }

            _wasapiProc = WasapiDataProc;

            const int device = -1; // default device
            bool exclusive = _currentOutputMode == OutputMode.WasapiExclusive;
            var flags = (BASSWASAPIInit)0;
            if (exclusive)
            {
                flags |= BASSWASAPIInit.BASS_WASAPI_EXCLUSIVE;
            }

            _isWasapiInitialized = BassWasapi.BASS_WASAPI_Init(
                device,
                sampleRate,
                2,
                flags,
                0.05f, // buffer
                0.01f, // period
                _wasapiProc,
                IntPtr.Zero
                );

            // In shared mode, try exact format first. If unsupported, allow autoformat fallback.
            if (!_isWasapiInitialized && !exclusive)
            {
                _isWasapiInitialized = BassWasapi.BASS_WASAPI_Init(
                    device,
                    sampleRate,
                    2,
                    BASSWASAPIInit.BASS_WASAPI_AUTOFORMAT,
                    0.05f,
                    0.01f,
                    _wasapiProc,
                    IntPtr.Zero
                    );
            }

            if (!_isWasapiInitialized && exclusive) // fallback
            {
                Debug.WriteLine("[AudioEngine] WASAPI Exlusive failed, trying shared...");
                _currentOutputMode = OutputMode.WasapiShared;

                _isWasapiInitialized = BassWasapi.BASS_WASAPI_Init(
                    device,
                    sampleRate,
                    2,
                    BASSWASAPIInit.BASS_WASAPI_AUTOFORMAT,
                    0.05f, // buffer
                    0.01f, // period
                    _wasapiProc,
                    IntPtr.Zero
                    );
            }
            if (_isWasapiInitialized)
            {
                var info = BassWasapi.BASS_WASAPI_GetInfo();
                _currentOutputSampleRate = info.freq > 0 ? info.freq : sampleRate;
                _currentOutputChannels = info.chans > 0 ? info.chans : 2;
                _lastSampleRate = _currentOutputSampleRate;
            }
            if (!_isWasapiInitialized)
            {
                Debug.WriteLine("[AudioEngine] WASAPI Init failed with error: " + GetLastError());
            }
            return _isWasapiInitialized;
        }
        private void ReopenWasapi(int samplerate, int channels)
        {
            if (!IsWasapiMode())
            {
                return;
            }
            if (_isWasapiInitialized)
            {
                BassWasapi.BASS_WASAPI_Stop(true);
                BassWasapi.BASS_WASAPI_Free();
                _isWasapiInitialized = false;
            }

            _ = channels; // TODO: multichannel
            InitializeWasapiForCurrentMode(samplerate);
        }
        private int WasapiDataProc(IntPtr buffer, int length, IntPtr user)
        {
            if (_outputMixerStream == 0)
            {
                return 0;
            }

            return Bass.BASS_ChannelGetData(_outputMixerStream, buffer, length | (int)BASSData.BASS_DATA_FLOAT);
        }
        private bool IsWasapiMode()
        {
            return _currentOutputMode == OutputMode.WasapiShared
                || _currentOutputMode == OutputMode.WasapiExclusive;
        }
        private void Teardown()
        {
            if (_isWasapiInitialized)
            {
                BassWasapi.BASS_WASAPI_Stop(true);
                BassWasapi.BASS_WASAPI_Free();
                _isWasapiInitialized = false;
            }

            if (_flacPluginHandle != 0)
            {
                Bass.BASS_PluginFree(_flacPluginHandle);
                _flacPluginHandle = 0;
            }
            if (_opusPluginHandle != 0) {
                Bass.BASS_PluginFree(_opusPluginHandle);
                _opusPluginHandle = 0;
            }

            if (_isInitialized)
            {
                Bass.BASS_Free();
                _isInitialized = false;
            }

            _outputMixerStream = 0;
            _wasapiProc = null;
            _lastRequestedSampleRate = 44100;
            _lastRequestedChannels = 2;
            _currentOutputSampleRate = 44100;
            _currentOutputChannels = 2;
        }
        #endregion

        public void Dispose()
        {
            Teardown();
        }
    }
}
