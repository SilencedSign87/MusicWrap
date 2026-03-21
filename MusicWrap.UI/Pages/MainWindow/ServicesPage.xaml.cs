using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Application;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
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

namespace MusicWrap.UI.Pages.MainWindow
{
    /// <summary>
    /// Lógica de interacción para ServicesPage.xaml
    /// </summary>
    public partial class ServicesPage : UserControl
    {
        private string? _currentVideoId = null;
        private string? _currentListId = null;
        private string? _currentWatchUrl = null;

        public ServicesPage()
        {
            InitializeComponent();
        }

        private void YoutubeMusicWebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            EvaluateURL();
        }

        private void YoutubeMusicWebView_SourceChanged(object sender, Microsoft.Web.WebView2.Core.CoreWebView2SourceChangedEventArgs e)
        {
            EvaluateURL();
        }

        private void EvaluateURL()
        {
            _currentWatchUrl = null;
            _currentVideoId = null;
            _currentListId = null;

            var uri = YoutubeMusicWebView.Source;
            if (uri is null)
            {
                AddToLibraryButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (!TryExtractYoutubeWatch(uri, out var videoId, out var listId))
            {
                AddToLibraryButton.Visibility = Visibility.Collapsed;
                return;
            }

            _currentVideoId = videoId;
            _currentListId = listId;
            _currentWatchUrl = BuildCanonicalWatchUrl(videoId);
            AddToLibraryButton.Visibility = Visibility.Visible;

        }
        private void Reload_click(object sender, RoutedEventArgs e)
        {
            YoutubeMusicWebView.Reload();
        }

        private async void AddToLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentVideoId) || string.IsNullOrWhiteSpace(_currentWatchUrl))
                return;

            AddToLibraryButton.IsEnabled = false;
            try
            {
                var library = App.Services.GetRequiredService<MusicLibrary>();
                var repository = App.Services.GetRequiredService<ILibraryRepository>();
                var cache = App.Services.GetRequiredService<ILibraryCacheService>();
                var indexer = App.Services.GetRequiredService<ILibraryIndexer>();

                var meta = await TryFetchOEmbedAsync(_currentVideoId);

                string title = !string.IsNullOrWhiteSpace(meta?.Title) ? meta!.Title! : $"YoutubeTrack {_currentVideoId}";
                string artistName = !string.IsNullOrWhiteSpace(meta?.AuthorName) ? meta!.AuthorName! : "Unknown Artist";

                var thumb = await TryDownloadThumbnailAsync(meta?.ThumbnailUrl);

                var result = indexer.IndexExternalTrack(new ExternalTrackIndexRequest
                {
                    Origin = TrackOrigin.Youtube,
                    SourceUri = _currentWatchUrl!,
                    ExternalId = _currentVideoId!,
                    Title = title,
                    ArtistName = artistName,
                    AlbumName = title,
                    ThumbnailBytes = thumb?.Bytes,
                    ThumbnailMimeType = thumb?.MimeType
                });

                if (!result.Created)
                {
                    MessageBox.Show("This track is already in your library.", "Duplicate Track", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                repository.Save(library);
                cache.InvalidateCache();

                MessageBox.Show("Track indexed successfully.", "MusicWrap", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Indexing failed: {ex.Message}", "MusicWrap", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AddToLibraryButton.IsEnabled = true;
            }
        }

        private static bool TryExtractYoutubeWatch(Uri uri, out string videoId, out string? listId)
        {
            videoId = string.Empty;
            listId = null;

            var host = uri.Host.ToLowerInvariant();
            bool validHost = host switch
            {
                "music.youtube.com" => true,
                "www.youtube.com" => true,
                "youtube.com" => true,
                _ => false
            };
            if (!validHost) return false;

            if (!string.Equals(uri.AbsolutePath, "/watch", StringComparison.OrdinalIgnoreCase)) return false;

            var q = HttpUtility.ParseQueryString(uri.Query);
            var v = q["v"];
            if (string.IsNullOrWhiteSpace(v)) return false;

            videoId = v.Trim();
            listId = string.IsNullOrWhiteSpace(q["list"]) ? null : q["list"]!.Trim();
            return true;
        }
        private static string BuildCanonicalWatchUrl(string videoId)
        {
            return $"https://music.youtube.com/watch?v={videoId}";
        }
        private static async Task<OEmbedResponse?> TryFetchOEmbedAsync(string videoId)
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(8);

                string url = $"https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={videoId}&format=json";
                var json = await http.GetStringAsync(url);

                return JsonSerializer.Deserialize<OEmbedResponse>(json);
            }
            catch
            {
                return null;
            }

        }
        private static async Task<(byte[] Bytes, string MimeType)?> TryDownloadThumbnailAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                using var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var mime = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes == null || bytes.Length <=0) return null;

                return (bytes, mime);
            }
            catch {
                return null;
            }
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
