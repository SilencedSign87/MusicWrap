using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
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

        private bool _buttonsAdded;
        private bool _isPlaying;

        private const uint ButtonPrevious = 1;
        private const uint ButtonPlayPause = 2;
        private const uint ButtonNext = 3;
        private const int IconSize = 32;

        private readonly IMusicPlayerService _playerService;
        private readonly IUIDispatcher _dispatcher;
        private readonly ILogger _logger;
        private List<IntPtr> _createdIcons = new();

        public TaskbarController(
            IMusicPlayerService playerService,
            ILogger<TaskbarController> logger,
            IUIDispatcher dispatcher
            )
        {
            _playerService = playerService;
            _logger = logger;
            _dispatcher = dispatcher;

            var type = Type.GetTypeFromCLSID(
                new Guid("56FDF344-FD6D-11D0-958A-006097C9A090"));

            _taskbar = (ITaskbarList3)Activator.CreateInstance(type!)!;
            _taskbar.HrInit();

            _isPlaying = _playerService.IsPlaying;
            _playerService.PlaybackStateChanged += OnPlaybackChanged;
        }

        private void OnPlaybackChanged(object? sender, ManagedBass.PlaybackState e)
        {
            SetPlaying(e == ManagedBass.PlaybackState.Playing);
        }

        public void Attach(Window window)
        {
            if (ReferenceEquals(_window, window))
                return;

            Detach();

            _window = window;
            _hwnd = new WindowInteropHelper(window).EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);

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
            AddButtons();
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

        public void SetPlaying(bool playing)
        {
            _isPlaying = playing;

            if (_hwnd == IntPtr.Zero || !_buttonsAdded)
                return;

            UpdateButtons();
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
                _logger.LogWarning("ThumbBarAddButtons -> 0x{hr:X8}", hr);

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
                    TaskbarNative.DestroyIcon(icon);
            }

            icons.Clear();
        }

        private TaskbarNative.THUMBBUTTON[] CreateButtons()
        {
            TaskbarNative.THUMBBUTTON[] buttons =
            [
                new TaskbarNative.THUMBBUTTON
                {
                    dwMask = TaskbarNative.THB_ICON | TaskbarNative.THB_TOOLTIP | TaskbarNative.THB_FLAGS,
                    iId = ButtonPrevious,
                    hIcon = CreateIcon("previous"),
                    szTip = "Previous",
                    dwFlags = TaskbarNative.THBF_ENABLED
                },
                new TaskbarNative.THUMBBUTTON
                {
                    dwMask = TaskbarNative.THB_ICON | TaskbarNative.THB_TOOLTIP | TaskbarNative.THB_FLAGS,
                    iId = ButtonPlayPause,
                    hIcon = CreateIcon(_isPlaying ? "pause" : "play"),
                    szTip = _isPlaying ? "Pause" : "Play",
                    dwFlags = TaskbarNative.THBF_ENABLED
                },
                new TaskbarNative.THUMBBUTTON
                {
                    dwMask = TaskbarNative.THB_ICON | TaskbarNative.THB_TOOLTIP | TaskbarNative.THB_FLAGS,
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
            var uri = new Uri($"pack://application:,,,/Resources/Icons/{fileName}", UriKind.Absolute);

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
            if (msg == TaskbarNative.WM_COMMAND)
            {
                var command = (uint)(wParam.ToInt64() & 0xFFFF);
                var notification = (uint)((wParam.ToInt64() >> 16) & 0xFFFF);

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
                    _dispatcher.Invoke(() => _playerService.Previous());
                    break;
                case ButtonPlayPause:
                    _dispatcher.Invoke(() =>
                            {
                                if (_playerService.IsPlaying) _playerService.Pause();
                                else _playerService.Play();
                            });
                    break;
                case ButtonNext:
                    _dispatcher.Invoke(() => _playerService.Next());
                    break;
            }
        }

        public void Dispose()
        {
            Detach();

            if (_taskbar is IDisposable disposable)
                disposable.Dispose();

            _playerService.PlaybackStateChanged -= OnPlaybackChanged;
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
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, uint flags);
        void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
        void UnregisterTab(IntPtr hwndTab);
        void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
        void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint dwReserved);
        int ThumbBarAddButtons(IntPtr hwnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] TaskbarNative.THUMBBUTTON[] buttons);
        int ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)][In] TaskbarNative.THUMBBUTTON[] buttons);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
        void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string? description);
        void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? tooltip);
        void SetThumbnailClip(IntPtr hwnd, IntPtr rect);
    }

    internal static class TaskbarNative
    {
        public const uint WM_COMMAND = 0x0111;
        public const uint THBN_CLICKED = 0x1800;

        public const uint THB_ICON = 0x0002;
        public const uint THB_TOOLTIP = 0x0004;
        public const uint THB_FLAGS = 0x0008;

        public const uint THBF_ENABLED = 0x0000;

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

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
    #endregion
}
