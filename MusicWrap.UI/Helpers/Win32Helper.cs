using System;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace MusicWrap.UI.Helpers
{
    internal sealed class Win32Helper
    {
        private static readonly Version _osVersion = Environment.OSVersion.Version;

        public static bool IsWindows10OrGreater() => _osVersion.Build >= 10240;
        public static bool IsWindows11OrGreater() => _osVersion.Build >= 22000;
        public static bool IsWindows11_22H1OrGreater() => _osVersion.Build >= 22621;


        public const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 25;
        public const int DWMWA_MICA_EFFECT = 26;

        public enum DWM_SYSTEMBACKDROP_TYPE
        {
            DWMSBT_AUTO,
            DWMSBT_NONE,
            DWMSBT_MAINWINDOW,
            DWMSBT_TRANSIENTWINDOW,
            DWMSBT_TABBEDWINDOW
        }

        #region dwmapi.dll

        public static bool DwmSetWindowAttribute(IntPtr hWnd, int attribute, int value)
            => DwmSetWindowAttribute(hWnd, attribute, ref value);

        public static bool DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value)
            => hWnd != IntPtr.Zero && DwmSetWindowAttribute(hWnd, attribute, ref value, sizeof(int)) == 0; // 0 == S_OK

        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        #endregion

        #region user32.dll

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow([In] IntPtr hWnd);

        #endregion


    }
}
