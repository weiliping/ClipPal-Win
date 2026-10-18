using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using ClipPal.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClipPal;

public partial class App : Application
{
    private SingleInstance? _single;
    private HwndSource? _messageSource;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ToolStripMenuItem? _countItem;
    private ClipboardMonitor? _monitor;
    private HotKeyManager? _hotKey;
    private ClipboardStore? _store;
    private AppSettings? _settings;
    private HistoryPanel? _panel;
    private SettingsWindow? _settingsWindow;
    private uint _taskbarCreatedMessage;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    public App()
    {
        // 兜底：任何未捕获的 UI 异常都写日志 + 弹框，而不是直接崩掉/只剩 JIT 对话框
        DispatcherUnhandledException += (_, args) =>
        {
            var log = WriteCrashLog(args.Exception);
            MessageBox.Show(
                L10n.T($"ClipPal 遇到一个错误：\n\n{args.Exception.Message}\n\n日志已写入：\n{log}",
                       $"ClipPal hit an error:\n\n{args.Exception.Message}\n\nLog written to:\n{log}"),
                "ClipPal", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteCrashLog(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { WriteCrashLog(args.Exception); args.SetObserved(); };
    }

    /// <summary>把异常写到 %AppData%\ClipPal\crash.log，返回日志路径。</summary>
    private static string WriteCrashLog(Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipPal");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "crash.log");
            File.AppendAllText(file,
                $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===={Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            return file;
        }
        catch
        {
            return "(写日志失败)";
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _single = new SingleInstance("ClipPal.SingleInstance.v1");
        if (!_single.IsFirstInstance)
        {
            MessageBox.Show(L10n.T("ClipPal 已经在运行了。", "ClipPal is already running."), "ClipPal");
            Shutdown();
            return;
        }

        _settings = StorageService.LoadSettings();
        L10n.Language = _settings.AppLanguage;
        ThemeManager.Apply(this);

        _store = new ClipboardStore(_settings);

        // 消息窗口：只收消息、永远不显示（HWND_MESSAGE）
        var parameters = new HwndSourceParameters("ClipPal.MessageWindow")
        {
            ParentWindow = new IntPtr(-3),   // HWND_MESSAGE
            Width = 0,
            Height = 0,
        };
        _messageSource = new HwndSource(parameters);
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        _messageSource.AddHook(MessageHook);

        _monitor = new ClipboardMonitor { IgnoreConcealed = _settings.IgnoreConcealed };
        _monitor.ClipboardChanged += item => _store?.Add(item);
        _monitor.Start(_messageSource.Handle);

        _hotKey = new HotKeyManager();
        _hotKey.HotKeyPressed += TogglePanel;
        _hotKey.Start(_messageSource.Handle, _settings);

        SetupTrayIcon();

        if (!_hotKey.IsRegistered && _hotKey.LastError != null)
            _trayIcon?.ShowBalloonTip(5000, "ClipPal", _hotKey.LastError, System.Windows.Forms.ToolTipIcon.Warning);
    }

    private IntPtr MessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // explorer 重启后托盘图标会消失，需要重新添加
        if (_taskbarCreatedMessage != 0 && (uint)msg == _taskbarCreatedMessage)
            SetupTrayIcon();
        return IntPtr.Zero;
    }

    private void SetupTrayIcon()
    {
        _trayIcon?.Dispose();
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = "ClipPal / 剪贴伴侣",
        };
        _trayIcon.DoubleClick += (_, _) => TogglePanel();

        var menu = new System.Windows.Forms.ContextMenuStrip();

        _countItem = new System.Windows.Forms.ToolStripMenuItem { Enabled = false };
        menu.Items.Add(_countItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(L10n.T("打开历史列表", "Open History"), null, (_, _) => ShowPanel());
        menu.Items.Add(L10n.T("设置…", "Settings…"), null, (_, _) => ShowSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(L10n.T("清空未固定记录", "Clear Unpinned"), null, (_, _) => _store?.ClearUnpinned());
        menu.Items.Add(L10n.T("退出 ClipPal", "Quit ClipPal"), null, (_, _) => ExitApp());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.ContextMenuStrip.Opening += (_, _) => UpdateTrayText();
        UpdateTrayText();
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon != null) return icon;
            }
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    private void UpdateTrayText()
    {
        if (_countItem == null || _store == null) return;
        _countItem.Text = L10n.T($"共 {_store.Items.Count} 条记录", $"{_store.Items.Count} items");
    }

    private void TogglePanel()
    {
        if (_panel is { IsVisible: true }) { _panel.Hide(); return; }
        ShowPanel();
    }

    private void ShowPanel()
    {
        if (_store == null || _monitor == null || _settings == null) return;
        _panel ??= new HistoryPanel(_store, _monitor, _settings);
        _panel.ShowAtCursor();
    }

    private void ShowSettings()
    {
        if (_store == null || _settings == null || _hotKey == null) return;

        _settingsWindow?.Close();
        _settingsWindow = new SettingsWindow(_settings, _store, _hotKey, onLanguageChanged: () =>
        {
            // 语言改了：重建设置窗口，让界面立刻变
            _settingsWindow?.Close();
            _settingsWindow = null;
            ShowSettings();
        });
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ExitApp()
    {
        _store?.SaveNow();
        _monitor?.Dispose();
        _hotKey?.Dispose();
        _trayIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _store?.SaveNow();
        _monitor?.Dispose();
        _hotKey?.Dispose();
        _trayIcon?.Dispose();
        _messageSource?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
