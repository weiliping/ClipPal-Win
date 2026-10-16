using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ClipPal.Services;

/// <summary>全局快捷键（RegisterHotKey），可捕获系统级按键。</summary>
public class HotKeyManager : IDisposable
{
    [Flags]
    public enum Modifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
    }

    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0x5A01;
    private const uint MOD_NOREPEAT = 0x4000;   // 长按不重复触发

    private HwndSource? _hwndSource;
    private bool _isRegistered;

    public event Action? HotKeyPressed;

    /// <summary>最近一次注册失败的原因（供 UI 提示）。</summary>
    public string? LastError { get; private set; }

    public bool IsRegistered => _isRegistered;

    public bool Start(IntPtr hwnd, AppSettings settings)
    {
        _hwndSource = HwndSource.FromHwnd(hwnd);
        if (_hwndSource == null)
        {
            LastError = L10n.T("无法创建消息窗口，快捷键不可用。", "Could not create the message window; the hot key is unavailable.");
            return false;
        }
        _hwndSource.AddHook(WndProc);
        return Register((Modifiers)settings.HotKeyModifiers, settings.HotKeyVirtualKey);
    }

    public bool Register(Modifiers modifiers, uint virtualKey)
    {
        Unregister();
        LastError = null;

        if (_hwndSource == null)
        {
            LastError = L10n.T("快捷键服务未启动。", "The hot key service is not started.");
            return false;
        }

        _isRegistered = RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, (uint)modifiers | MOD_NOREPEAT, virtualKey);
        if (!_isRegistered)
        {
            var code = Marshal.GetLastWin32Error();
            LastError = L10n.T(
                $"注册 {Describe(modifiers, virtualKey)} 失败（错误码 {code}）——这个组合可能已被其他程序占用。",
                $"Could not register {Describe(modifiers, virtualKey)} (error {code}) — the combination may be taken by another app.");
        }
        return _isRegistered;
    }

    public void Unregister()
    {
        if (!_isRegistered || _hwndSource == null) return;
        UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
        _isRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotKeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_hwndSource == null) return;
        _hwndSource.RemoveHook(WndProc);
        _hwndSource = null;
    }

    // ---------- 显示 ----------

    public static string Describe(Modifiers modifiers, uint virtualKey)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(Modifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(Modifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(Modifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(Modifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(virtualKey));
        return string.Join("+", parts);
    }

    public static string Describe(uint modifiers, uint virtualKey) =>
        Describe((Modifiers)modifiers, virtualKey);

    public static string KeyName(uint vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x60 and <= 0x69 => "Num" + (vk - 0x60),
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2D => "Insert",
        0x2E => "Delete",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => L10n.T($"键码 {vk}", $"Key {vk}"),
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
