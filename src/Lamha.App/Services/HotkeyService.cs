using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Lamha.App.Services;

/// <summary>One global keyboard shortcut (RegisterHotKey) that works from any app.</summary>
public sealed partial class HotkeyService : IDisposable
{
    private const int Id = 0x4C41; // "LA"
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

    private readonly HwndSource _window;
    private Action? _onPressed;
    private bool _registered;

    public HotkeyService()
    {
        // Invisible message-only window to receive WM_HOTKEY.
        _window = new HwndSource(new HwndSourceParameters("LamhaHotkey") { ParentWindow = new IntPtr(-3) });
        _window.AddHook(WndProc);
    }

    /// <summary>Registers e.g. "Ctrl+Alt+N". Returns false if the text is invalid or another app owns the combo.</summary>
    public bool Register(string combo, Action onPressed)
    {
        if (!TryParse(combo, out uint mods, out uint vk)) return false;
        Unregister();
        _registered = RegisterHotKey(_window.Handle, Id, mods | MOD_NOREPEAT, vk);
        if (_registered) _onPressed = onPressed;
        return _registered;
    }

    public void Unregister()
    {
        if (_registered) UnregisterHotKey(_window.Handle, Id);
        _registered = false;
    }

    public void Dispose()
    {
        Unregister();
        _window.Dispose();
    }

    public static bool TryParse(string combo, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        foreach (string part in combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": mods |= MOD_CONTROL; break;
                case "alt": mods |= MOD_ALT; break;
                case "shift": mods |= MOD_SHIFT; break;
                case "win": mods |= MOD_WIN; break;
                default:
                    if (!Enum.TryParse(part, ignoreCase: true, out Key key)) return false;
                    vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        return mods != 0 && vk != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == Id)
        {
            _onPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
