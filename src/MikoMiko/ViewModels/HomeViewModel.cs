using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>首页：随机推荐流 + 米游社「帖子」分区 / 版块 / 排序流。</summary>
public sealed partial class HomeViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly ContentService _content;
    private readonly Action<FeedItem> _openItem;
    private readonly HashSet<string> _seen = new();
    private readonly List<FeedItem> _feed = new();

    private string _forumLastId = string.Empty;
    private bool _forumHasMore;
    private bool _suppressFilterLoad;
    private int _forumRequest;

    public HomeViewModel(ContentService content, Action<FeedItem> openItem)
    {
        _content = content;
        _openItem = openItem;

        SortOptions = new ObservableCollection<ChoiceOption>
        {
            new("1", "默认"),
            new("3", "热门"),
            new("2", "最新发布"),
        };
        SortOptions[0].IsSelected = true;
        SelectedSort = SortOptions[0];
    }

    public ObservableCollection<FeedItem> Items { get; } = new();
    public ObservableCollection<ForumSection> Sections { get; } = new();
    public ObservableCollection<ForumBoard> Boards { get; } = new();
    public ObservableCollection<ChoiceOption> SortOptions { get; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private bool _isLatest;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _filter = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasMore = true;
    [ObservableProperty] private bool _isSearchMode;
    [ObservableProperty] private bool _isOfficialTab;
    [ObservableProperty] private bool _isPostsTab;
    [ObservableProperty] private string _searchKeyword = string.Empty;

    [ObservableProperty] private ForumSection? _selectedSection;
    [ObservableProperty] private ForumBoard? _selectedBoard;
    [ObservableProperty] private ChoiceOption? _selectedSort;
    [ObservableProperty] private bool _isForumReady;

    public bool IsRecommendTab => !IsPostsTab;

    partial void OnIsPostsTabChanged(bool value) => OnPropertyChanged(nameof(IsRecommendTab));

    // ---------------------------------------------------------------- 推荐流

    public async Task LoadAsync(bool latest)
    {
        IsLatest = latest;
        IsOfficialTab = false;
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
            HasMore = true;
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

    // ---------------------------------------------------------------- 「帖子」分区流

    /// <summary>切到「帖子」页：首次进入时拉取官方分区 / 版块目录。</summary>
    public async Task ActivatePostsAsync()
    {
        IsPostsTab = true;
        IsOfficialTab = false;
        IsSearchMode = false;
        if (Sections.Count == 0)
        {
            await LoadForumCatalogAsync();
        }
        else if (Items.Count == 0)
        {
            await LoadForumAsync(reset: true);
        }
    }

    public void DeactivatePosts()
    {
        IsPostsTab = false;
    }

    private async Task LoadForumCatalogAsync()
    {
        IsLoading = true;
        StatusText = "正在读取米游社分区与版块…";
        try
        {
            var sections = await _content.GetForumSectionsAsync();
            _suppressFilterLoad = true;
            Sections.Clear();
            foreach (var section in sections) Sections.Add(section);

            SelectedSection = Sections.FirstOrDefault(s => s.GameId == 5) ?? Sections.FirstOrDefault();
            RefreshBoards();
            SelectedBoard = Boards.FirstOrDefault();
            IsForumReady = Sections.Count > 0 && Boards.Count > 0;
            // 目录初始化期间保持抑制，避免 ComboBox 的 SelectedItem 同步再触发第二次加载。
            await LoadForumAsync(reset: true);
        }
        catch
        {
            _suppressFilterLoad = false;
            IsForumReady = false;
            StatusText = "分区数据加载失败，请点刷新重试";
        }
        finally
        {
            _suppressFilterLoad = false;
            IsLoading = false;
        }
    }

    partial void OnSelectedSectionChanged(ForumSection? value)
    {
        if (_suppressFilterLoad) return;
        RefreshBoards();
        _suppressFilterLoad = true;
        SelectedBoard = Boards.FirstOrDefault();
        _suppressFilterLoad = false;
        _ = LoadForumAsync(reset: true);
    }

    partial void OnSelectedBoardChanged(ForumBoard? value)
    {
        if (_suppressFilterLoad) return;
        _ = LoadForumAsync(reset: true);
    }

    partial void OnSelectedSortChanged(ChoiceOption? value)
    {
        if (_suppressFilterLoad) return;
        _ = LoadForumAsync(reset: true);
    }

    private void RefreshBoards()
    {
        Boards.Clear();
        if (SelectedSection is null) return;
        foreach (var board in SelectedSection.Boards) Boards.Add(board);
    }

    /// <summary>按当前分区 / 版块 / 排序加载帖子；reset=false 时追加下一页。</summary>
    public async Task LoadForumAsync(bool reset)
    {
        if (SelectedSection is null || SelectedBoard is null || SelectedSort is null) return;

        var request = ++_forumRequest;
        if (reset) _forumLastId = string.Empty;
        if (reset) IsLoading = true;
        else IsLoadingMore = true;
        if (reset) StatusText = "正在加载帖子…";

        var section = SelectedSection;
        var board = SelectedBoard;
        var sort = int.TryParse(SelectedSort.Key, out var parsed) ? parsed : 1;
        try
        {
            var feed = await _content.GetForumFeedAsync(section.GameId, board.Id, sort, PageSize,
                reset ? string.Empty : _forumLastId);
            if (request != _forumRequest) return;

            if (reset)
            {
                _seen.Clear();
                _feed.Clear();
                Items.Clear();
            }

            foreach (var item in feed.Items)
            {
                if (_seen.Add(item.PostId))
                {
                    _feed.Add(item);
                    Items.Add(item);
                }
            }

            _forumLastId = feed.LastId;
            _forumHasMore = !feed.IsLast && !string.IsNullOrEmpty(feed.LastId) && feed.Items.Count > 0;
            HasMore = _forumHasMore;
            IsEmpty = Items.Count == 0;
            StatusText = IsEmpty ? $"{board.Name} 暂无帖子" : string.Empty;
        }
        catch
        {
            if (request == _forumRequest) StatusText = "帖子加载失败，请点右下角刷新重试";
        }
        finally
        {
            if (request == _forumRequest)
            {
                IsLoading = false;
                IsLoadingMore = false;
            }
        }
    }

    // ---------------------------------------------------------------- 搜索 / 分页

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

    /// <summary>清空搜索，回到原来的帖子流。</summary>
    public void ExitSearch()
    {
        if (!IsSearchMode) return;
        IsSearchMode = false;
        SearchKeyword = string.Empty;
        Items.Clear();
        foreach (var item in _feed) Items.Add(item);
        IsEmpty = Items.Count == 0;
        HasMore = IsPostsTab ? _forumHasMore : true;
    }

    /// <summary>下滑到底时追加内容：推荐流随机取样，帖子流按 last_id 翻页。</summary>
    public async Task LoadMoreAsync()
    {
        if (IsLoading || IsLoadingMore || !HasMore || IsSearchMode) return;

        if (IsPostsTab)
        {
            await LoadForumAsync(reset: false);
            return;
        }

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
    private Task Refresh()
    {
        if (IsPostsTab) return LoadForumCatalogIfNeededAsync();
        return LoadAsync(IsLatest);
    }

    private Task LoadForumCatalogIfNeededAsync()
    {
        if (Sections.Count == 0) return LoadForumCatalogAsync();
        return LoadForumAsync(reset: true);
    }

    [RelayCommand]
    private void Open(FeedItem? item)
    {
        if (item is not null) _openItem(item);
    }

    /// <summary>兼容旧调用：官方聚合页已替换为「帖子」分区流。</summary>
    public Task LoadOfficialAsync() => ActivatePostsAsync();
}
