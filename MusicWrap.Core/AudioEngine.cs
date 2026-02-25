using System;
using System.Collections.Generic;
using System.Text;
using Un4seen.Bass;


namespace MusicWrap.Core
{
    public class AudioEngine : IDisposable
    {
        private int _currentStream;
        private bool _isInitialized;

        public bool Initialize(int deviceIndex = -1, int sampleRate = 44100)
        {
            if (_isInitialized)
                return true;
            _isInitialized = Bass.BASS_Init(deviceIndex, sampleRate, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            return _isInitialized;
        }
        public int CreateStream(string filePath)
        {
            if (!_isInitialized) throw new InvalidOperationException("Audio engine not initialized.");

            return Bass.BASS_StreamCreateFile(filePath, 0, 0, BASSFlag.BASS_DEFAULT);
        }
        public bool Play(int stream, bool restart = false)
        {
            return Bass.BASS_ChannelPlay(stream, restart);
        }
        public bool Pause(int stream)
        {
            return Bass.BASS_ChannelPause(stream);
        }
        public bool Stop(int stream)
        {
            return Bass.BASS_ChannelStop(stream);
        }
        public bool Free(int stream)
        {
            return Bass.BASS_StreamFree(stream);
        }
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
            return Bass.BASS_ChannelBytes2Seconds(stream, length);
        }
        public double GetPosition(int stream)
        {
            long pos = Bass.BASS_ChannelGetPosition(stream);
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
        public (int Index, BASS_DEVICEINFO Info)[] GetOutputDevices()
        {
            var devices = new List<(int, BASS_DEVICEINFO)>();
            int index = 0;
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
        public void SetEndCallback(int stream, SYNCPROC callback)
        {
            Bass.BASS_ChannelSetSync(stream, BASSSync.BASS_SYNC_END, 0, callback, IntPtr.Zero);
        }
        public BASSError GetLastError()
        {
            return Bass.BASS_ErrorGetCode();
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                if (_currentStream != 0)
                    Free(_currentStream);
                Bass.BASS_Free();
                _isInitialized = false;
            }
        }
    }
}
