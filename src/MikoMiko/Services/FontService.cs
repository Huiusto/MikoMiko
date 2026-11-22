using System.Windows;
using System.Windows.Media;

namespace MikoMiko.Services;

/// <summary>界面字体切换。</summary>
public static class FontService
{
    public static readonly (string Name, string Display)[] Available =
    {
        ("Microsoft YaHei UI", "微软雅黑 UI（默认）"),
        ("Segoe UI", "Segoe UI"),
        ("DengXian", "等线"),
        ("HarmonyOS Sans SC", "HarmonyOS Sans"),
        ("Source Han Sans SC", "思源黑体"),
    };

    public static void Apply(string font)
    {
        if (string.IsNullOrWhiteSpace(font)) font = "Microsoft YaHei UI";
        Application.Current.Resources["Font.Ui"] = new FontFamily(font);
    }
}
