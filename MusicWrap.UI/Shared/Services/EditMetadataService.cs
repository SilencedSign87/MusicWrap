using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.UI.Services
{
    public interface IEditMetadataService
    {
        void OpenMetadataWindow(List<int> trackIds);
        event EventHandler<List<int>>? ItemsChanged;
    }
    public class EditMetadataService : IEditMetadataService
    {
        private Window? MetadataWindow;
        private readonly MusicLibrary _library;
        public event EventHandler<List<int>>? ItemsChanged;
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

            if (MetadataWindow != null)
            {
                MetadataWindow.Title = $"Edit Metadata - {title}";
                MetadataWindow.Activate();
                ItemsChanged?.Invoke(this, trackIds);
                return;
            }

            var mainWindow = App.CurrentWindow;

            MetadataWindow = new MetadataEditorWindow
            {
                Title = $"Edit Metadata - {title}",
                Owner = mainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            MetadataWindow.Show();
            ItemsChanged?.Invoke(this, trackIds);

            MetadataWindow.Closed += MetadataWindow_Closed;
        }

        private void MetadataWindow_Closed(object? sender, EventArgs e)
        {
            MetadataWindow?.Closed -= MetadataWindow_Closed;
            MetadataWindow = null;
        }
    }

    public enum MetadataEntityType
    {
        Artist,
        Album,
        Track
    }
}




