using System.Runtime.InteropServices;

namespace VRC_cantalkcn.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private static int _nextHotkeyId = 0x4C20;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint WM_HOTKEY = 0x0312;

    private readonly int _hotkeyId;

    private IntPtr _windowHandle;
    private SubclassProc? _subclassProc;
    private bool _subclassAttached;

    public event EventHandler? HotkeyPressed;

    public GlobalHotkeyService()
    {
        _hotkeyId = Interlocked.Increment(ref _nextHotkeyId);
    }

    public void Register(IntPtr windowHandle, string hotkeyText)
    {
        Unregister();

        if (windowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(hotkeyText))
        {
            return;
        }

        var (modifiers, virtualKey) = ParseHotkey(hotkeyText);

        _windowHandle = windowHandle;
        _subclassProc = WindowSubclassProc;

        if (!SetWindowSubclass(_windowHandle, _subclassProc, (IntPtr)_hotkeyId, IntPtr.Zero))
        {
            throw new InvalidOperationException("热键窗口钩子安装失败。");
        }

        _subclassAttached = true;

        if (!RegisterHotKey(_windowHandle, _hotkeyId, modifiers, virtualKey))
        {
            Unregister();
            throw new InvalidOperationException("热键注册失败，可能被占用。请更换热键。");
        }
    }

    public void Unregister()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, _hotkeyId);

            if (_subclassAttached && _subclassProc is not null)
            {
                RemoveWindowSubclass(_windowHandle, _subclassProc, (IntPtr)_hotkeyId);
            }
        }

        _subclassAttached = false;
        _subclassProc = null;
        _windowHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
    }

    private IntPtr WindowSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr uIdSubclass,
        IntPtr dwRefData)
    {
        if (uMsg == WM_HOTKEY && wParam == (IntPtr)_hotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private static (uint modifiers, uint virtualKey) ParseHotkey(string raw)
    {
        var parts = raw
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.ToLowerInvariant())
            .ToList();

        if (parts.Count == 0)
        {
            throw new InvalidOperationException("热键格式无效。");
        }

        uint modifiers = 0;
        string keyToken = parts[^1];

        foreach (var part in parts.Take(parts.Count - 1))
        {
            switch (part)
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    throw new InvalidOperationException($"不支持的热键修饰符: {part}");
            }
        }

        var vk = ParseVirtualKey(keyToken);
        return (modifiers, vk);
    }

    private static uint ParseVirtualKey(string token)
    {
        if (token.Length == 1)
        {
            char ch = token.ToUpperInvariant()[0];
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
            {
                return ch;
            }
        }

        if (token.StartsWith("f", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(token[1..], out var fn)
            && fn >= 1
            && fn <= 24)
        {
            return (uint)(0x70 + fn - 1);
        }

        return token switch
        {
            "space" => 0x20,
            "enter" => 0x0D,
            "tab" => 0x09,
            "esc" or "escape" => 0x1B,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            _ => throw new InvalidOperationException($"不支持的热键按键: {token}"),
        };
    }

    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr uIdSubclass,
        IntPtr dwRefData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        IntPtr uIdSubclass,
        IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        IntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam);
}
