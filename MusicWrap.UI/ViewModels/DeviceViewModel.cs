using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core;
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
        private int currentDeviceIndex = 0;
        [ObservableProperty]
        private int currentSampleRateIndex = 0;

        public bool IsInitialized { get; private set; } = false;

        private readonly IMusicPlayerService _player;
        private readonly int[] SampleRates = [-1, 44100, 48000, 88200, 96000, 176400, 192000];
        public DeviceViewModel(IMusicPlayerService player)
        {
            _player = player;

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


            _player.DeviceIndexChanged += _player_DeviceIndexChanged;
            _player.SampleRateChanged += _player_SampleRateChanged;
            IsInitialized = true;
        }

        public void SetCurrentSampleRate(int index)
        {
            if (index < 0 || index >= SampleRates.Length) return;

            int target = SampleRates[index];
            if (target == _player.CurrentSampleRate) return;

            _player.ChangeSampleRate(target);

        }
        public void SetCurrentDevice(int index)
        {
            if (index < 0 && index >= AvailableDevices.Count) return;

            int target = AvailableDevices[index].Index;
            if (target == _player.CurrentDeviceIndex) return;

            _player.ChangeOutputDevice(target);

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
