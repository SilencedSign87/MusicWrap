using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Helpers
{
    public class WallpaperHelper
    {
        private const int SPI_GETDESKWALLPAPER = 0x0073;
        private const int MAX_PATH = 260;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(
            int uiAction,
            int uiParam,
            string pvParam,
            int fWinIni);

        public static string? GetWallpaperPath()
        {
            var path = new string('\0', MAX_PATH);

            if (!SystemParametersInfo(
                    SPI_GETDESKWALLPAPER,
                    MAX_PATH,
                    path,
                    0))
            {
                return null;
            }

            path = path.TrimEnd('\0');

            return File.Exists(path)
                ? path
                : null;
        }

    }
}
