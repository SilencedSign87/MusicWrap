using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using MusicWrap.UI.ViewModels.Providers;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Pages.Providers
{
    /// <summary>
    /// Lógica de interacción para YoutubeProviderPage.xaml
    /// </summary>
    public partial class YoutubeProviderPage : UserControl
    {
        private readonly YoutubeProviderViewModel _viewModel;
        private readonly CommandPaletteViewModel _commandPaletteViewModel;
        private bool _isSubscribed;

        public event EventHandler? BackRequested;

        public YoutubeProviderPage()
        {
            InitializeComponent();

            _viewModel = App.Services.GetRequiredService<YoutubeProviderViewModel>();
            _commandPaletteViewModel = App.Services.GetRequiredService<CommandPaletteViewModel>();

            DataContext = _viewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isSubscribed)
            {
                return;
            }

            _commandPaletteViewModel.QuerySubmitted += OnQuerySubmitted;
            _isSubscribed = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _commandPaletteViewModel.QuerySubmitted -= OnQuerySubmitted;
            _isSubscribed = false;
        }

        private async void OnQuerySubmitted(object? sender, string query)
        {
            if (!IsVisible)
            {
                return;
            }

            await _viewModel.SearchAsync(query);
        }

        private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox)
            {
                return;
            }

            if (listBox.SelectedItem is YoutubeSearchLeafNode selected)
            {
                await _viewModel.SelectItemAsync(selected);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
