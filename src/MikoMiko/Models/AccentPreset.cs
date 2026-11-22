using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace MikoMiko.Models;

/// <summary>页面配色预设（对应设置页「页面配色」的十个色板）。</summary>
public sealed partial class AccentPreset : ObservableObject
{
    public AccentPreset(string key, string label, string hex)
    {
        Key = key;
        Label = label;
        Hex = hex;
    }

    public string Key { get; }
    public string Label { get; }
    public string Hex { get; }

    [ObservableProperty] private bool _isSelected;

    public Color Color => (Color)System.Windows.Media.ColorConverter.ConvertFromString(Hex);

    public Brush Swatch => new SolidColorBrush(Color);
}
