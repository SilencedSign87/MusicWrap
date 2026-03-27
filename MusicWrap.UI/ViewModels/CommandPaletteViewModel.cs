using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;

namespace MusicWrap.UI.ViewModels
{
    public partial class CommandPaletteViewModel : ObservableObject
    {
        [ObservableProperty] private string query = string.Empty;
        public event EventHandler<string>? QuerySubmitted;

        public CommandPaletteViewModel(ILibraryCacheService libraryCache, MusicLibrary library)
        {
            _ = libraryCache;
            _ = library;
        }

        partial void OnQueryChanged(string value)
        {
            _ = value;
        }

        [RelayCommand]
        private void SubmitQuery()
        {
            QuerySubmitted?.Invoke(this, Query?.Trim() ?? string.Empty);
        }

        [RelayCommand]
        private void ClearQuery()
        {
            Query = string.Empty;
            QuerySubmitted?.Invoke(this, string.Empty);
        }
    }
}
