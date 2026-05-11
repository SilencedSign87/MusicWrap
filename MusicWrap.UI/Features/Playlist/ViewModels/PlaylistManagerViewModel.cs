using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MusicWrap.UI.Features.Playlist.ViewModels
{
    public partial class PlaylistManagerViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<int> trackIds = new();
        [ObservableProperty] private int selectedTrackId = 0;
        //[ObservableProperty] private ObservableCollection
    }
}

