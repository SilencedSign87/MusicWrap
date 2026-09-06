using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace MusicWrap.UI.ViewModels
{
    public partial class DeviceViewModel : ObservableObject
    {
        // Player state properties
        [ObservableProperty]
        public partial string CurrentDeviceName { get; set; } = "Default Device";
        [ObservableProperty]
        public partial string CurrentDeviceSampleRateName { get; set; } = "Loading information...";

        // Settings
        [ObservableProperty]
        public partial List<DeviceDefinition> AvailableDevices { get; set; } = [];
        [ObservableProperty]
        public partial DeviceDefinition? PreferredDevice { get; set; }
        [ObservableProperty]
        public partial OutputMode PreferredOutputMode { get; set; } = OutputMode.WasapiShared;
        [ObservableProperty]
        public partial int PreferredSampleRateIndex { get; set; } = 0;


        private bool _hasInitialize = false;
        private readonly IMusicPlayerService _player;
        private readonly MusicWrapSettings _userSettings;

        private readonly SampleRatePreference[] SampleRates = [
            SampleRatePreference.Auto,
            SampleRatePreference.Hz44100,
            SampleRatePreference.Hz48000,
            SampleRatePreference.Hz88200,
            SampleRatePreference.Hz96000,
            SampleRatePreference.Hz176400,
            SampleRatePreference.Hz192000
            ];
        public List<OutputMode> Outputmodes { get; } = [OutputMode.WasapiShared, OutputMode.WasapiExclusive];

        public DeviceViewModel(IMusicPlayerService player, MusicWrapSettings userSettings)
        {
            _player = player;
            _userSettings = userSettings;
        }

        public void LoadData()
        {
            LoadDevices();
            LoadCurrentPlayerState();
            LoadUserSettings();

            _player.TrackChanged += OnPlayerTrackChanged;
            _player.DeviceIndexChanged += OnPlayerDeviceChanged;
            _player.SampleRateChanged += OnPlayerSampleRateChanged;
            _player.OutputModeChanged += OnPlayerOutputModeChanged;
            _hasInitialize = true;
        }
        public void UnloadData()
        {
            _player.TrackChanged -= OnPlayerTrackChanged;
            _player.DeviceIndexChanged -= OnPlayerDeviceChanged;
            _player.SampleRateChanged -= OnPlayerSampleRateChanged;
            _player.OutputModeChanged -= OnPlayerOutputModeChanged;
        }
        #region Event handlers
        private void OnPlayerTrackChanged(object? sender, string e) => LoadCurrentPlayerState();
        private void OnPlayerDeviceChanged(object? sender, int e) => LoadCurrentPlayerState();
        private void OnPlayerSampleRateChanged(object? sender, SampleRateChangedEventArgs e) => LoadCurrentPlayerState();
        private void OnPlayerOutputModeChanged(object? sender, OutputMode e) => LoadCurrentPlayerState();
        #endregion
        #region Partial methods
        partial void OnPreferredSampleRateIndexChanged(int value)
        {
            if (!_hasInitialize) return;
            var samplerate = SampleRates[value];
            if (_player.CurrentSampleRate != samplerate)
            {
                _player.ChangeSampleRate(samplerate);
            }
        }
        partial void OnPreferredOutputModeChanged(OutputMode value)
        {
            if (!_hasInitialize) return;
            if (_player.CurrentOutputMode != value)
            {
                _player.ChangeOutputMode(value);
            }
        }
        partial void OnPreferredDeviceChanged(DeviceDefinition? value)
        {
            if (!_hasInitialize) return;
            if(value is not null && _player.CurrentDeviceIndex != value.Index)
            {
                _player.ChangeOutputDevice(value.Index);
            }
        }
        #endregion
        #region Internal

        private void LoadDevices()
        {
            AvailableDevices.Clear();
            var devices = _player.GetAvailableDevices();
            foreach (var device in devices)
            {
                AvailableDevices.Add(new DeviceDefinition { Index = device.Index, Name = device.Name });
            }
        }
        private void LoadCurrentPlayerState()
        {
            CurrentDeviceSampleRateName = _player.GetCurrentOutputSampleRate().ToString();
            var deviceIndex = _player.CurrentDeviceIndex;

            var device = AvailableDevices.Find(d => d.Index == deviceIndex);
            if (device != null)
            {
                CurrentDeviceName = device.Name;
            }
            else
            {
                CurrentDeviceName = "Unknown Device";
            }
        }

        private void LoadUserSettings()
        {
            var storedSR = _userSettings.Playback.PreferredSampleRate;
            var storedOutputMode = _userSettings.Playback.PreferredOutputMode;
            var storedDeviceIndex = _userSettings.Playback.PreferredDeviceIndex;

            var device = AvailableDevices.Find(d => d.Index == storedDeviceIndex);
            if (device != null)
            {
                PreferredDevice = device;
            }

            PreferredOutputMode = storedOutputMode;
            PreferredSampleRateIndex = Array.IndexOf(SampleRates, storedSR);
        }
        #endregion
    }

    public class DeviceDefinition
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
