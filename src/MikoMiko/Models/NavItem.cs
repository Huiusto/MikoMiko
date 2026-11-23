using CommunityToolkit.Mvvm.ComponentModel;
using MikoMiko.Localization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MikoMiko.Models;

/// <summary>左侧导航条目。</summary>
public sealed partial class NavItem : ObservableObject
{
    public NavItem(string key, string titleKey, string iconName)
    {
        Key = key;
        TitleKey = titleKey;
        IconDefault = LoadIcon($"{iconName}-default.png");
        IconSelected = LoadIcon($"{iconName}-active.png");
        Title = Loc.Instance[titleKey];
    }

    public string Key { get; }
    public string TitleKey { get; }

    /// <summary>默认状态：灰色线稿。</summary>
    public ImageSource IconDefault { get; }

    /// <summary>选中状态：米蓝填充 + 深色描边。</summary>
    public ImageSource IconSelected { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isSelected;

    public void RefreshTitle() => Title = Loc.Instance[TitleKey];

    private static ImageSource LoadIcon(string fileName)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri($"pack://application:,,,/Assets/Nav/{fileName}", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
