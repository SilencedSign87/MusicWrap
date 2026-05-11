namespace MusicWrap.UI.Helpers
{
    /// <summary>
    /// Copied from WPF gallery sample
    /// </summary>
    public class BackdropHelper
    {
        public static bool IsWindows11OrGreater()
        {
            var os = Environment.OSVersion;
            var version = os.Version;

            return (version.Major >= 10 && version.Build >= 22000);
        }

        public static bool IsBackdropSupported()
        {
            var os = Environment.OSVersion;
            var version = os.Version;

            return version.Major >= 10 && version.Build >= 22621;
        }

        public static bool IsBackdropDisabled()
        {
            var appContextBackdropData = AppContext.GetData("Switch.System.Windows.Appearance.DisableFluentThemeWindowBackdrop");
            bool disableFluentThemeWindowBackdrop = false;

            if (appContextBackdropData != null)
            {
                disableFluentThemeWindowBackdrop = bool.TryParse(Convert.ToString(appContextBackdropData), out bool parsed) && parsed;
            }

            return disableFluentThemeWindowBackdrop;
        }
    }
}
