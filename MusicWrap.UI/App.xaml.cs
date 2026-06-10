using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Player;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Bootstrap;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.Shell.Windows;
using Serilog;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace MusicWrap.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private const string SingleInstanceMutexName = "MusicWrap.SingleInstance";
        public static IServiceProvider Services { get; private set; } = default!;

        protected override void OnStartup(StartupEventArgs e)
        {
            SetDropDownMenuToBeRightAligned();
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);

            try
            {
                SplashScreen splash = new("Resources/SplashScreen.png");
                splash.Show(autoClose: false, topMost: true);

                Services = StartupOrquestrator.BuildServiceProvider();

                _ = StartupOrquestrator.InitializeAsync(Services, splash);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error during application startup");
            }

        }
        protected override void OnExit(ExitEventArgs e)
        {
            var WindowManager = Services.GetRequiredService<WindowManager>();
            WindowManager.IsShuttingDown = true;

            try
            {
                var coordinator = Services.GetRequiredService<ISaveCoordinator>();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                coordinator.FlushAsync(cts.Token).GetAwaiter().GetResult();

                if (Services.GetServices<ITrayService>()  is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while saving data on exit");
            }
            finally
            {
                Log.Information("Application exiting");
                Log.CloseAndFlush();
                base.OnExit(e);
            }
        }

        #region Internal

        private static void SetDropDownMenuToBeRightAligned()
        {
            var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
            Action setAlignmentValue = () =>
            {
                if (SystemParameters.MenuDropAlignment && menuDropAlignmentField != null) menuDropAlignmentField.SetValue(null, false);
            };

            setAlignmentValue();

            SystemParameters.StaticPropertyChanged += (sender, e) =>
            {
                setAlignmentValue();
            };
        }
        #endregion
    }

}




