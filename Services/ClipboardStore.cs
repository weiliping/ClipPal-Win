using System.Collections.ObjectModel;
using System.Windows.Threading;
using ClipPal.Models;

namespace ClipPal.Services;

/// <summary>
/// 剪贴板历史核心（对标 macOS 版 ClipboardStore）。
///  - 去重：重复内容只提到最前面
///  - LRU：超出上限丢最久未用的；固定项不占名额、不被淘汰
///  - 落盘：改动后 0.4 秒防抖写一次，退出时 SaveNow() 立即写
/// </summary>
public class ClipboardStore
{
    public ObservableCollection<ClipboardItem> Items { get; } = new();

    private readonly DispatcherTimer _saveTimer;
    private AppSettings _settings;

    public ClipboardStore(AppSettings settings)
    {
        _settings = settings;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };

        foreach (var item in StorageService.LoadHistory())
            Items.Add(item);

        TrimToLimit(scheduleSave: false);
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        TrimToLimit();
    }

    public int Limit => Math.Clamp(_settings.HistoryLimit, 5, 500);

    /// <summary>收到一条新的剪贴板内容。</summary>
    public void Add(ClipboardItem incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming.Content)) return;

        var existing = Items.FirstOrDefault(i =>
            i.Kind == incoming.Kind && string.Equals(i.Content, incoming.Content, StringComparison.Ordinal));
        if (existing != null)
        {
            incoming.IsPinned = existing.IsPinned;   // 保留固定状态
            Items.Remove(existing);
        }

        incoming.Timestamp = DateTime.Now;

        // 固定项统一在顶部，新内容插到所有固定项之后
        var index = Items.Count(i => i.IsPinned);
        Items.Insert(index, incoming);

        TrimToLimit();
        Renumber();
    }

    /// <summary>LRU 淘汰：固定项保留，未固定的最多 Limit 条，按时新排序。</summary>
    public void TrimToLimit(bool scheduleSave = true)
    {
        var pinned = Items.Where(i => i.IsPinned).ToList();
        var unpinned = Items.Where(i => !i.IsPinned)
                            .OrderByDescending(i => i.Timestamp)
                            .ToList();

        if (unpinned.Count <= Limit) return;

        Items.Clear();
        foreach (var p in pinned) Items.Add(p);
        foreach (var u in unpinned.Take(Limit)) Items.Add(u);
        Renumber();
        if (scheduleSave) ScheduleSave();
    }

    /// <summary>右键「固定 / 取消固定」。</summary>
    public void TogglePin(ClipboardItem item)
    {
        item.IsPinned = !item.IsPinned;
        item.Timestamp = DateTime.Now;

        var ordered = Items.OrderByDescending(i => i.IsPinned)
                           .ThenByDescending(i => i.Timestamp)
                           .ToList();
        Items.Clear();
        foreach (var i in ordered) Items.Add(i);
        Renumber();

        ScheduleSave();
    }

    public void Remove(ClipboardItem item)
    {
        if (!Items.Remove(item)) return;
        Renumber();
        ScheduleSave();
    }

    /// <summary>把某条提到最前（LRU 里的「使用」）。</summary>
    public void BumpToFront(ClipboardItem item)
    {
        item.Timestamp = DateTime.Now;
        if (item.IsPinned) { ScheduleSave(); return; }

        var index = Items.IndexOf(item);
        if (index < 0) return;
        Items.RemoveAt(index);
        Items.Insert(Items.Count(i => i.IsPinned), item);
        Renumber();
        ScheduleSave();
    }

    public void ClearAll()
    {
        if (Items.Count == 0) return;
        Items.Clear();
        ScheduleSave();
    }

    public void ClearUnpinned()
    {
        var pinned = Items.Where(i => i.IsPinned).ToList();
        if (pinned.Count == Items.Count) return;
        Items.Clear();
        foreach (var p in pinned) Items.Add(p);
        Renumber();
        ScheduleSave();
    }

    /// <summary>重排 1 基序号（列表里显示用）。所有改动后都会自动调用。</summary>
    public void Renumber()
    {
        for (var i = 0; i < Items.Count; i++)
            Items[i].Number = i + 1;
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>退出前调用，把还在防抖里的内容立刻写掉。</summary>
    public void SaveNow()
    {
        _saveTimer.Stop();
        StorageService.SaveHistory(Items);
    }
}
