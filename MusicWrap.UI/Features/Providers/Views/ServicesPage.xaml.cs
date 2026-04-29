using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Features.Providers.Views
{
    /// <summary>
    /// Lógica de interacción para ServicesPage.xaml
    /// </summary>
    public partial class ServicesPage : UserControl
    {
        public ServicesPage()
        {
            InitializeComponent();
        }

        private void HandleGoToYoutubeProvider(object sender, RoutedEventArgs e)
        {
            ServiceHomeView.Visibility = Visibility.Collapsed;

            ContentControlRenderer.Visibility = Visibility.Visible;
            var page = new YoutubeProviderPage();
            page.BackRequested += YoutubeProviderPage_BackRequested;
            ContentControlRenderer.Content = page;
        }

        private void YoutubeProviderPage_BackRequested(object? sender, EventArgs e)
        {
            if (sender is YoutubeProviderPage page)
            {
                page.BackRequested -= YoutubeProviderPage_BackRequested;
            }

            ContentControlRenderer.Content = null;
            ContentControlRenderer.Visibility = Visibility.Collapsed;
            ServiceHomeView.Visibility = Visibility.Visible;
        }
    }
    sealed class OEmbedResponse
    {
        public string? title { get; set; }
        public string? author_name { get; set; }
        public string? thumbnail_url { get; set; }
        public string? Title => title;
        public string? AuthorName => author_name;
        public string? ThumbnailUrl => thumbnail_url;
    }
}



