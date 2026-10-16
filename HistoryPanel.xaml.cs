using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipPal.Models;
using ClipPal.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ClipPal;

public partial class HistoryPanel : Window
{
    private readonly ClipboardStore _store;
    private readonly ClipboardMonitor _monitor;
    private readonly AppSettings _settings;
    private readonly System.Windows.Threading.DispatcherTimer _hideTimer;
    private bool _suppressAutoHide;      // 确认框等弹窗期间不要收起
    private ClipboardItem? _contextItem; // 右键作用的那一条

    public HistoryPanel(ClipboardStore store, ClipboardMonitor monitor, AppSettings settings)
    {
        InitializeComponent();
        _store = store;
        _monitor = monitor;
        _settings = settings;

        // 失焦 250ms 后收起（对齐 macOS 版）
        _hideTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!IsActive) Hide();
        };

        HistoryList.ItemsSource = _store.Items;
        HistoryList.MouseLeftButtonUp += OnListClick;
        HistoryList.PreviewMouseRightButtonDown += OnListRightClick;

        Deactivated += (_, _) =>
        {
            if (_suppressAutoHide) return;
            _hideTimer.Stop();
            _hideTimer.Start();
        };
        Activated += (_, _) => _hideTimer.Stop();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>唤出面板：重置选中、定位到鼠标附近、拿焦点。</summary>
    public void ShowAtCursor()
    {
        ApplyBackgroundOpacity();
        _store.Renumber();
        HistoryList.SelectedIndex = _store.Items.Count > 0 ? 0 : -1;
        if (HistoryList.SelectedItem != null) HistoryList.ScrollIntoView(HistoryList.SelectedItem);

        FooterHint.Text = L10n.T("↑↓ 选择 · ⏎ 粘贴 · 1-9 直选 · ⌫ 删除 · Esc 关闭",
                                 "↑↓ select · ⏎ paste · 1-9 quick · ⌫ delete · Esc close");
        TrashButton.ToolTip = L10n.T("清空全部历史", "Clear all history");
        TrashButton.IsEnabled = _store.Items.Count > 0;

        PositionNearCursor();
        Show();
        Activate();
        HistoryList.Focus();
    }

    /// <summary>
    /// 透明度只作用在背景层，文字始终不透明 —— 与 macOS 版一致。
    /// （用 Window.Opacity 会把文字和阴影一起变淡，观感不同。）
    /// </summary>
    private void ApplyBackgroundOpacity()
    {
        var opacity = Math.Clamp(_settings.PanelOpacity, 0.3, 1.0);
        if (TryFindResource("PanelBackgroundBrush") is SolidColorBrush brush)
            PanelBorder.Background = new SolidColorBrush(brush.Color) { Opacity = opacity };
    }

    private void PositionNearCursor()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var area = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;

        // WinForms 给的是物理像素，WPF 的 Left/Top 是 DIP，按当前 DPI 换算
        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY <= 0 ? 1 : dpi.DpiScaleY;

        var areaLeft = area.Left / scaleX;
        var areaTop = area.Top / scaleY;
        var areaWidth = area.Width / scaleX;
        var areaHeight = area.Height / scaleY;

        var x = cursor.X / scaleX - Width / 2;
        var y = cursor.Y / scaleY - Height / 2;

        Left = areaLeft + Math.Max(8, Math.Min(x - areaLeft, areaWidth - Width - 8));
        Top = areaTop + Math.Max(8, Math.Min(y - areaTop, areaHeight - Height - 8));
    }

    private void OnListClick(object sender, MouseButtonEventArgs e)
    {
        // 单击某行直接粘贴；点在空白处不动作
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>() != null)
            PasteSelected();
    }

    private void OnListRightClick(object sender, MouseButtonEventArgs e)
    {
        var item = (e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>();
        if (item?.DataContext is ClipboardItem data)
        {
            _contextItem = data;
            HistoryList.SelectedItem = data;
        }
        else
        {
            _contextItem = null;
        }
    }

    private void RowMenu_Opened(object sender, RoutedEventArgs e)
    {
        // DataTemplate 里的控件拿不到代码字段，按 Tag 找
        if (sender is not ContextMenu menu) return;
        var item = _contextItem;

        foreach (var entry in menu.Items)
        {
            if (entry is not MenuItem menuItem || menuItem.Tag is not string tag) continue;
            menuItem.Header = tag switch
            {
                "pin" => item?.IsPinned == true ? L10n.T("取消固定", "Unpin") : L10n.T("固定到顶部", "Pin to Top"),
                "delete" => L10n.T("删除", "Delete"),
                _ => menuItem.Header,
            };
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var count = _store.Items.Count;
        if (count == 0 && e.Key != Key.Escape) return;

        // 1-9 / Ctrl+1-9 / 小键盘 1-9：直接粘贴第 N 条
        var digit = e.Key switch
        {
            >= Key.D1 and <= Key.D9 => e.Key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad1,
            _ => -1,
        };
        if (digit >= 0)
        {
            if (digit < count)
            {
                HistoryList.SelectedIndex = digit;
                PasteSelected();
            }
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;

            case Key.Enter:
                PasteSelected();
                e.Handled = true;
                break;

            case Key.Down:
                if (count > 0)
                {
                    HistoryList.SelectedIndex = (HistoryList.SelectedIndex + 1) % count;
                    ScrollSelectionIntoView();
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (count > 0)
                {
                    HistoryList.SelectedIndex = (HistoryList.SelectedIndex - 1 + count) % count;
                    ScrollSelectionIntoView();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                if (HistoryList.SelectedItem is ClipboardItem item)
                {
                    _store.Remove(item);
                    _store.Renumber();
                    HistoryList.SelectedIndex = Math.Min(HistoryList.SelectedIndex, _store.Items.Count - 1);
                    TrashButton.IsEnabled = _store.Items.Count > 0;
                    e.Handled = true;
                }
                break;
        }
    }

    private void ScrollSelectionIntoView()
    {
        if (HistoryList.SelectedItem != null)
            HistoryList.ScrollIntoView(HistoryList.SelectedItem);
    }

    private void PasteSelected()
    {
        if (HistoryList.SelectedItem is not ClipboardItem item) return;

        if (!_monitor.SetItem(item)) return;

        _store.BumpToFront(item);
        _store.Renumber();
        Hide();

        if (_settings.PasteAutomatically)
        {
            // 等前台窗口恢复后再投 ⌘V→Ctrl+V
            Task.Delay(80).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                Paster.Paste();
                if (_settings.PlaySound) Paster.PlayFeedbackSound();
            }));
        }
        else if (_settings.PlaySound)
        {
            Paster.PlayFeedbackSound();
        }
    }

    private void PinMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextItem == null) return;
        _store.TogglePin(_contextItem);
        _store.Renumber();
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextItem == null) return;
        _store.Remove(_contextItem);
        _store.Renumber();
        TrashButton.IsEnabled = _store.Items.Count > 0;
    }

    private void TrashButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.ConfirmBeforeClearing)
        {
            _suppressAutoHide = true;                     // 弹窗期间不要因失焦收起
            var dialog = new ConfirmDialog(
                L10n.T("清空历史", "Clear History"),
                L10n.T("确定要清空全部剪贴板历史吗？", "Clear all clipboard history?"),
                L10n.T("清空", "Clear"),
                L10n.T("取消", "Cancel"),
                L10n.T("不再询问", "Don't ask again"))
            {
                Owner = this,
            };
            var confirmed = dialog.ShowDialog() == true;

            if (dialog.DontAskAgain)
            {
                _settings.ConfirmBeforeClearing = false;
                StorageService.SaveSettings(_settings);
            }
            _suppressAutoHide = false;

            if (!confirmed)
            {
                Activate();                               // 取消后把焦点还给面板
                HistoryList.Focus();
                return;
            }
        }

        _store.ClearAll();
        Hide();                                           // 清空后收起，对齐 macOS
    }
}

internal static class VisualTreeHelpers
{
    public static T? FindAncestor<T>(this DependencyObject? node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T typed) return typed;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }
}
