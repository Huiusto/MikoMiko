using System.Windows.Media;

namespace MikoMiko.Models;

/// <summary>绑定的游戏账号（原神 / 星穹铁道 / 绝区零）。</summary>
public sealed class GameAccount
{
    public int GameId { get; set; }
    public string GameBiz { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string LevelLabel { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string ExtraLabel { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
    public string AccentHex { get; set; } = "#FF7DA6";
    public string Badge { get; set; } = string.Empty;

    public Brush Accent => new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(AccentHex));
    public Brush AccentSoft => new SolidColorBrush(Color.FromArgb(28, ((SolidColorBrush)Accent).Color.R,
        ((SolidColorBrush)Accent).Color.G, ((SolidColorBrush)Accent).Color.B));
}
