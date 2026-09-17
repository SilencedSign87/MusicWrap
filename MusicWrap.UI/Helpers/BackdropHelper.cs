using System.Windows;
using System.Windows.Interop;

namespace MusicWrap.UI.Helpers
{
    /// <summary>
    /// Based on the WPFUI Implementation
    /// </summary>
    public class BackdropHelper
    {
        public static bool IsWindows11OrGreater() => Win32Helper.IsWindows11OrGreater();

        public static bool IsBackdropSupported() => Win32Helper.IsWindows11_22H1OrGreater();

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

        public static bool IsSupported(WindowBackdropType type) => type switch
        {
            WindowBackdropType.Auto => IsWindows11OrGreater(),
            WindowBackdropType.Mica => IsWindows11OrGreater(),
            WindowBackdropType.Acrylic => IsWindows11OrGreater(),
            WindowBackdropType.Tabbed => IsWindows11OrGreater(),
            WindowBackdropType.None => true,
            _ => false
        };

        public static bool ApplyBackdrop(Window? window, WindowBackdropType type)
        {
            if (window is null) return false;

            if (window.IsLoaded)
            {
                IntPtr hWnd = new WindowInteropHelper(window).Handle;
                return hWnd != IntPtr.Zero && ApplyBackdrop(hWnd, type);
            }

            window.Loaded += (_, _) => ApplyBackdrop(window, type);
            return true;
        }

        public static bool ApplyBackdrop(IntPtr hWnd, WindowBackdropType backdropType)
        {
            if (hWnd == IntPtr.Zero || !Win32Helper.IsWindow(hWnd))
                return false;

            // 22H2+: real Mica / Acrylic / Tabbed via DWMWA_SYSTEMBACKDROP_TYPE.
            if (Win32Helper.IsWindows11_22H1OrGreater())
            {
                return backdropType switch
                {
                    WindowBackdropType.Auto => ApplyDwmWindowAttribute(hWnd, Win32Helper.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_AUTO),
                    WindowBackdropType.Mica => ApplyDwmWindowAttribute(hWnd, Win32Helper.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW),
                    WindowBackdropType.Acrylic => ApplyDwmWindowAttribute(hWnd, Win32Helper.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW),
                    WindowBackdropType.Tabbed => ApplyDwmWindowAttribute(hWnd, Win32Helper.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW),
                    _ => ApplyDwmWindowAttribute(hWnd, Win32Helper.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE)
                };
            }

            // 21H2 (22000-22620): only legacy Mica via DWMWA_MICA_EFFECT.
            if (Win32Helper.IsWindows11OrGreater())
                return backdropType != WindowBackdropType.None && ApplyLegacyMicaBackdrop(hWnd);

            // Windows 10 and below: no system backdrop.
            return false;
        }

        public static bool RemoveBackdrop(Window? window)
        {
            if (window is null) return false;
            return RemoveBackdrop(new WindowInteropHelper(window).Handle);
        }

        public static bool RemoveBackdrop(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !Win32Helper.IsWindow(hWnd))
                return false;

            _ = Win32Helper.DwmSetWindowAttribute(hWnd, Win32Helper.DWMWA_MICA_EFFECT, 0);
            return Win32Helper.DwmSetWindowAttribute(hWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, (int)Win32Helper.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE);
        }

        private static bool ApplyDwmWindowAttribute(IntPtr hWnd, Win32Helper.DWM_SYSTEMBACKDROP_TYPE type)
            => Win32Helper.DwmSetWindowAttribute(hWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, (int)type);

        private static bool ApplyLegacyMicaBackdrop(IntPtr hWnd)
            => Win32Helper.DwmSetWindowAttribute(hWnd, Win32Helper.DWMWA_MICA_EFFECT, 1);
    }

    public enum WindowBackdropType
    {
        Auto,
        Mica,
        Acrylic,
        Tabbed,
        None
    }
}
