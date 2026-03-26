using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MusicWrap.UI.ViewModels.Playlist
{
    public partial class PlaylistViewModel : ObservableObject
    {
        [ObservableProperty] ObservableCollection<PlaylistEntry> entries = [];

        public PlaylistViewModel()
        {

        }
    }
    public record PlaylistEntry(
        string Title,
        string ImagePath,
        string Description
        );
}
