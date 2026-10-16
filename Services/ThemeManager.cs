using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace ClipPal.Services;

/// <summary>
/// 跟随 Windows 的「应用模式」浅色/深色。WPF 不会自动跟随，只能自己读注册表并替换画刷。
/// </summary>
public static class ThemeManager
{
    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public static void Apply(System.Windows.Application app)
    {
        var dark = IsDarkMode();
        void Set(string key, string light, string darkColor) =>
            app.Resources[key] = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                    .ConvertFromString(dark ? darkColor : light)!);

        Set("PanelBackgroundBrush", "#F7F7F7", "#2B2B2B");
        Set("PanelBorderBrush",     "#D8D8D8", "#3C3C3C");
        Set("PanelTextBrush",       "#1B1B1B", "#EDEDED");
        Set("PanelSubtleBrush",     "#6B6B6B", "#A0A0A0");
        Set("RowSelectedBrush",     "#0A6CFF", "#0A6CFF");
        Set("RowSelectedTextBrush", "#FFFFFF", "#FFFFFF");
        Set("RowHoverBrush",        "#EDEDED", "#383838");
    }
}
