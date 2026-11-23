using CommunityToolkit.Mvvm.ComponentModel;

namespace MikoMiko.Models;

/// <summary>设置项中的单选项（主题 / 语言 / 字体…）。</summary>
public sealed partial class ChoiceOption : ObservableObject
{
    public ChoiceOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private bool _isSelected;

    public override string ToString() => Label;
}
