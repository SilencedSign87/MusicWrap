using System;
using System.Collections.Generic;
using System.Drawing.Interop;
using System.Security.Permissions;
using System.Text;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;


namespace MusicWrap.Core
{
    public class AudioEngine : IDisposable
    {
        private bool _isInitialized;

        public bool Initialize(int deviceIndex = -1, int sampleRate = 44100)
        {
            if (_isInitialized)
                return true;
            _isInitialized = Bass.BASS_Init(deviceIndex, sampleRate, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            return _isInitialized;
        }

        #region MIXER

        public int CreateMixer(int sampleRate = 44100)
        {
            return BassMix.BASS_Mixer_StreamCreate(sampleRate, 2, BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_MIXER_NONSTOP); // gapless mixing
        }
        public int CreateDecodeStream(string filePath)
        {
            return Bass.BASS_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);
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
            var info = Bass.BASS_GetInfo();
            return Math.Max(0, info.latency);
        }

        public int CreateStream(string filePath)
        {
            if (!_isInitialized) throw new InvalidOperationException("Audio engine not initialized.");

            return Bass.BASS_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_DEFAULT);
        }
        #region PLAYBACK
        public bool Play(int stream, bool restart = false) => Bass.BASS_ChannelPlay(stream, restart);
        public bool Pause(int stream) => Bass.BASS_ChannelPause(stream);
        public bool Stop(int stream) => Bass.BASS_ChannelStop(stream);
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
            return Bass.BASS_ChannelSetPosition(stream, bytePos);
        }
        public bool ChangeSampleRate(int deviceIndex, int sampleRate)
        {
            if (!_isInitialized) return false;
            Bass.BASS_Free();
            _isInitialized = false;
            return Initialize(deviceIndex, sampleRate);
        }
        public bool ChangeOutputDevice(int deviceIndex)
        {
            if (!_isInitialized) return false;
            Bass.BASS_Free();
            _isInitialized = false;
            return Initialize(deviceIndex);
        }
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
            // Bass.BASS_ChannelSetSync(stream, BASSSync.BASS_SYNC_END | BASSSync.BASS_SYNC_MIXTIME, 0, callback, IntPtr.Zero);
        }
        public BASSError GetLastError()
        {
            return Bass.BASS_ErrorGetCode();
        }
        #endregion

        public void Dispose()
        {
            if (_isInitialized)
            {
                Bass.BASS_Free();
                _isInitialized = false;
            }
        }
    }
}
