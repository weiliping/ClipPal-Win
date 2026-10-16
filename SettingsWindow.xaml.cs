using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClipPal.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace ClipPal;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ClipboardStore _store;
    private readonly HotKeyManager _hotKey;
    private readonly Action _onLanguageChanged;
    private readonly DispatcherTimer _opacitySaveTimer;

    private bool _loading;
    private bool _recording;

    public SettingsWindow(AppSettings settings, ClipboardStore store, HotKeyManager hotKey, Action onLanguageChanged)
    {
        InitializeComponent();
        _settings = settings;
        _store = store;
        _hotKey = hotKey;
        _onLanguageChanged = onLanguageChanged;

        _opacitySaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _opacitySaveTimer.Tick += (_, _) => { _opacitySaveTimer.Stop(); Save(); };

        Title = L10n.T("剪贴伴侣设置", "ClipPal Settings");
        ApplyTexts();
        LoadFromSettings();
    }

    private void ApplyTexts()
    {
        TitleText.Text = L10n.T("设置", "Settings");

        LimitHeader.Text = L10n.T("历史记录条数", "History Size");
        LimitCustom.Content = L10n.T("自定义", "Custom");
        LimitHint.Text = L10n.T(
            "超出上限时按 LRU 淘汰：最久没用过的那条先丢。固定的记录不会被淘汰，也不占这个名额。",
            "Beyond the limit the least recently used entry is dropped. Pinned entries are never evicted and don't count toward the limit.");

        HotKeyHeader.Text = L10n.T("全局快捷键", "Global Shortcut");
        ResetHotKeyBtn.Content = L10n.T("恢复默认", "Reset");
        HotKeyHint.Text = L10n.T("点一下左边的框，然后按下新的组合键（Esc 取消）。",
                                 "Click the box, then press a new key combination (Esc to cancel).");

        BehaviorHeader.Text = L10n.T("行为", "Behavior");
        PasteAuto.Content = L10n.T("在列表里选中后自动粘贴到当前 App", "Paste into the frontmost app after selecting");
        PlaySound.Content = L10n.T("粘贴后播放提示音", "Play a sound after pasting");
        IgnoreConcealed.Content = L10n.T("忽略密码管理器等标记为隐藏的内容", "Ignore concealed content (password managers)");
        LaunchAtLogin.Content = L10n.T("开机时自动启动", "Launch at login");
        ConfirmClear.Content = L10n.T("清空前先确认", "Ask before clearing");
        ElevationHint.Text = L10n.T(
            "提示：如果目标程序是以管理员身份运行的，Windows 不允许普通程序向它发送按键，自动粘贴会无效。",
            "Note: if the target app runs as administrator, Windows blocks synthetic keystrokes from a normal app, so auto-paste will not work there.");

        AppearanceHeader.Text = L10n.T("外观", "Appearance");
        LanguageLabel.Text = L10n.T("界面语言", "Language");
        OpacityLabel.Text = L10n.T("面板透明度", "Panel opacity");

        DataHeader.Text = L10n.T("数据", "Data");
        ShowFolderBtn.Content = L10n.T("打开目录", "Show Folder");
        ClearUnpinnedBtn.Content = L10n.T("清空未固定", "Clear Unpinned");
        ClearAllBtn.Content = L10n.T("清空全部", "Clear All");
        CloseBtn.Content = L10n.T("关闭", "Close");

        // 语言下拉：三项固定文案，不随语言变化
        _loading = true;
        LanguageBox.Items.Clear();
        LanguageBox.Items.Add(new ComboBoxItem { Content = "跟随系统 / Follow System", Tag = "system" });
        LanguageBox.Items.Add(new ComboBoxItem { Content = "中文", Tag = "zh" });
        LanguageBox.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
        _loading = false;
    }

    private void LoadFromSettings()
    {
        _loading = true;

        var limit = Math.Clamp(_settings.HistoryLimit, 5, 500);
        if (limit == 30) Limit30.IsChecked = true;
        else if (limit == 50) Limit50.IsChecked = true;
        else if (limit == 100) Limit100.IsChecked = true;
        else { LimitCustom.IsChecked = true; CustomLimitBox.Text = limit.ToString(); }
        CustomLimitBox.Text = limit.ToString();

        PasteAuto.IsChecked = _settings.PasteAutomatically;
        PlaySound.IsChecked = _settings.PlaySound;
        IgnoreConcealed.IsChecked = _settings.IgnoreConcealed;
        LaunchAtLogin.IsChecked = StartupManager.IsEnabled();
        ConfirmClear.IsChecked = _settings.ConfirmBeforeClearing;

        foreach (ComboBoxItem item in LanguageBox.Items)
            if ((string?)item.Tag == _settings.AppLanguage) LanguageBox.SelectedItem = item;

        OpacitySlider.Value = Math.Clamp(_settings.PanelOpacity, 0.3, 1.0);
        UpdateOpacityLabel();
        UpdateRecorderText();

        StoragePathText.Text = StorageService.StorageFolder;

        _loading = false;

        Limit30.Checked += (_, _) => ApplyLimit(30);
        Limit50.Checked += (_, _) => ApplyLimit(50);
        Limit100.Checked += (_, _) => ApplyLimit(100);
        LimitCustom.Checked += (_, _) => ApplyCustomLimit();
        CustomLimitBox.LostFocus += (_, _) => ApplyCustomLimit();

        PasteAuto.Click += (_, _) => { _settings.PasteAutomatically = PasteAuto.IsChecked == true; Save(); };
        PlaySound.Click += (_, _) => { _settings.PlaySound = PlaySound.IsChecked == true; Save(); };
        IgnoreConcealed.Click += (_, _) =>
        {
            _settings.IgnoreConcealed = IgnoreConcealed.IsChecked == true;
            Save();
        };
        LaunchAtLogin.Click += (_, _) =>
        {
            var want = LaunchAtLogin.IsChecked == true;
            if (StartupManager.SetEnabled(want)) _settings.LaunchAtLogin = want;
            else LaunchAtLogin.IsChecked = StartupManager.IsEnabled();
            Save();
        };
        ConfirmClear.Click += (_, _) => { _settings.ConfirmBeforeClearing = ConfirmClear.IsChecked == true; Save(); };
    }

    private void ApplyLimit(int value)
    {
        if (_loading) return;
        _settings.HistoryLimit = value;
        _loading = true; CustomLimitBox.Text = value.ToString(); _loading = false;
        _store.TrimToLimit();
        Save();
    }

    private void ApplyCustomLimit()
    {
        if (_loading) return;
        if (!int.TryParse(CustomLimitBox.Text, out var v)) return;
        v = Math.Clamp(v, 5, 500);
        _settings.HistoryLimit = v;
        _store.TrimToLimit();
        Save();
    }

    private void Save()
    {
        StorageService.SaveSettings(_settings);
        _store.UpdateSettings(_settings);
    }

    // ---------- 快捷键录制 ----------

    private void UpdateRecorderText()
    {
        RecorderText.Text = HotKeyManager.Describe(_settings.HotKeyModifiers, _settings.HotKeyVirtualKey);
    }

    private void RecorderBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        RecorderBox.Focus();
        _recording = true;
        HotKeyErrorText.Visibility = Visibility.Collapsed;
        RecorderText.Text = L10n.T("请按下快捷键…", "Press a shortcut…");
    }

    private void RecorderBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _recording = false;
        UpdateRecorderText();
    }

    private void RecorderBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape) { _recording = false; UpdateRecorderText(); return; }

        // 只按住修饰键时继续等主键
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

        // ModifierKeys 的位与 Win32 MOD_* 完全一致
        var modifiers = (uint)Keyboard.Modifiers;
        if (modifiers == 0)
        {
            HotKeyErrorText.Text = L10n.T("至少要带一个修饰键（Ctrl / Alt / Shift / Win）。",
                                          "Use at least one modifier (Ctrl / Alt / Shift / Win).");
            HotKeyErrorText.Visibility = Visibility.Visible;
            return;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (_hotKey.Register((HotKeyManager.Modifiers)modifiers, vk))
        {
            _settings.HotKeyModifiers = modifiers;
            _settings.HotKeyVirtualKey = vk;
            Save();
            _recording = false;
            HotKeyErrorText.Visibility = Visibility.Collapsed;
            UpdateRecorderText();
        }
        else
        {
            HotKeyErrorText.Text = _hotKey.LastError ?? string.Empty;
            HotKeyErrorText.Visibility = Visibility.Visible;
            UpdateRecorderText();
        }
    }

    private void ResetHotKeyBtn_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings();
        if (_hotKey.Register((HotKeyManager.Modifiers)defaults.HotKeyModifiers, defaults.HotKeyVirtualKey))
        {
            _settings.HotKeyModifiers = defaults.HotKeyModifiers;
            _settings.HotKeyVirtualKey = defaults.HotKeyVirtualKey;
            Save();
            HotKeyErrorText.Visibility = Visibility.Collapsed;
            UpdateRecorderText();
        }
        else
        {
            HotKeyErrorText.Text = _hotKey.LastError ?? string.Empty;
            HotKeyErrorText.Visibility = Visibility.Visible;
        }
    }

    // ---------- 外观 ----------

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LanguageBox.SelectedItem is not ComboBoxItem item) return;
        var tag = (string?)item.Tag ?? "system";
        if (tag == _settings.AppLanguage) return;

        _settings.AppLanguage = tag;
        L10n.Language = tag;
        Save();
        _onLanguageChanged();   // 重建设置窗口，界面立刻变
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.PanelOpacity = Math.Clamp(e.NewValue, 0.3, 1.0);
        UpdateOpacityLabel();
        _opacitySaveTimer.Stop();
        _opacitySaveTimer.Start();
    }

    private void UpdateOpacityLabel()
    {
        OpacityLabel.Text = L10n.T($"面板透明度：{Math.Round((1 - _settings.PanelOpacity) * 100)}%",
                                   $"Panel transparency: {Math.Round((1 - _settings.PanelOpacity) * 100)}%");
    }

    // ---------- 数据 ----------

    private void ShowFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(StorageService.StorageFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = StorageService.StorageFolder,
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private void ClearUnpinnedBtn_Click(object sender, RoutedEventArgs e) => _store.ClearUnpinned();

    private void ClearAllBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.ConfirmBeforeClearing)
        {
            var result = MessageBox.Show(
                L10n.T("确定要清空全部剪贴板历史吗？", "Clear all clipboard history?"),
                L10n.T("清空历史", "Clear History"),
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK) return;
        }
        _store.ClearAll();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
