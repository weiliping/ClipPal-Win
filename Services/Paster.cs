using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;

namespace ClipPal.Services;

/// <summary>
/// 模拟键盘粘贴（Ctrl+V）与提示音。
/// </summary>
public static class Paster
{
    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    // 关键：union 必须能容纳最大的成员 MOUSEINPUT(32 字节)。
    // 只放 KEYBDINPUT(24 字节) 的话，Marshal.SizeOf<INPUT>() 会算成 32，
    // 而 SendInput 要求恰好等于原生 sizeof(INPUT)=40 —— 不符时它直接返回 0
    // 并置 ERROR_INVALID_PARAMETER(87)，粘贴静默失效。
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>模拟一次 Ctrl+V。返回是否成功投递。</summary>
    public static bool Paste()
    {
        var inputs = new[]
        {
            MakeKey(VK_CONTROL, false),
            MakeKey(VK_V, false),
            MakeKey(VK_V, true),
            MakeKey(VK_CONTROL, true),
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            Debug.WriteLine($"[Paster] SendInput 失败 sent={sent}/{inputs.Length} cbSize={Marshal.SizeOf<INPUT>()} err={Marshal.GetLastWin32Error()}");
            return false;
        }
        return true;
    }

    /// <summary>粘贴后的提示音。</summary>
    public static void PlayFeedbackSound()
    {
        try { SystemSounds.Asterisk.Play(); } catch { /* 没有音频设备就算了 */ }
    }

    private static INPUT MakeKey(byte vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };
}
