using MusicWrap.Core.Services.Library;
using System.Diagnostics;

namespace MusicWrap.Mobile
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        private readonly ILibraryService _libraryService;
        public MainPage(ILibraryService libraryService)
        {
            InitializeComponent();
            _libraryService = libraryService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                var library = MauiProgram.Services.GetRequiredService<ILibraryService>();
                var entries = await _libraryService.GetEntriesAsync(Data.User.Models.LibraryEntryType.Album, true);
                CounterBtn.Text = $"Álbumes: {entries.Count}";
            }
            catch (Exception ex)
            {
                CounterBtn.Text = $"Error: {ex.Message}";
                Debug.WriteLine(ex);
            }
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }
}
