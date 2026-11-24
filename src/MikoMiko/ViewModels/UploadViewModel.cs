using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>发布板块（米游社 newArticle 通道）。</summary>
public sealed class UploadSection
{
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Accent { get; init; } = "#4EA4DD";

    public string UrlFor(string kind) => $"https://www.miyoushe.com/{Slug}/newArticle/0/{kind}";
}

/// <summary>发布内容类型：长文 / 图片 / 视频。</summary>
public sealed class UploadContentType
{
    public string Name { get; init; } = string.Empty;
    public string Hint { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Badge { get; init; } = string.Empty;
    public string Accent { get; init; } = "#4EA4DD";
}

/// <summary>上传内容弹层：第一步选板块，第二步选发布类型。</summary>
public sealed partial class UploadViewModel : ObservableObject
{
    private readonly Action<string> _openWeb;
    public Action? CloseRequested;

    public UploadViewModel(Action<string> openWeb)
    {
        _openWeb = openWeb;
    }

    public ObservableCollection<UploadSection> Sections { get; } = new(new[]
    {
        new UploadSection { Name = "大别墅", Slug = "dby", Accent = "#5FA8F6" },
        new UploadSection { Name = "崩坏学园2", Slug = "bh2", Accent = "#F08A4B" },
        new UploadSection { Name = "崩坏3", Slug = "bh3", Accent = "#4EA4DD" },
        new UploadSection { Name = "未定事件簿", Slug = "wd", Accent = "#C56C87" },
        new UploadSection { Name = "原神", Slug = "ys", Accent = "#6AA9D8" },
        new UploadSection { Name = "崩坏：星穹铁道", Slug = "sr", Accent = "#8A83D6" },
        new UploadSection { Name = "绝区零", Slug = "zzz", Accent = "#D2A53A" },
        new UploadSection { Name = "星布谷地", Slug = "planet", Accent = "#63B68B" },
        new UploadSection { Name = "崩坏：因缘精灵", Slug = "hna", Accent = "#8E7BC4" },
    });

    public ObservableCollection<UploadContentType> ContentTypes { get; } = new(new[]
    {
        new UploadContentType { Name = "发布长文", Hint = "攻略、考据、同人文字", Kind = "1", Badge = "文", Accent = "#4EA4DD" },
        new UploadContentType { Name = "发布图片", Hint = "插画、截图、同人图", Kind = "2", Badge = "图", Accent = "#F08FB0" },
        new UploadContentType { Name = "发布视频", Hint = "实况、剪辑、短片", Kind = "5", Badge = "影", Accent = "#7A8FD8" },
    });

    [ObservableProperty] private UploadSection? _selectedSection;
    [ObservableProperty] private bool _isChoosingType;

    /// <summary>每次重新打开都从板块选择开始。</summary>
    public void Reset()
    {
        IsChoosingType = false;
        SelectedSection = null;
    }

    /// <summary>第一步：选中板块，进入发布类型。</summary>
    [RelayCommand]
    private void ChooseSection(UploadSection? section)
    {
        if (section is null) return;
        SelectedSection = section;
        IsChoosingType = true;
    }

    /// <summary>返回板块选择。</summary>
    [RelayCommand]
    private void Back()
    {
        IsChoosingType = false;
        SelectedSection = null;
    }

    /// <summary>第二步：打开对应板块的米游社发布页。</summary>
    [RelayCommand]
    private void Publish(UploadContentType? type)
    {
        if (type is null || SelectedSection is null) return;
        var url = SelectedSection.UrlFor(type.Kind);
        CloseRequested?.Invoke();
        _openWeb(url);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}
