using System.Globalization;

namespace ClipPal.Services;

/// <summary>
/// 中英文文案。与 macOS 版一一对应：设置里选「跟随系统 / 中文 / English」。
/// </summary>
public static class L10n
{
    /// <summary>"system" | "zh" | "en"</summary>
    public static string Language { get; set; } = "system";

    public static bool IsChinese => Language switch
    {
        "zh" => true,
        "en" => false,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                 .StartsWith("zh", StringComparison.OrdinalIgnoreCase),
    };

    public static string T(string zh, string en) => IsChinese ? zh : en;
}
