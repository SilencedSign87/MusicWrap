using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace MusicWrap.UI.Shared.Services
{
    public enum MediaKey
    {
        Unknown,
        PlayPause,
        Next,
        Previous,
        Stop
    }

    public sealed class GlobalHotkeyService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly WindowManager _windowManager;

        // personalized hotkeys
        private readonly List<RegisteredHotkey> _hotkeys = new();
        private int _nextId;

        // current window hook
        private Window? _currentWindow;
        private HwndSourceHook? _currentHook;

        // Low-level keyboard hook
        private nint _keyboardHookId;
        private readonly KeyboardHookProc _hookProc;

        private bool _disposed;

        public event Action<MediaKey>? MediaKeyPressed;

        #region Win32
        private const int WM_HOTKEY = 0x0312;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_MEDIA_NEXT_TRACK = 0xB0;
        private const int VK_MEDIA_PREV_TRACK = 0xB1;
        private const int VK_MEDIA_STOP = 0xB2;
        private const int VK_MEDIA_PLAY_PAUSE = 0xB3;
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(nint hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(nint hWnd, int id);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint SetWindowsHookEx(int idHook, KeyboardHookProc lpfn, nint hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(nint hhk);
        [DllImport("user32.dll")]
        private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint GetModuleHandle(string lpModuleName);
        private delegate nint KeyboardHookProc(int nCode, nint wParam, nint lParam);
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public nint dwExtraInfo;
        }
        #endregion

        public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger, WindowManager windowManager)
        {
            _logger = logger;
            _windowManager = windowManager;

            _windowManager.CurrentWindowChanged += OnCurrentWindowChanged;

            if (_windowManager.CurrentWindow is not null)
                AttachToWindow(_windowManager.CurrentWindow);

            _hookProc = LowLevelKeyboardProc;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var mainModule = curProcess.MainModule;
            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc,
                GetModuleHandle(mainModule!.ModuleName), 0);

            if (_keyboardHookId == 0)
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogWarning("Failed to install low-level keyboard hook (error {Error}). " +
                    "Media keys will only work via SMTC.", error);
            }
            else
            {
                _logger.LogInformation("Low-level keyboard hook installed for media keys.");
            }
        }

        public int RegisterHotkey(int modifiers, int key, Action callback)
        {
            int id = Interlocked.Increment(ref _nextId);
            var entry = new RegisteredHotkey(id, modifiers, key, callback);
            
            RegisterOnCurrentWindow(entry);
            _hotkeys.Add(entry);
            _logger.LogDebug("Hotkey registered (id={Id}, mod=0x{Mod:X}, key=0x{Key:X})", id, modifiers, key);
            return id;
        }
        public bool UnregisterHotkey(int id)
        {
            var entry = _hotkeys.FirstOrDefault(h => h.Id == id);
            if (entry is null) return false;
            if (entry.RegisteredHandle != nint.Zero)
                UnregisterHotKey(entry.RegisteredHandle, id);
            _hotkeys.Remove(entry);
            return true;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _windowManager.CurrentWindowChanged -= OnCurrentWindowChanged;
            DetachFromCurrentWindow();
            if (_keyboardHookId != 0)
                UnhookWindowsHookEx(_keyboardHookId);
            foreach (var entry in _hotkeys)
            {
                if (entry.RegisteredHandle != nint.Zero)
                    UnregisterHotKey(entry.RegisteredHandle, entry.Id);
            }
            _hotkeys.Clear();
        }
        #region Window handling
        private void OnCurrentWindowChanged(Window? window)
        {
            DetachFromCurrentWindow();
            if (window is not null)
                AttachToWindow(window);
        }
        private void AttachToWindow(Window window)
        {
            _currentWindow = window;
            _currentHook = WndProc;
            window.SourceInitialized += OnWindowSourceInitialized;
            if (window.IsInitialized)
            {
                var helper = new WindowInteropHelper(window);
                if (helper.Handle != nint.Zero)
                {
                    var source = HwndSource.FromHwnd(helper.Handle);
                    if (source is not null)
                    {
                        source.AddHook(_currentHook);
                        ReRegisterAllHotkeys(helper.Handle);
                        _logger.LogDebug("Attached to window {Window}", window.GetType().Name);
                    }
                }
            }
        }
        private void OnWindowSourceInitialized(object? sender, EventArgs e)
        {
            if (sender is not Window window) return;
            window.SourceInitialized -= OnWindowSourceInitialized;
            var helper = new WindowInteropHelper(window);
            var source = HwndSource.FromHwnd(helper.Handle);
            if (source is not null && _currentHook is not null)
            {
                source.AddHook(_currentHook);
                ReRegisterAllHotkeys(helper.Handle);
                _logger.LogDebug("Attached to window {Window} (on SourceInitialized)", window.GetType().Name);
            }
        }
        private void DetachFromCurrentWindow()
        {
            if (_currentWindow is null) return;
            foreach (var entry in _hotkeys)
            {
                if (entry.RegisteredHandle != nint.Zero)
                {
                    UnregisterHotKey(entry.RegisteredHandle, entry.Id);
                    entry.RegisteredHandle = nint.Zero;
                }
            }
            if (_currentHook is not null)
            {
                try
                {
                    var helper = new WindowInteropHelper(_currentWindow);
                    if (helper.Handle != nint.Zero)
                    {
                        var source = HwndSource.FromHwnd(helper.Handle);
                        source?.RemoveHook(_currentHook);
                    }
                }
                catch { /* window already disposed */ }
            }
            _currentWindow.SourceInitialized -= OnWindowSourceInitialized;
            _currentWindow = null;
            _currentHook = null;
        }
        private void RegisterOnCurrentWindow(RegisteredHotkey entry)
        {
            if (_currentWindow is null) return;
            var helper = new WindowInteropHelper(_currentWindow);
            if (helper.Handle == nint.Zero) return;
            if (RegisterHotKey(helper.Handle, entry.Id, entry.Modifiers, entry.Key))
                entry.RegisteredHandle = helper.Handle;
        }
        private void ReRegisterAllHotkeys(nint hwnd)
        {
            foreach (var entry in _hotkeys)
            {
                if (RegisterHotKey(hwnd, entry.Id, entry.Modifiers, entry.Key))
                    entry.RegisteredHandle = hwnd;
            }
        }
        #endregion
        #region Internal
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                var entry = _hotkeys.FirstOrDefault(h => h.Id == id);
                if (entry is not null)
                {
                    entry.Callback();
                    handled = true;
                }
            }
            return nint.Zero;
        }
        private nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                MediaKey? key = hookStruct.vkCode switch
                {
                    VK_MEDIA_PLAY_PAUSE => MediaKey.PlayPause,
                    VK_MEDIA_NEXT_TRACK => MediaKey.Next,
                    VK_MEDIA_PREV_TRACK => MediaKey.Previous,
                    VK_MEDIA_STOP => MediaKey.Stop,
                    _ => null
                };
                if (key.HasValue)
                {
                    MediaKeyPressed?.Invoke(key.Value);
                    return (nint)1;
                }
            }
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }
        #endregion

        private sealed class RegisteredHotkey
        {
            public int Id { get; }
            public int Modifiers { get; }
            public int Key { get; }
            public Action Callback { get; }
            public nint RegisteredHandle { get; set; }
            public RegisteredHotkey(int id, int modifiers, int key, Action callback)
            {
                Id = id;
                Modifiers = modifiers;
                Key = key;
                Callback = callback;
                RegisteredHandle = nint.Zero;
            }
        }
    }
}
