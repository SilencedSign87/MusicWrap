using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.UI.ViewModels;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataTagEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        public bool HasChanges => false;

        public void Load()
        {
            // TODO fase 2: requiere definir el modelo de tags del Track
        }

        [RelayCommand]
        private void CancelChanges() { }

        [RelayCommand]
        private Task SaveAsync() => Task.CompletedTask;
    }
}
