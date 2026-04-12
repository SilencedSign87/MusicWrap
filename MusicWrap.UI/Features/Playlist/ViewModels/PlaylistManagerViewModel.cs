using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace MusicWrap.UI.Features.Playlist.ViewModels
{
    public partial class PlaylistManagerViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<int> trackIds = new();
        [ObservableProperty] private int selectedTrackId = 0;
        //[ObservableProperty] private ObservableCollection
    }
}

