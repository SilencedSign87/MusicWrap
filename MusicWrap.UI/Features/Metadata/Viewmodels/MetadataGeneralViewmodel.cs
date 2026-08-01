using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.UI.ViewModels;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataGeneralViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        public bool HasChanges => false;

        public void Load()
        {
            // TODO fase 2: info read-only del track (path, duración, codec, tamaño)
        }

        [RelayCommand]
        private void CancelChanges() { }

        [RelayCommand]
        private Task SaveAsync() => Task.CompletedTask;
    }
}
