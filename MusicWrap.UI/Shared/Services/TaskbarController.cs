using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Shared.Services
{
    public sealed class TaskbarController : IDisposable
    {
        private readonly ITaskbarList3 _taskbar;
        private Window? _window;
        private HwndSource? _hwndSource;
        private IntPtr _hwnd;

        private BitmapSource
            ? _cover;

        private bool _buttonsAdded;
        private bool _isPlaying;
        public event Action? PreviousRequested;
        public event Action? PlayPauseRequested;
        public event Action? NextRequested;

        private const uint ButtonPrevious = 1;
        private const uint ButtonPlayPause = 2;
        private const uint ButtonNext = 3;

        private readonly IMusicPlayerService _playerService;
        private readonly ILibraryService _libraryService;
        private readonly IwindowsImageService _imageService;
        private readonly ILogger _logger;

        private const int IconSize = 32;
        private List<IntPtr> _createdIcons = new();

        public TaskbarController(IMusicPlayerService playerService, ILibraryService libraryService, IwindowsImageService imageService, ILogger<TaskbarController> logger)
        {
            _playerService = playerService;
            _libraryService = libraryService;
            _imageService = imageService;
            _logger = logger;

            var type = System.Type.GetTypeFromCLSID(
                new Guid("56FDF344-FD6D-11D0-958A-006097C9A090"));

            _taskbar = (ITaskbarList3)Activator.CreateInstance(type!)!;
            _taskbar.HrInit();

            _playerService.PlaybackStateChanged += OnPlaybackChanged;
            _playerService.TrackChanged += OnTrackChanged;
        }

        private void OnTrackChanged(object? sender, string e)
        {
            RefreshArtwork();
        }
        private void RefreshArtwork()
        {
            var trackId = _playerService.CurrentTrackId;

            var track = trackId == 0
                ? null
                : _libraryService.GetTrackById(trackId);

            if (track is null)
            {
                SetArtwork(null);
                return;
            }
            // search for a valid cover art
            int coverId = track.CoverId;
            if (coverId == 0 && track.AlbumId > 0)
            {
                var album = _libraryService.GetAlbumById(track.AlbumId);
                if (album is not null)
                    coverId = album.CoverId;
            }
            if (coverId == 0)
            {
                SetArtwork(null);
                return;
            }

            var cover = _libraryService.GetCoverAsset(coverId);
            if (cover is null)
            {
                SetArtwork(null);
                return;
            }

            SetArtwork(_imageService.LoadForSize(
                    cover.FileName,
                    500,
                    preferOriginal: true
                    ));

        }

        private void OnPlaybackChanged(object? sender, ManagedBass.PlaybackState e)
        {
            SetPlaying(e == ManagedBass.PlaybackState.Playing);
        }

        public void Attach(Window window)
        {
            if (ReferenceEquals(_window, window)) return;

            Detach();

            _window = window;

            _hwnd = new WindowInteropHelper(window).EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);
            EnableCustomThumbnail();
            if (_cover is null)
            {
                RefreshArtwork();
            }
            if (window.IsLoaded)
            {
                OnWindowReady();
            }
            else
            {
                window.Loaded += OnWindowLoaded;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Window window || !ReferenceEquals(_window, window))
                return;

            window.Loaded -= OnWindowLoaded;
            OnWindowReady();
        }

        private void OnWindowReady()
        {
            EnableCustomThumbnail();
            AddButtons();
            InvalidateThumbnail();
        }

        public void Detach()
        {
            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            if (_window is not null)
            {
                _window.Loaded -= OnWindowLoaded;
            }

            _window = null;
            _hwnd = IntPtr.Zero;
            _buttonsAdded = false;
            DestroyIcons(_createdIcons);
        }
        public void SetArtwork(BitmapSource? artwork)
        {
            if (artwork is not null)
            {
                artwork.Freeze();
            }

            _cover = artwork;

            InvalidateThumbnail();
        }
        public void SetPlaying(bool playing)
        {
            _isPlaying = playing;

            if (_hwnd == IntPtr.Zero || !_buttonsAdded)
                return;

            UpdateButtons();
        }
        private void EnableCustomThumbnail()
        {
            int enabled = 1;

            int hr = TaskbarNative.DwmSetWindowAttribute(
                _hwnd,
                TaskbarNative.DWMWA_FORCE_ICONIC_REPRESENTATION,
                ref enabled,
                sizeof(int));

            if (hr != 0)
                _logger.LogWarning("DwmSetWindowAttribute(FORCE_ICONIC) → 0x{hr:X8}", hr);

            hr = TaskbarNative.DwmSetWindowAttribute(
                _hwnd,
                TaskbarNative.DWMWA_HAS_ICONIC_BITMAP,
                ref enabled,
                sizeof(int));
            if (hr != 0)
                _logger.LogWarning("DwmSetWindowAttribute(HAS_ICONIC_BITMAP) → 0x{hr:X8}", hr);
        }
        private void InvalidateThumbnail()
        {
            if (_hwnd == IntPtr.Zero)
                return;

            TaskbarNative.DwmInvalidateIconicBitmaps(_hwnd);
        }
        private void AddButtons()
        {
            if (_hwnd == IntPtr.Zero)
                return;

            var buttons = CreateButtons();

            int hr = _taskbar.ThumbBarAddButtons(
                _hwnd,
                (uint)buttons.Length,
                buttons);

            if (hr != 0)
                _logger.LogWarning("ThumbBarAddButtons → 0x{hr:X8}", hr);

            _buttonsAdded = true;
        }
        private void UpdateButtons()
        {
            if (_hwnd == IntPtr.Zero)
                return;

            var previousIcons = _createdIcons;
            _createdIcons = new List<IntPtr>();

            var buttons = CreateButtons();

            _taskbar.ThumbBarUpdateButtons(
                _hwnd,
                (uint)buttons.Length,
                buttons);
            DestroyIcons(previousIcons);
        }

        private void DestroyIcons(List<IntPtr> icons)
        {
            foreach (var icon in icons)
            {
                if (icon != IntPtr.Zero)
                {
                    TaskbarNative.DestroyIcon(icon);
                }
            }

            icons.Clear();
        }

        private TaskbarNative.THUMBBUTTON[] CreateButtons()
        {
            TaskbarNative.THUMBBUTTON[] buttons = [
                new TaskbarNative.THUMBBUTTON
                {
                    dwMask =
                        TaskbarNative.THB_ICON |
                        TaskbarNative.THB_TOOLTIP |
                        TaskbarNative.THB_FLAGS,
                    iId = ButtonPrevious,
                    hIcon = CreateIcon("previous"),
                    szTip = "Previous",
                    dwFlags = TaskbarNative.THBF_ENABLED
                },
                new TaskbarNative.THUMBBUTTON
                {
                    dwMask =
                        TaskbarNative.THB_ICON |
                        TaskbarNative.THB_TOOLTIP |
                        TaskbarNative.THB_FLAGS,
                    iId = ButtonPlayPause,
                    hIcon = CreateIcon(_isPlaying ? "pause" : "play"),
                    szTip = _isPlaying ? "Pause" : "Play",
                    dwFlags = TaskbarNative.THBF_ENABLED
                                    },
                new TaskbarNative.THUMBBUTTON
                {
                    dwMask =
                        TaskbarNative.THB_ICON |
                        TaskbarNative.THB_TOOLTIP |
                        TaskbarNative.THB_FLAGS,
                    iId = ButtonNext,
                    hIcon = CreateIcon("next"),
                    szTip = "Next",
                    dwFlags = TaskbarNative.THBF_ENABLED
                    }
                ];
            _createdIcons.AddRange(buttons.Select(b => b.hIcon));

            return buttons;
        }
        private static IntPtr CreateIcon(string name)
        {
            var fileName = $"{char.ToUpperInvariant(name[0])}{name[1..]}Icon.png";
            var uri = new System.Uri(
            $"pack://application:,,,/Resources/Icons/{fileName}",
            UriKind.Absolute);

            using var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream is null)
                return IntPtr.Zero;

            using var source = new Bitmap(stream);

            using var resized = new Bitmap(source, new System.Drawing.Size(IconSize, IconSize));

            return resized.GetHicon();
        }
        private IntPtr WndProc(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            if (msg == TaskbarNative.WM_DWMSENDICONICTHUMBNAIL)
            {
                int width = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
                int height = (int)(lParam.ToInt64() & 0xFFFF);

                RenderThumbnail(width, height);

                handled = true;
                return IntPtr.Zero;
            }

            if (msg == TaskbarNative.WM_DWMSENDICONICLIVEPREVIEWBITMAP)
            {
                if (TaskbarNative.GetClientRect(_hwnd, out TaskbarNative.RECT rect))
                {
                    RenderLivePreview(
                        rect.right - rect.left,
                        rect.bottom - rect.top);
                }

                handled = true;
                return IntPtr.Zero;
            }

            if (msg == TaskbarNative.WM_COMMAND)
            {
                var command = (uint)(wParam.ToInt64() & 0xFFFF);
                var notification =
                    (uint)((wParam.ToInt64() >> 16) & 0xFFFF);

                if (notification == TaskbarNative.THBN_CLICKED)
                {
                    HandleButton(command);

                    handled = true;
                }
            }

            return IntPtr.Zero;
        }
        private void HandleButton(uint id)
        {
            switch (id)
            {
                case ButtonPrevious:
                    PreviousRequested?.Invoke();
                    break;

                case ButtonPlayPause:
                    PlayPauseRequested?.Invoke();
                    break;

                case ButtonNext:
                    NextRequested?.Invoke();
                    break;
            }
        }

        private static int SquareSide(int width, int height)
        {
            int side = Math.Min(width, height);
            side = Math.Min(side, 1600);
            return side;
        }

        private void RenderThumbnail(int width, int height)
        {
            if (_hwnd == IntPtr.Zero)
                return;

            int side = SquareSide(width, height);
            if (side <= 0)
                return;

            IntPtr hbmp = RenderArtworkDib(side);
            if (hbmp == IntPtr.Zero)
                return;

            try
            {
                int hr = TaskbarNative.DwmSetIconicThumbnail(
                    _hwnd, hbmp, TaskbarNative.DWM_SIT_NONE);
                if (hr != 0)
                    _logger.LogWarning("DwmSetIconicThumbnail → 0x{hr:X8}", hr);
            }
            finally
            {
                TaskbarNative.DeleteObject(hbmp);
            }
        }

        private void RenderLivePreview(int width, int height)
        {
            if (_hwnd == IntPtr.Zero || width <= 0 || height <= 0)
                return;

            IntPtr hbmp = CaptureWindowToHBitmap(width, height);
            if (hbmp == IntPtr.Zero)
                return;

            try
            {
                int hr = TaskbarNative.DwmSetIconicLivePreviewBitmap(
                    _hwnd, hbmp, IntPtr.Zero, TaskbarNative.DWM_SIT_NONE);
                if (hr != 0)
                    _logger.LogWarning("DwmSetIconicLivePreviewBitmap → 0x{hr:X8}", hr);
            }
            finally
            {
                TaskbarNative.DeleteObject(hbmp);
            }
        }

        private IntPtr CaptureWindowToHBitmap(int width, int height)
        {
            IntPtr hdc = TaskbarNative.GetDC(_hwnd);
            if (hdc == IntPtr.Zero)
                return IntPtr.Zero;

            try
            {
                IntPtr hdcMem = TaskbarNative.CreateCompatibleDC(hdc);
                if (hdcMem == IntPtr.Zero)
                    return IntPtr.Zero;

                try
                {
                    IntPtr hbmp = TaskbarNative.CreateCompatibleBitmap(hdc, width, height);
                    if (hbmp == IntPtr.Zero)
                        return IntPtr.Zero;

                    try
                    {
                        IntPtr hbmpPrev = TaskbarNative.SelectObject(hdcMem, hbmp);

                        if (!TaskbarNative.PrintWindow(_hwnd, hdcMem, TaskbarNative.PW_RENDERFULLCONTENT))
                            return IntPtr.Zero;

                        TaskbarNative.SelectObject(hdcMem, hbmpPrev);

                        return DdbToDib32(hdc, hbmp, width, height);
                    }
                    finally
                    {
                        TaskbarNative.DeleteObject(hbmp);
                    }
                }
                finally
                {
                    TaskbarNative.DeleteDC(hdcMem);
                }
            }
            finally
            {
                TaskbarNative.ReleaseDC(_hwnd, hdc);
            }
        }

        private static IntPtr DdbToDib32(IntPtr hdc, IntPtr hbmp, int width, int height)
        {
            var header = new TaskbarNative.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<TaskbarNative.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
            };

            var bmi = new TaskbarNative.BITMAPINFO { bmiHeader = header };

            var pixels = new byte[width * height * 4];

            int lines = TaskbarNative.GetDIBits(
                hdc, hbmp, 0, (uint)height, pixels, ref bmi, 0);
            if (lines == 0)
                return IntPtr.Zero;

            return CreateDib(pixels, width, height);
        }

        private IntPtr RenderArtworkDib(int side)
        {
            if (_cover is not null)
                return CreateSquareHBitmap(_cover, side);

            return CreateSolidDib(side);
        }

        private static IntPtr CreateSquareHBitmap(BitmapSource source, int side)
        {
            var formatted = new FormatConvertedBitmap(
                source,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                0);
            formatted.Freeze();

            double scale = Math.Max(
                (double)side / formatted.PixelWidth,
                (double)side / formatted.PixelHeight);

            var resized = new TransformedBitmap(
                formatted,
                new System.Windows.Media.ScaleTransform(scale, scale));
            resized.Freeze();

            int scaledWidth = (int)Math.Round(formatted.PixelWidth * scale);
            int scaledHeight = (int)Math.Round(formatted.PixelHeight * scale);

            var cropped = new CroppedBitmap(
                resized,
                new Int32Rect(
                    Math.Max(0, (scaledWidth - side) / 2),
                    Math.Max(0, (scaledHeight - side) / 2),
                    side,
                    side));
            cropped.Freeze();

            return BitmapSourceToDib(cropped, side);
        }

        private static IntPtr BitmapSourceToDib(BitmapSource source, int side)
        {
            int stride = side * 4;
            var pixels = new byte[side * stride];
            source.CopyPixels(pixels, stride, 0);

            return CreateDib(pixels, side, side);
        }

        private static IntPtr CreateSolidDib(int side)
        {
            var pixels = new byte[side * side * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0x1C;     // B
                pixels[i + 1] = 0x18; // G
                pixels[i + 2] = 0x18; // R
                pixels[i + 3] = 0xFF; // A
            }

            return CreateDib(pixels, side, side);
        }

        private static IntPtr CreateDib(byte[] pixels, int width, int height)
        {
            var header = new TaskbarNative.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<TaskbarNative.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
            };

            var bmi = new TaskbarNative.BITMAPINFO { bmiHeader = header };

            IntPtr bits = IntPtr.Zero;

            IntPtr hbmp = TaskbarNative.CreateDIBSection(
                IntPtr.Zero,
                ref bmi,
                0,
                out bits,
                IntPtr.Zero,
                0);
            if (hbmp == IntPtr.Zero || bits == IntPtr.Zero)
                return IntPtr.Zero;

            Marshal.Copy(pixels, 0, bits, pixels.Length);

            return hbmp;
        }
        public void Dispose()
        {
            Detach();

            if (_taskbar is IDisposable disposable)
                disposable.Dispose();

            _playerService.PlaybackStateChanged -= OnPlaybackChanged;
            _playerService.TrackChanged -= OnTrackChanged;
        }
    }
    #region Native Interop
    [ComImport]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITaskbarList3
    {
        void HrInit();

        void AddTab(IntPtr hwnd);

        void DeleteTab(IntPtr hwnd);

        void ActivateTab(IntPtr hwnd);

        void SetActiveAlt(IntPtr hwnd);

        void MarkFullscreenWindow(
            IntPtr hwnd,
            [MarshalAs(UnmanagedType.Bool)] bool fullscreen);

        void SetProgressValue(
            IntPtr hwnd,
            ulong completed,
            ulong total);

        void SetProgressState(
            IntPtr hwnd,
            uint flags);

        void RegisterTab(
            IntPtr hwndTab,
            IntPtr hwndMDI);

        void UnregisterTab(IntPtr hwndTab);

        void SetTabOrder(
            IntPtr hwndTab,
            IntPtr hwndInsertBefore);

        void SetTabActive(
            IntPtr hwndTab,
            IntPtr hwndMDI,
            uint dwReserved);

        int ThumbBarAddButtons(
            IntPtr hwnd,
            uint cButtons,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            TaskbarNative.THUMBBUTTON[] buttons);

        int ThumbBarUpdateButtons(
            IntPtr hwnd,
            uint cButtons,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            [In] TaskbarNative.THUMBBUTTON[] buttons);

        void ThumbBarSetImageList(
            IntPtr hwnd,
            IntPtr himl);

        void SetOverlayIcon(
            IntPtr hwnd,
            IntPtr hIcon,
            [MarshalAs(UnmanagedType.LPWStr)] string? description);

        void SetThumbnailTooltip(
            IntPtr hwnd,
            [MarshalAs(UnmanagedType.LPWStr)] string? tooltip);

        void SetThumbnailClip(
            IntPtr hwnd,
            IntPtr rect);
    }

    internal static class TaskbarNative
    {
        public const uint WM_COMMAND = 0x0111;
        public const uint THBN_CLICKED = 0x1800;

        public const uint WM_DWMSENDICONICTHUMBNAIL = 0x0323;
        public const uint WM_DWMSENDICONICLIVEPREVIEWBITMAP = 0x0326;

        public const uint DWMWA_FORCE_ICONIC_REPRESENTATION = 7;
        public const uint DWMWA_HAS_ICONIC_BITMAP = 10;

        public const uint DWM_SIT_NONE = 0x00000000;
        public const uint DWM_SIT_UPDATE_DISPATCH = 0x00000002;

        public const uint THB_BITMAP = 0x0001;
        public const uint THB_ICON = 0x0002;
        public const uint THB_TOOLTIP = 0x0004;
        public const uint THB_FLAGS = 0x0008;

        public const uint THBF_ENABLED = 0x0000;
        public const uint THBF_DISABLED = 0x0001;
        public const uint THBF_NOBACKGROUND = 0x0004;
        public const uint THBF_HIDDEN = 0x0008;
        public const uint THBF_NONINTERACTIVE = 0x0010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct THUMBBUTTON
        {
            public uint dwMask;
            public uint iId;
            public uint iBitmap;
            public IntPtr hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szTip;

            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
        }

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(
                   IntPtr hdc,
                   ref BITMAPINFO pbmi,
                   uint iUsage,
                   out IntPtr ppvBits,
                   IntPtr hSection,
                   uint dwOffset);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
                    IntPtr hwnd,
                    uint dwAttribute,
                    ref int pvAttribute,
                    int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            uint dwAttribute,
            ref bool pvAttribute,
            int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetIconicThumbnail(
            IntPtr hwnd,
            IntPtr hbmp,
            uint dwSITFlags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetIconicLivePreviewBitmap(
            IntPtr hwnd,
            IntPtr hbmp,
            IntPtr lprcDestination,
            uint dwSITFlags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmInvalidateIconicBitmaps(
            IntPtr hwnd);

[DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public const uint PW_CLIENTONLY = 0x00000001;
        public const uint PW_RENDERFULLCONTENT = 0x00000002;

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(
            IntPtr hwnd,
            IntPtr hdcBlt,
            uint nFlags);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(
            IntPtr hdc,
            int cx,
            int cy);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(
            IntPtr hdc,
            IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern int GetDIBits(
            IntPtr hdc,
            IntPtr hbmp,
            uint uStartScan,
            uint cScanLines,
            [Out] byte[] lpvBits,
            ref BITMAPINFO lpbmi,
            uint uUsage);
    }
    #endregion
}
