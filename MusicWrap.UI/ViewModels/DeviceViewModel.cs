using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.ViewModels
{
    public partial class DeviceViewModel : ObservableObject
    {
        [ObservableProperty]
        private List<DeviceDefinition> availableDevices = [];
        [ObservableProperty]
        private string currentDeviceName = "Default Device";
        [ObservableProperty]
        private string currentSampleRate = "44100";
        [ObservableProperty]
        private int currentOutputModeIndex = 0;
        [ObservableProperty]
        private string currentOutputModeName = "WASAPI Shared";
        [ObservableProperty]
        private int currentDeviceIndex = 0;
        [ObservableProperty]
        private int currentSampleRateIndex = 0;

        public bool IsInitialized { get; private set; } = false;

        private readonly IMusicPlayerService _player;
        private readonly IUserSettingsRepository _userSettingsRepository;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly UserSettings _userSettings;
        private readonly int[] SampleRates = [-1, 44100, 48000, 88200, 96000, 176400, 192000];
        private readonly OutputMode[] Outputmodes = [OutputMode.WasapiShared, OutputMode.WasapiExclusive];
        public DeviceViewModel(IMusicPlayerService player, IUserSettingsRepository userSettingsRepository, ISaveCoordinator saveCoordinator, UserSettings userSettings)
        {
            _player = player;
            _userSettingsRepository = userSettingsRepository;
            _userSettings = userSettings;
            _saveCoordinator = saveCoordinator;

            LoadDevices();

            // Initialize states
            var devIdx = _player.CurrentDeviceIndex;
            if (devIdx >= 0)
            {
                var idx = AvailableDevices.FindIndex(d => d.Index == devIdx);
                CurrentDeviceIndex = idx >= 0 ? idx : 0;
            }
            CurrentDeviceName = AvailableDevices.Count > 0 ? AvailableDevices[CurrentDeviceIndex].Name : "Default Device";

            var sr = _player.CurrentSampleRate;
            var srIdx = Array.IndexOf(SampleRates, sr);
            CurrentSampleRateIndex = srIdx >= 0 ? srIdx : 0;
            CurrentSampleRate = sr > 0 ? sr.ToString() : "Auto";

            var outputMode = _player.CurrentOutputMode;
            var outIdx = Array.IndexOf(Outputmodes, outputMode);
            CurrentOutputModeIndex = outIdx >= 0 ? outIdx : 0;
            CurrentOutputModeName = outputMode == OutputMode.WasapiShared ? "WASAPI Shared" : "WASAPI Exclusive";


            _player.DeviceIndexChanged += _player_DeviceIndexChanged;
            _player.SampleRateChanged += _player_SampleRateChanged;
            _player.OutputModeChanged += _player_OutputModeChanged;
            IsInitialized = true;
        }
        public void SetCurrentOutputMode(int index)
        {
            if (index < 0 || index >= Outputmodes.Length) return;
            var target = Outputmodes[index];
            if (target == _player.CurrentOutputMode) return;

            _player.ChangeOutputMode(target);
            _userSettings.PreferredOutputMode = target;
            _saveCoordinator.Enqueue(SaveKind.Settings);
            //_userSettingsRepository.Save(_userSettings);
        }

        public void SetCurrentSampleRate(int index)
        {
            if (index < 0 || index >= SampleRates.Length) return;

            int target = SampleRates[index];
            if (target == _player.CurrentSampleRate) return;

            _player.ChangeSampleRate(target);
            _userSettings.PreferredSampleRate = (SampleRatePreference)target;
            _saveCoordinator.Enqueue(SaveKind.Settings);
            //_userSettingsRepository.Save(_userSettings);

        }
        public void SetCurrentDevice(int index)
        {
            if (index < 0 && index >= AvailableDevices.Count) return;

            int target = AvailableDevices[index].Index;
            if (target == _player.CurrentDeviceIndex) return;

            _player.ChangeOutputDevice(target);
            _userSettings.PreferredDeviceIndex = target;
            _saveCoordinator.Enqueue(SaveKind.Settings);
            //_userSettingsRepository.Save(_userSettings);

        }

        private void _player_SampleRateChanged(object? sender, SampleRateChangedEventArgs e)
        {
            var prefered = e.PreferedSampleRate;
            var effective = e.EffectiveSampleRate;
            CurrentSampleRate = effective > 0 ? effective.ToString() : "Auto";

            var idx = Array.IndexOf(SampleRates, prefered);
            if (idx < 0)
            {
                idx = Array.IndexOf(SampleRates, effective);
            }
            CurrentSampleRateIndex = idx >= 0 ? idx : 0;
        }

        private void _player_DeviceIndexChanged(object? sender, int e)
        {
            var idx = AvailableDevices.FindIndex(d => d.Index == e);
            if (idx >= 0)
            {
                CurrentDeviceIndex = idx;
                CurrentDeviceName = AvailableDevices[CurrentDeviceIndex].Name;
            }
        }
        private void _player_OutputModeChanged(object? sender, OutputMode e)
        {
            var idx = Array.IndexOf(Outputmodes, e);
            CurrentOutputModeIndex = idx >= 0 ? idx : 0;
            CurrentOutputModeName = e == OutputMode.WasapiShared ? "WASAPI Shared" : "WASAPI Exclusive";
        }

        private void LoadDevices()
        {
            var devices = _player.GetAvailableDevices();
            List<DeviceDefinition> deviceDefinitions = [];
            foreach (var device in devices)
            {
                deviceDefinitions.Add(new DeviceDefinition() { Index = device.Index, Name = device.Name });
            }
            AvailableDevices = deviceDefinitions;
        }
    }

    public class DeviceDefinition
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
