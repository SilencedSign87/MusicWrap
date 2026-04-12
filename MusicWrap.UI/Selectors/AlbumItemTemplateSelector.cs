using MusicWrap.UI.Features.Library.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Selectors
{
    public class AlbumItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? AlbumTemplate { get; set; }
        public DataTemplate? TrackListTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is LibraryViewModel.AlbumData)
                return AlbumTemplate;
            
            if (item is LibraryViewModel.TrackListPlaceholder)
                return TrackListTemplate;

            return base.SelectTemplate(item, container);
        }
    }
}

