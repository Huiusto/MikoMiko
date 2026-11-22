using CommunityToolkit.Mvvm.ComponentModel;
using MikoMiko.Localization;
using System.Windows.Media;

namespace MikoMiko.Models;

/// <summary>左侧导航条目。</summary>
public sealed partial class NavItem : ObservableObject
{
    public NavItem(string key, string titleKey, string iconPath)
    {
        Key = key;
        TitleKey = titleKey;
        IconData = Geometry.Parse(iconPath);
        Title = Loc.Instance[titleKey];
    }

    public string Key { get; }
    public string TitleKey { get; }
    public Geometry IconData { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isSelected;

    public void RefreshTitle() => Title = Loc.Instance[TitleKey];
}
