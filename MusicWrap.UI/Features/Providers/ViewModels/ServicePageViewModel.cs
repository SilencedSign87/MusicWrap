using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Providers.Views;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Providers.ViewModels
{
    public partial class ServicePageViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private ServicePageType currentPageType = ServicePageType.Home;

        [ObservableProperty]
        private UserControl? currentControl = null;

        public ServicePageViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        #region RelayCommands
        [RelayCommand(CanExecute = nameof(CanNavigateTo))]
        private void NavigateTo(string pageType)
        {

            if (Enum.TryParse<ServicePageType>(pageType, ignoreCase: true, out var result))
            {
                CurrentPageType = result;

                var control = CurrentPageType switch
                {
                    ServicePageType.Home => null,
                    ServicePageType.YoutubeProvider => _serviceProvider.GetRequiredService<YoutubeProviderPage>(),
                    _ => null
                };

                SetCurrentControl(control);
            }
        }

        private void Page_HomeRequested(object? sender, EventArgs e)
        {
            NavigateTo("Home");
        }

        private bool CanNavigateTo(string pageType)
        {
            if (!Enum.TryParse<ServicePageType>(pageType, ignoreCase: true, out var result))
            {
                return false;
            }
            return CurrentPageType != result;
        }
        private void SetCurrentControl(UserControl? control)
        {
            if (CurrentControl != null)
            {
                if (CurrentControl is YoutubeProviderPage youtubePage)
                {
                    youtubePage.HomeRequested -= Page_HomeRequested;
                }
            }
            CurrentControl = control;
            if (CurrentControl != null)
            {
                if (CurrentControl is YoutubeProviderPage youtubePage)
                {
                    youtubePage.HomeRequested += Page_HomeRequested;
                }
            }
        }
        #endregion

    }

    public enum ServicePageType
    {
        Home,
        YoutubeProvider,
        InternetRadioProvider,
        RemoteFileProvider,
    }
}
