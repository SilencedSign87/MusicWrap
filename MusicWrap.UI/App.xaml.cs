using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data;
using MusicWrap.Data.Services;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Pages.MainWindow;
using MusicWrap.UI.Services;
using MusicWrap.UI.ViewModels;
using MusicWrap.UI.ViewModels.Library;
using MusicWrap.UI.ViewModels.Settings;
using MusicWrap.UI.Windows;
using System.Configuration;
using System.Data;
using System.Windows;

namespace MusicWrap.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static Window? CurrentWindow { get; private set; }
        public static IServiceProvider Services { get; private set; } = default!;
        private TaskbarIcon? _trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SplashScreen splash = new("Resources/SplashScreen.png");
            splash.Show(autoClose: false, topMost: true);

            Services = ConfigureServices();

            InitializeServicesAsync(splash);

        }

        private void _trayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (CurrentWindow is not null)
            {
                if (CurrentWindow.WindowState == WindowState.Minimized)
                    CurrentWindow.WindowState = WindowState.Normal;
                CurrentWindow.Activate();
                CurrentWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TrayIconManager.DisposeTrayIcon();
            TrySaveLibrary();
            base.OnExit(e);
        }

        public static void ShowMain()
        {
            var main = Services.GetRequiredService<MainWindow>();
            main.Show();

            CurrentWindow?.Close();

            CheckMemory();

            CurrentWindow = main;
        }

        public static void ShowCompactPlayer()
        {
            var player = Services.GetRequiredService<CompactPlayer>();
            player.Show();

            CurrentWindow?.Close();

            CheckMemory();

            CurrentWindow = player;
        }

        #region Internal

        private static void TrySaveLibrary()
        {
            try
            {
                if (Services is null) return;
                var store = Services.GetService<ILibraryStore>();
                var library = Services.GetService<MusicLibrary>();

                if (store != null && library != null)
                {
                    store.Save(library);
                }
                var settings = Services.GetService<IKeyValueStore>();
                settings?.SaveToDisk();
            }
            catch
            {

            }
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();


            //Data layer
            services.AddSingleton<ILibraryStore, LibraryStore>();
            services.AddSingleton(sp => sp.GetRequiredService<ILibraryStore>().Load());
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddSingleton<ILibraryIndexer, LibraryIndexer>();
            services.AddSingleton<IKeyValueStore, KeyValueStore>();

            //Player
            services.AddSingleton<IMusicPlayerService, MusicPlayerService>();

            // UI
            services.AddTransient<MainWindow>();
            services.AddTransient<CompactPlayer>();
            services.AddTransient<SettingsWindow>();

            // Services
            services.AddSingleton<ILibraryCacheService, LibraryCacheService>();


            // View Models
            services.AddTransient<DirectoriesManagerViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<AlbumTracksViewModel>();
            services.AddTransient<QueueViewModel>();
            //services.AddTransient<AlbumViewModel>();

            // Player
            services.AddSingleton<PlayerViewModel>();

            return services.BuildServiceProvider();
        }

        private async void InitializeServicesAsync(SplashScreen splash)
        {
            try
            {
                // Force load

                Services.GetRequiredService<MusicLibrary>();
                Services.GetRequiredService<IKeyValueStore>();

                var settings = Services.GetRequiredService<IKeyValueStore>();
                var listBy = settings.GetValue<string>("library_list_by") ?? "Artist";
                var ascending = settings.GetValue<bool>("library_list_ascending");

                var LibraryCache = Services.GetRequiredService<ILibraryCacheService>();
                await LibraryCache.InitializeAsync(listBy, ascending);

                _trayIcon = TrayIconManager.GetTrayIcon();
                _trayIcon.TrayMouseDoubleClick += _trayIcon_TrayMouseDoubleClick;
            }
            catch
            {

            }
            finally
            {

                splash.Close(TimeSpan.FromSeconds(0.5));
                ShowMain();
            }
        }

        private static void CheckMemory()
        {
#if DEBUG
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
#endif
        }
        #endregion
    }

}
