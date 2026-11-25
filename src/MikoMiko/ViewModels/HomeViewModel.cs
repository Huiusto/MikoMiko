using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>首页：随机推荐流（同作者间隔 20 条）+ 米游社「板块」分区 / 版块 / 排序流。</summary>
public sealed partial class HomeViewModel : ObservableObject
{
    private const int PageSize = 20;
    private const int AuthorGap = 20;

    private readonly ContentService _content;
    private readonly Action<FeedItem> _openItem;
    private readonly Action<SearchUser>? _openUser;
    private readonly HashSet<string> _seen = new();
    private readonly List<FeedItem> _feed = new();
    private readonly Dictionary<string, int> _recommendAuthorIndex = new();

    private string _forumLastId = string.Empty;
    private bool _forumHasMore;
    private bool _suppressFilterLoad;
    private int _forumRequest;
    private List<FeedItem> _searchResults = new();
    private List<SearchUser> _searchUserResults = new();

    public HomeViewModel(ContentService content, Action<FeedItem> openItem, Action<SearchUser>? openUser = null)
    {
        _content = content;
        _openItem = openItem;
        _openUser = openUser;

        SortOptions = new ObservableCollection<ChoiceOption>
        {
            new("1", "默认"),
            new("3", "热门"),
            new("2", "最新发布"),
        };
        SortOptions[0].IsSelected = true;
        SelectedSort = SortOptions[0];

        SearchTabs = new ObservableCollection<ChoiceOption>
        {
            new("All", "综合") { IsSelected = true },
            new("Guide", "攻略"),
            new("Video", "视频"),
            new("Wiki", "百科"),
            new("Image", "图片"),
            new("User", "用户"),
            new("Topic", "话题"),
        };
    }

    public ObservableCollection<FeedItem> Items { get; } = new();
    public ObservableCollection<ForumSection> Sections { get; } = new();
    public ObservableCollection<ForumBoard> Boards { get; } = new();
    public ObservableCollection<ChoiceOption> SortOptions { get; }
    public ObservableCollection<ChoiceOption> SearchTabs { get; }
    public ObservableCollection<SearchUser> SearchUsers { get; } = new();

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
    [ObservableProperty] private string _searchTab = "All";
    [ObservableProperty] private bool _isSearchUsersExpanded;

    [ObservableProperty] private ForumSection? _selectedSection;
    [ObservableProperty] private ForumBoard? _selectedBoard;
    [ObservableProperty] private ChoiceOption? _selectedSort;
    [ObservableProperty] private bool _isForumReady;

    public bool IsRecommendTab => !IsPostsTab;
    public bool HasSearchUsers => SearchUsers.Count > 0;
    public bool ShowSearchUsers => IsSearchMode && !string.IsNullOrWhiteSpace(SearchKeyword) && SearchUsers.Count > 0;
    public bool ShowSearchContent => IsSearchMode && SearchTab != "User";
    public bool ShowFeedGrid => !(IsSearchMode && SearchTab == "User");
    public bool CanExpandSearchUsers => !IsSearchUsersExpanded && _searchUserResults.Count > SearchUsers.Count;
    public double SearchUsersMaxHeight => IsSearchUsersExpanded ? 4200 : 224;

    partial void OnIsPostsTabChanged(bool value) => OnPropertyChanged(nameof(IsRecommendTab));

    partial void OnIsSearchUsersExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExpandSearchUsers));
        OnPropertyChanged(nameof(SearchUsersMaxHeight));
    }

    private void FillSearchUsers(bool all)
    {
        SearchUsers.Clear();
        var source = all ? _searchUserResults : _searchUserResults.Take(6);
        foreach (var user in source) SearchUsers.Add(user);
        OnPropertyChanged(nameof(HasSearchUsers));
        OnPropertyChanged(nameof(ShowSearchUsers));
        OnPropertyChanged(nameof(CanExpandSearchUsers));
    }

    /// <summary>「更多用户」：展开显示本次搜索结果里的全部相关用户。</summary>
    [RelayCommand]
    private void ExpandSearchUsers()
    {
        IsSearchUsersExpanded = true;
        FillSearchUsers(all: true);
    }

    partial void OnSearchTabChanged(string value)
    {
        ApplySearchTab();
        OnPropertyChanged(nameof(ShowSearchContent));
        OnPropertyChanged(nameof(ShowFeedGrid));
    }

    // ---------------------------------------------------------------- 推荐流

    /// <summary>推荐流补足：同一作者的帖子之间至少保留 20 条其他帖子。</summary>
    private async Task<int> AppendRecommendAsync(int target)
    {
        var added = 0;
        for (var attempt = 0; attempt < 3 && added < target; attempt++)
        {
            var items = await _content.GetRandomPostsAsync(Math.Max(target * 2, 24), _seen);
            foreach (var item in items)
            {
                if (added >= target) break;
                if (string.IsNullOrEmpty(item.PostId) || _seen.Contains(item.PostId)) continue;

                var author = RecommendAuthorKey(item);
                if (author.Length > 0 &&
                    _recommendAuthorIndex.TryGetValue(author, out var last) &&
                    _feed.Count - last - 1 < AuthorGap)
                {
                    // 这一批里位置太近，先丢弃，避免下一次随机取样又反复命中。
                    _seen.Add(item.PostId);
                    continue;
                }

                _seen.Add(item.PostId);
                _feed.Add(item);
                if (author.Length > 0) _recommendAuthorIndex[author] = _feed.Count - 1;
                added++;
            }
        }

        return added;
    }

    private static string RecommendAuthorKey(FeedItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.AuthorUid)) return "uid:" + item.AuthorUid;
        return string.IsNullOrWhiteSpace(item.Author) ? string.Empty : "name:" + item.Author;
    }

    public async Task LoadAsync(bool latest)
    {
        IsLatest = latest;
        IsOfficialTab = false;
        IsSearchMode = false;
        SearchKeyword = string.Empty;
        IsSearchUsersExpanded = false;
        _searchUserResults.Clear();
        _searchResults.Clear();
        SearchUsers.Clear();
        OnPropertyChanged(nameof(HasSearchUsers));
        OnPropertyChanged(nameof(ShowSearchUsers));
        OnPropertyChanged(nameof(CanExpandSearchUsers));
        IsLoading = true;
        StatusText = Localization.Loc.Instance["Common.Loading"];
        try
        {
            _seen.Clear();
            _feed.Clear();
            _recommendAuthorIndex.Clear();
            await AppendRecommendAsync(PageSize);
            IsDemo = _content.IsDemo;

            Items.Clear();
            foreach (var item in _feed) Items.Add(item);
            IsEmpty = Items.Count == 0;
            HasMore = Items.Count > 0;
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

    // ---------------------------------------------------------------- 「板块」分区流

    /// <summary>切到「板块」页：首次进入拉目录，随后每次点击都强制刷新当前帖子流。</summary>
    public async Task ActivatePostsAsync()
    {
        IsPostsTab = true;
        IsOfficialTab = false;
        IsSearchMode = false;
        SearchUsers.Clear();
        if (Sections.Count == 0)
        {
            await LoadForumCatalogAsync();
        }
        else
        {
            IsForumReady = Sections.Count > 0 && Boards.Count > 0;
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
                _recommendAuthorIndex.Clear();
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
        if (string.IsNullOrEmpty(value))
        {
            ExitSearch();
            return;
        }

        IsSearchMode = true;
        SearchKeyword = value;
        IsSearchUsersExpanded = false;
        IsLoading = true;
        StatusText = "正在米游社搜索用户与内容…";
        try
        {
            var usersTask = _content.Api.SearchUsersAsync(value, 30);
            var resultsTask = _content.SearchPostsAsync(value, 30);
            await Task.WhenAll(usersTask, resultsTask);

            _searchUserResults = usersTask.Result.ToList();
            _searchResults = resultsTask.Result.ToList();
            FillSearchUsers(all: false);
            OnPropertyChanged(nameof(HasSearchUsers));
            OnPropertyChanged(nameof(ShowSearchUsers));

            SearchTab = "All";
            foreach (var tab in SearchTabs) tab.IsSelected = tab.Key == "All";
            ApplySearchTab();
            HasMore = false;
        }
        catch
        {
            Items.Clear();
            SearchUsers.Clear();
            OnPropertyChanged(nameof(HasSearchUsers));
            IsEmpty = true;
            StatusText = "搜索失败，请稍后重试";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectSearchTab(ChoiceOption? tab)
    {
        if (tab is null) return;
        foreach (var item in SearchTabs) item.IsSelected = item.Key == tab.Key;
        SearchTab = tab.Key;
    }

    /// <summary>搜索页分类：综合显示用户+内容，视频/图片按类型过滤，用户只显示用户。</summary>
    private void ApplySearchTab()
    {
        var list = _searchResults;
        if (SearchTab == "Video") list = list.Where(i => i.IsVideo).ToList();
        else if (SearchTab == "Image")
            list = list.Where(i => i.Images.Count > 0 || !string.IsNullOrEmpty(i.Cover)).ToList();
        else if (SearchTab == "User") list = new List<FeedItem>();

        Items.Clear();
        foreach (var item in list) Items.Add(item);
        var showUsers = SearchTab is "All" or "User" && _searchUserResults.Count > 0;
        IsEmpty = SearchTab == "User" ? SearchUsers.Count == 0 : Items.Count == 0;
        if (!showUsers)
        {
            SearchUsers.Clear();
            OnPropertyChanged(nameof(HasSearchUsers));
        }
        else
        {
            FillSearchUsers(IsSearchUsersExpanded);
        }
        OnPropertyChanged(nameof(HasSearchUsers));
        OnPropertyChanged(nameof(ShowSearchUsers));
        OnPropertyChanged(nameof(ShowSearchContent));
        OnPropertyChanged(nameof(ShowFeedGrid));
        StatusText = SearchTab == "User"
            ? $"用户 {_searchUserResults.Count} 位"
            : $"内容 {Items.Count} 条" + (showUsers ? $" · 用户 {_searchUserResults.Count} 位" : string.Empty);
    }

    /// <summary>清空搜索，回到原来的帖子流。0.1.11：即使已经是非搜索态，也会清掉遗留的相关用户。</summary>
    public void ExitSearch()
    {
        var wasSearching = IsSearchMode;
        IsSearchMode = false;
        SearchKeyword = string.Empty;
        IsSearchUsersExpanded = false;
        SearchUsers.Clear();
        _searchUserResults.Clear();
        _searchResults.Clear();
        OnPropertyChanged(nameof(HasSearchUsers));
        OnPropertyChanged(nameof(ShowSearchUsers));
        OnPropertyChanged(nameof(ShowSearchContent));
        OnPropertyChanged(nameof(ShowFeedGrid));
        OnPropertyChanged(nameof(CanExpandSearchUsers));
        if (!wasSearching) return;
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
            var before = _feed.Count;
            await AppendRecommendAsync(12);
            for (var i = before; i < _feed.Count; i++) Items.Add(_feed[i]);

            HasMore = _feed.Count > before;
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

    [RelayCommand]
    private void OpenUser(SearchUser? user)
    {
        if (user is not null) _openUser?.Invoke(user);
    }

    /// <summary>兼容旧调用：官方聚合页已替换为「板块」分区流。</summary>
    public Task LoadOfficialAsync() => ActivatePostsAsync();
}
