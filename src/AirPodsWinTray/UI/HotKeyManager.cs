using System.Runtime.InteropServices;

namespace AirPodsWinTray.UI;

public sealed class HotKeyManager : IDisposable
{
    private readonly Dictionary<int, Action> _bindings = new();
    private int _nextId = 0xA700;

    public bool Register(IntPtr hwnd, string gesture, Action handler)
    {
        if (!TryParse(gesture, out var modifiers, out var key))
        {
            return false;
        }

        var id = _nextId++;
        if (!RegisterHotKey(hwnd, id, modifiers, key))
        {
            return false;
        }

        _bindings[id] = handler;
        return true;
    }

    public void ProcessMessage(ref Message m)
    {
        if (m.Msg != WM_HOTKEY)
        {
            return;
        }

        if (_bindings.TryGetValue(m.WParam.ToInt32(), out var action))
        {
            action();
        }
    }

    public void Dispose()
    {
        foreach (var id in _bindings.Keys)
        {
            UnregisterHotKey(IntPtr.Zero, id);
        }
    }

    private static bool TryParse(string gesture, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => MOD_CONTROL,
                "ALT" => MOD_ALT,
                "SHIFT" => MOD_SHIFT,
                "WIN" => MOD_WIN,
                _ => 0
            };
        }

        var keyToken = parts[^1].ToUpperInvariant();
        key = keyToken switch
        {
            "P" => 0x50,
            "R" => 0x52,
            _ when keyToken.Length == 1 => keyToken[0],
            _ => 0
        };

        return key != 0;
    }

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}