using CommunityToolkit.Mvvm.ComponentModel;

namespace MikoMiko.Models;

/// <summary>顶部标签（推荐 / 最新）。</summary>
public sealed partial class TabOption : ObservableObject
{
    public TabOption(string key, string titleKey)
    {
        Key = key;
        TitleKey = titleKey;
        Title = Localization.Loc.Instance[titleKey];
    }

    public string Key { get; }
    public string TitleKey { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isSelected;

    public void RefreshTitle() => Title = Localization.Loc.Instance[TitleKey];
}
