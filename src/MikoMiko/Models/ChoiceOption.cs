using CommunityToolkit.Mvvm.ComponentModel;

namespace MikoMiko.Models;

/// <summary>设置项中的单选项（主题 / 语言 / 字体…）。</summary>
public sealed partial class ChoiceOption : ObservableObject
{
    public ChoiceOption(string key, string label, string icon = "")
    {
        Key = key;
        Label = label;
        Icon = icon;
    }

    public string Key { get; }

    /// <summary>可选的线性小图标字符，用于影视分类栏等紧凑导航。</summary>
    public string Icon { get; }

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private bool _isSelected;

    public override string ToString() => Label;
}
