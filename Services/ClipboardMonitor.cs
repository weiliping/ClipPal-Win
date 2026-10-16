using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ClipPal.Models;
using Clipboard = System.Windows.Clipboard;

namespace ClipPal.Services;

/// <summary>
/// 监听 Windows 剪贴板变化（AddClipboardFormatListener + WM_CLIPBOARDUPDATE），
/// 比定时轮询更及时、开销也更低。
/// </summary>
public class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    /// <summary>Win10+ / 传统约定：标记为这些格式的内容不要收录（密码管理器等在用）。</summary>
    private static readonly string[] ConcealedFormats =
    {
        "ExcludeClipboardContentFromMonitorProcessing",
        "CanIncludeInClipboardHistory",
        "CanUploadToCloudClipboard",
        "Clipboard Viewer Ignore",
    };

    private HwndSource? _hwndSource;
    private uint _lastSequence;   // 上次已处理的剪贴板序号，用于忽略自己写入引发的变化
    private bool _ignoreConcealed = true;

    public event Action<ClipboardItem>? ClipboardChanged;

    public bool IgnoreConcealed
    {
        get => _ignoreConcealed;
        set => _ignoreConcealed = value;
    }

    public bool Start(Window window) => Start(new WindowInteropHelper(window).Handle);

    public bool Start(IntPtr hwnd)
    {
        _hwndSource = HwndSource.FromHwnd(hwnd);
        if (_hwndSource == null) return false;

        _hwndSource.AddHook(WndProc);
        _lastSequence = GetClipboardSequenceNumber();

        if (!AddClipboardFormatListener(hwnd))
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
            return false;
        }
        return true;
    }

    /// <summary>把一条历史记录写回剪贴板：文件还原成文件列表，文本写文本。</summary>
    public bool SetItem(ClipboardItem item)
    {
        try
        {
            if (item.Kind == ClipboardItemKind.FileDrop)
            {
                var paths = item.Content
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
                if (paths.Count == 0) return false;
                var collection = new System.Collections.Specialized.StringCollection();
                collection.AddRange(paths.ToArray());
                Clipboard.SetFileDropList(collection);
            }
            else
            {
                Clipboard.SetText(item.Content);
            }
            _lastSequence = GetClipboardSequenceNumber();
            return true;
        }
        catch (Exception ex)
        {
            // 剪贴板偶尔被别的程序独占，重试交给调用方
            System.Diagnostics.Debug.WriteLine($"[Monitor] 写剪贴板失败: {ex.Message}");
            return false;
        }
    }

    public void SetText(string text) => SetItem(new ClipboardItem(text));

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            var sequence = GetClipboardSequenceNumber();
            if (sequence == _lastSequence) return IntPtr.Zero;  // 自己写的，跳过
            _lastSequence = sequence;
            TryCapture();
        }
        return IntPtr.Zero;
    }

    private void TryCapture()
    {
        // 重试：复制瞬间剪贴板常被占用
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (_ignoreConcealed && IsConcealed()) return;

                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files.Count > 0)
                    {
                        var paths = files.Cast<string>().ToArray();
                        ClipboardChanged?.Invoke(new ClipboardItem(
                            string.Join(Environment.NewLine, paths), ClipboardItemKind.FileDrop));
                        return;
                    }
                }

                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                        ClipboardChanged?.Invoke(new ClipboardItem(text));
                }
                return;
            }
            catch
            {
                System.Threading.Thread.Sleep(30);
            }
        }
    }

    private static bool IsConcealed()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data == null) return false;
            foreach (var format in ConcealedFormats)
            {
                if (!data.GetDataPresent(format)) continue;

                // CanIncludeInClipboardHistory / CanUploadToCloudClipboard 是 DWORD，0 表示禁止
                if (format.StartsWith("Can", StringComparison.Ordinal))
                {
                    if (data.GetData(format) is int value && value == 0) return true;
                }
                else
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    public void Dispose()
    {
        if (_hwndSource == null) return;
        RemoveClipboardFormatListener(_hwndSource.Handle);
        _hwndSource.RemoveHook(WndProc);
        _hwndSource = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
