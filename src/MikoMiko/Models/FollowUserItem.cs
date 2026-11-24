using CommunityToolkit.Mvvm.ComponentModel;

namespace MikoMiko.Models;

/// <summary>关注页左侧导航中的一位关注用户。</summary>
public sealed partial class FollowUserItem : ObservableObject
{
    public string Uid { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Avatar { get; init; } = string.Empty;
    public int PostCount { get; init; }
    public bool HasUpdate { get; init; }

    [ObservableProperty] private bool _isSelected;

    public string PostCountText => PostCount <= 0 ? "暂无动态" : $"{PostCount} 条动态";
}
