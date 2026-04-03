using MusicWrap.UI.Windows;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace MusicWrap.UI.Services
{
    public interface IEditMetadataService
    {
        void OpenMetadataWindow(int entityId, MetadataEntityType type);
    }
    public class EditMetadataService : IEditMetadataService
    {
        private Window? MetadataWindow;
        public void OpenMetadataWindow(int entityId, MetadataEntityType type)
        {
            if (MetadataWindow != null)
            {
                MetadataWindow.Activate();
                return;
            }

            var mainWindow = App.CurrentWindow;

            MetadataWindow = new MetadataEditorWindow();
            MetadataWindow.Owner = mainWindow;
            MetadataWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            MetadataWindow.Show();

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
