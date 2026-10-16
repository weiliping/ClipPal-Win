using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using ClipPal.Services;

namespace ClipPal.Models;

public enum ClipboardItemKind
{
    Text,
    FileDrop,
}

public class ClipboardItem : INotifyPropertyChanged
{
    private bool _isPinned;
    private int _number;

    public string Content { get; set; } = string.Empty;
    public ClipboardItemKind Kind { get; set; } = ClipboardItemKind.Text;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public bool IsPinned
    {
        get => _isPinned;
        set { if (_isPinned == value) return; _isPinned = value; OnPropertyChanged(); }
    }

    /// <summary>列表里的 1 基序号（只用于显示，不落盘）。</summary>
    [JsonIgnore]
    public int Number
    {
        get => _number;
        set { if (_number == value) return; _number = value; OnPropertyChanged(); }
    }

    /// <summary>一行显示用的标题：文件显示文件名，文本取第一行并截断。</summary>
    [JsonIgnore]
    public string Title
    {
        get
        {
            if (Kind == ClipboardItemKind.FileDrop)
            {
                var paths = Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (paths.Length == 0) return L10n.T("（空）", "(empty)");
                var name = System.IO.Path.GetFileName(paths[0].TrimEnd('\\', '/'));
                if (string.IsNullOrEmpty(name)) name = paths[0];
                return paths.Length > 1
                    ? $"{name}{L10n.T($" 等 {paths.Length} 个文件", $" and {paths.Length - 1} more")}"
                    : name;
            }

            var first = Content.Replace("\r", string.Empty).Split('\n')[0].Trim();
            if (first.Length == 0) first = L10n.T("（空白文本）", "(blank text)");
            return first.Length > 200 ? first[..200] + "…" : first;
        }
    }

    public ClipboardItem() { }

    public ClipboardItem(string content, ClipboardItemKind kind = ClipboardItemKind.Text)
    {
        Content = content;
        Kind = kind;
        Timestamp = DateTime.Now;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
