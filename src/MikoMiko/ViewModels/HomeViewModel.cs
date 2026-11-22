using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>首页：全站随机帖子流，向下滑动不断加载更多。</summary>
public sealed partial class HomeViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly ContentService _content;
    private readonly Action<FeedItem> _openItem;
    private readonly HashSet<string> _seen = new();
    private readonly List<FeedItem> _feed = new();

    public HomeViewModel(ContentService content, Action<FeedItem> openItem)
    {
        _content = content;
        _openItem = openItem;
    }

    public ObservableCollection<FeedItem> Items { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private bool _isLatest;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _filter = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasMore = true;
    [ObservableProperty] private bool _isSearchMode;
    [ObservableProperty] private string _searchKeyword = string.Empty;

    public async Task LoadAsync(bool latest)
    {
        IsLatest = latest;
        IsSearchMode = false;
        IsLoading = true;
        StatusText = Localization.Loc.Instance["Common.Loading"];
        try
        {
            _seen.Clear();
            _feed.Clear();
            var items = await _content.GetRandomPostsAsync(PageSize, _seen);
            IsDemo = _content.IsDemo;
            foreach (var item in items)
            {
                if (_seen.Add(item.PostId)) _feed.Add(item);
            }

            Items.Clear();
            foreach (var item in _feed) Items.Add(item);
            IsEmpty = Items.Count == 0;
        }
        catch
        {
            StatusText = Localization.Loc.Instance["Common.Fail"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>首页搜索：直接请求米游社官方全站搜索，结果覆盖所有板块。</summary>
    public async Task SearchAsync(string keyword)
    {
        var value = (keyword ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(value)) return;

        IsSearchMode = true;
        SearchKeyword = value;
        IsLoading = true;
        StatusText = "正在米游社全站搜索…";
        try
        {
            var results = await _content.SearchPostsAsync(value, 30);
            Items.Clear();
            foreach (var item in results) Items.Add(item);
            IsEmpty = Items.Count == 0;
            StatusText = Items.Count == 0 ? "没有找到相关内容" : $"米游社搜索结果 · {Items.Count} 条";
            HasMore = false;
        }
        catch
        {
            Items.Clear();
            IsEmpty = true;
            StatusText = "搜索失败，请稍后重试";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>清空搜索，回到原来的随机帖子流。</summary>
    public void ExitSearch()
    {
        if (!IsSearchMode) return;
        IsSearchMode = false;
        SearchKeyword = string.Empty;
        StatusText = string.Empty;
        Items.Clear();
        foreach (var item in _feed) Items.Add(item);
        IsEmpty = Items.Count == 0;
        HasMore = true;
    }

    /// <summary>下滑到底时追加一批随机帖子。</summary>
    public async Task LoadMoreAsync()
    {
        if (IsLoading || IsLoadingMore || !HasMore || IsSearchMode) return;

        IsLoadingMore = true;
        try
        {
            var items = await _content.GetRandomPostsAsync(12, _seen);
            var added = 0;
            foreach (var item in items)
            {
                if (_seen.Add(item.PostId))
                {
                    _feed.Add(item);
                    Items.Add(item);
                    added++;
                }
            }

            // 连续几批都拿不到新内容就停止请求
            HasMore = added > 0;
        }
        catch
        {
            HasMore = false;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync(IsLatest);

    [RelayCommand]
    private void Open(FeedItem? item)
    {
        if (item is not null) _openItem(item);
    }
}
