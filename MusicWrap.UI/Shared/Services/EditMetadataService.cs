using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.ViewModels;
using System.Windows;

namespace MusicWrap.UI.Services
{
    public interface IEditMetadataService
    {
        void OpenMetadataWindow(List<int> trackIds);
        event EventHandler<List<int>>? ItemsChanged;
    }
    public class EditMetadataService : IEditMetadataService
    {
        public event EventHandler<List<int>>? ItemsChanged;
        private readonly MusicLibrary _library;
        private MetadataEditorWindow? _currentWindow;
        public EditMetadataService(MusicLibrary library)
        {
            _library = library;
        }
        public void OpenMetadataWindow(List<int> trackIds)
        {
            string title = string.Empty;

            if (trackIds.Count == 0)
            {
                return;
            }
            else
            {
                title = trackIds.Count == 1 ? _library.Tracks.Where(t => t.Id == trackIds[0]).Select(t => t.Title).FirstOrDefault() ?? "Unknown Track" : $"{trackIds.Count} Tracks";
            }

            if (_currentWindow != null && _currentWindow.IsLoaded)
            {
                _currentWindow.Title = $"Edit Metadata - {title}";
                _currentWindow.Focus();
                (_currentWindow.DataContext as MetadataEditorViewModel)?.LoadTracks(trackIds);
                return;
            }

            var mainWindow = App.CurrentWindow;

            _currentWindow = App.Services.GetRequiredService<MetadataEditorWindow>();
            _currentWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _currentWindow.Title = $"Edit Metadata - {title}";
            _currentWindow.Owner = mainWindow;

            var viewmodel = _currentWindow.DataContext as MetadataEditorViewModel;
            viewmodel?.LoadTracks(trackIds);

            _currentWindow.DataContext = viewmodel;

            _currentWindow.Show();
            ItemsChanged?.Invoke(this, trackIds);

            _currentWindow.Closed += MetadataWindow_Closed;
        }

        private void MetadataWindow_Closed(object? sender, EventArgs e)
        {
            _currentWindow?.Closed -= MetadataWindow_Closed;
            _currentWindow = null;
        }
    }

    public enum MetadataEntityType
    {
        Artist,
        Album,
        Track
    }
}




