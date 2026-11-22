using Microsoft.Win32;
using MikoMiko.Models;
using System.Windows;
using System.Windows.Media;

namespace MikoMiko.Services;

/// <summary>浅色 / 深色配色切换，以及「页面配色」预设。</summary>
public static class ThemeService
{
    public static bool IsDark { get; private set; }

    /// <summary>默认主色 #4EA4DD（米蓝）。</summary>
    public const string DefaultAccent = "MikoBlue";

    /// <summary>十个页面配色。</summary>
    public static IReadOnlyList<AccentPreset> Accents { get; } = new List<AccentPreset>
    {
        new("MikoBlue", "米蓝", "#4EA4DD"),
        new("GenshinCyan", "原青", "#38BFB0"),
        new("StarGold", "星金", "#E2B23C"),
        new("ZenlessYellow", "绝黄", "#F2C230"),
        new("Honkai2Orange", "崩2橙", "#FF8A3D"),
        new("Honkai3Blue", "崩3蓝", "#5B8FF9"),
        new("TearsCool", "未定冷", "#7FA8CF"),
        new("LoliPink", "萝莉粉", "#FF8FB5"),
        new("SisterPurple", "御姐紫", "#A277D6"),
        new("HutaoRed", "胡桃红", "#E2553F"),
    };

    public static AccentPreset FindAccent(string key) =>
        Accents.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Accents[0];

    public static void Apply(string theme)
    {
        IsDark = Resolve(theme);

        var app = Application.Current;
        if (app is null) return;

        var palette = new ResourceDictionary
        {
            Source = new Uri(
                IsDark ? "pack://application:,,,/Themes/Palette.Dark.xaml"
                       : "pack://application:,,,/Themes/Palette.Light.xaml",
                UriKind.Absolute)
        };

        var dictionaries = app.Resources.MergedDictionaries;
        if (dictionaries.Count > 0)
        {
            dictionaries[0] = palette;
        }
        else
        {
            dictionaries.Add(palette);
        }
    }

    /// <summary>把主色与派生色写入应用资源，所有 DynamicResource 会立即跟着换色。</summary>
    public static void ApplyAccent(string key)
    {
        var app = Application.Current;
        if (app is null) return;

        var preset = FindAccent(key);
        var color = preset.Color;
        var res = app.Resources;

        res["Brush.Accent"] = new SolidColorBrush(color);
        res["Brush.AccentHover"] = new SolidColorBrush(Mix(color, Colors.White, 0.22));
        res["Brush.AccentSoft"] = new SolidColorBrush(Color.FromArgb(0x18, color.R, color.G, color.B));
        res["Brush.AccentOverlay"] = new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B));
    }

    private static Color Mix(Color a, Color b, double ratio) => Color.FromArgb(
        0xFF,
        (byte)(a.R + (b.R - a.R) * ratio),
        (byte)(a.G + (b.G - a.G) * ratio),
        (byte)(a.B + (b.B - a.B) * ratio));

    private static bool Resolve(string theme)
    {
        if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)) return false;
        return SystemUsesDarkTheme();
    }

    /// <summary>读取系统「应用颜色模式」。</summary>
    public static bool SystemUsesDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i) return i == 0;
        }
        catch
        {
            // 读取失败时使用浅色
        }

        return false;
    }
}
