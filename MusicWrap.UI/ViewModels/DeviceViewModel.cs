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

        private readonly IMusicPlayerService _player;
        public DeviceViewModel( IMusicPlayerService player)
        {
            _player = player;

            LoadDevices();
            _player.DeviceIndexChanged += _player_DeviceIndexChanged;
            _player.SampleRateChanged += _player_SampleRateChanged;
        }

        private void _player_SampleRateChanged(object? sender, SampleRateChangedEventArgs e)
        {
            CurrentSampleRate = e.EffectiveSampleRate.ToString();
        }

        private void _player_DeviceIndexChanged(object? sender, int e)
        {
            //throw new NotImplementedException();
        }

        private void LoadDevices()
        {
            var devices = _player.GetAvailableDevices();
            List<DeviceDefinition> deviceDefinitions = [];
            foreach (var device in devices) {
                deviceDefinitions.Add(new DeviceDefinition() { Index = device.Index, Name = device.Name });
            }
            AvailableDevices = deviceDefinitions;
        }
    }

    public class  DeviceDefinition
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
