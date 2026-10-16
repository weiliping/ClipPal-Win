using System.IO;
using System.Text.Json;
using ClipPal.Models;

namespace ClipPal.Services;

/// <summary>应用设置（对标 macOS 版 AppSettings）。</summary>
public class AppSettings
{
    public int HistoryLimit { get; set; } = 50;

    /// <summary>默认 Ctrl+Alt+V。
    /// 不用 Ctrl+Shift+V —— RegisterHotKey 是全局独占的，会抢掉浏览器/编辑器里
    /// 无处不在的「粘贴为纯文本」。设置界面里可以改。</summary>
    public uint HotKeyModifiers { get; set; } = 0x0002 | 0x0001; // Ctrl + Alt
    public uint HotKeyVirtualKey { get; set; } = 0x56;          // V

    public bool PasteAutomatically { get; set; } = true;
    public bool PlaySound { get; set; }
    public bool IgnoreConcealed { get; set; } = true;
    public bool LaunchAtLogin { get; set; }
    public bool ConfirmBeforeClearing { get; set; } = true;

    /// <summary>面板整块背景的不透明度（1 = 完全不透明）。</summary>
    public double PanelOpacity { get; set; } = 0.96;

    /// <summary>"system" | "zh" | "en"</summary>
    public string AppLanguage { get; set; } = "system";
}

/// <summary>
/// 本地持久化：%AppData%\ClipPal\{history,settings}.json。
/// 写入用「临时文件 + 替换」，避免写一半崩溃把文件写坏。
/// </summary>
public static class StorageService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipPal");

    private static readonly string HistoryFile = Path.Combine(AppDataDir, "history.json");
    private static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string StorageFolder => AppDataDir;
    public static string HistoryPath => HistoryFile;

    public static List<ClipboardItem> LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return new List<ClipboardItem>();
            return JsonSerializer.Deserialize<List<ClipboardItem>>(File.ReadAllText(HistoryFile), JsonOpts)
                   ?? new List<ClipboardItem>();
        }
        catch
        {
            return new List<ClipboardItem>();
        }
    }

    public static void SaveHistory(IEnumerable<ClipboardItem> items)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            WriteAtomic(HistoryFile, JsonSerializer.Serialize(items.ToList(), JsonOpts));
        }
        catch
        {
            // 落盘失败不影响使用
        }
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile), JsonOpts)
                   ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            WriteAtomic(SettingsFile, JsonSerializer.Serialize(settings, JsonOpts));
        }
        catch { }
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        if (File.Exists(path))
        {
            try { File.Replace(tmp, path, null); return; }
            catch { File.Delete(path); }
        }
        File.Move(tmp, path, overwrite: true);
    }
}
