using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Features.Metadata.Viewmodels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using TagLib.Matroska;

namespace MusicWrap.UI.Features.Metadata.Services
{
    public sealed partial class MetadataEditorWorkspace : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSingleTrack))]
        private IReadOnlyList<int> trackIds = [];
        public bool IsSingleTrack => TrackIds.Count == 1;
    }
}
