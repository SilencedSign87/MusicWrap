using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Search;

namespace MusicWrap.UI.ViewModels
{
    public partial class CommandPaletteViewModel : ObservableObject
    {
        [ObservableProperty] private string query = string.Empty;

        private readonly SearchService _searchService;


        public CommandPaletteViewModel(SearchService searchService)
        {
            _searchService = searchService;
        }

        partial void OnQueryChanged(string value)
        {
            _searchService.SetQuery(value);
        }

        [RelayCommand]
        private void SubmitQuery()
        {
            _searchService.Submit();
        }

        [RelayCommand]
        private void ClearQuery()
        {
            Query = string.Empty;
            _searchService.Clear();
        }
    }
}


