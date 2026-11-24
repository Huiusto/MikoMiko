using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace MikoMiko.ViewModels;

/// <summary>追番/影视独立模块：下载门控、影视首页、侧栏分类、站内搜索与站内播放。</summary>
public sealed partial class AnimeViewModel : ObservableObject
{
    private readonly AnimeModuleService _module;
    private readonly AnimeCatalogService _catalog;
    private readonly AnimeStreamService _streams;
    private readonly AnimeFollowService _follows;
    private readonly Action<AnimePlayback> _openPlayer;
    private List<AnimeItem> _homeItems = new();
    private List<AnimeItem> _baseItems = new();
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _searchDebounceCts;
    private bool _isOpening;
    private bool _suppressSearch;

    public AnimeViewModel(AnimeModuleService module, AnimeCatalogService catalog, AnimeStreamService streams,
        AnimeFollowService follows, Action<AnimePlayback> openPlayer)
    {
        _module = module;
        _catalog = catalog;
        _streams = streams;
        _follows = follows;
        _openPlayer = openPlayer;

        Categories.Add(new ChoiceOption("home", "首页", "⌂"));
        Categories.Add(new ChoiceOption("cn", "国产", "◈"));
        Categories.Add(new ChoiceOption("jp", "日韩", "❀"));
        Categories.Add(new ChoiceOption("us", "欧美", "◍"));
        Categories.Add(new ChoiceOption("anime", "动画", "▣"));
        Categories.Add(new ChoiceOption("hk", "港台", "◇"));
        Categories.Add(new ChoiceOption("oversea", "海外", "◎"));
        Categories.Add(new ChoiceOption("all", "综合", "⊕"));
        Categories.Add(new ChoiceOption("update", "今日更新", "↻"));
        Categories.Add(new ChoiceOption("hot", "热榜", "♨"));
        Categories.Add(new ChoiceOption("news", "资讯", "▤"));
        Categories[0].IsSelected = true;
        InitFilters();
        _follows.Changed += RefreshWatchlist;
        RefreshWatchlist();
    }

    private void InitFilters()
    {
        foreach (var option in new[]
                 {
                     new ChoiceOption("all", "全部"),
                     new ChoiceOption("anime", "动漫"),
                     new ChoiceOption("movie", "电影"),
                     new ChoiceOption("tv", "电视剧"),
                     new ChoiceOption("short", "短剧"),
                     new ChoiceOption("doc", "纪录片"),
                     new ChoiceOption("variety", "综艺"),
                 })
            TypeFilters.Add(option);
        TypeFilters[0].IsSelected = true;

        foreach (var option in new[]
                 {
                     new ChoiceOption("default", "综合排序"),
                     new ChoiceOption("follow", "最多追番"),
                     new ChoiceOption("play", "最多播放"),
                     new ChoiceOption("update", "最近更新"),
                     new ChoiceOption("rating", "最高评分"),
                 })
            SortFilters.Add(option);
        SortFilters[0].IsSelected = true;

        foreach (var option in new[]
                 {
                     new ChoiceOption("all", "全部地区"),
                     new ChoiceOption("cn", "国产"),
                     new ChoiceOption("jp", "日本"),
                     new ChoiceOption("us", "美国"),
                     new ChoiceOption("other", "其他"),
                 })
            RegionFilters.Add(option);
        RegionFilters[0].IsSelected = true;

        foreach (var option in new[]
                 {
                     new ChoiceOption("all", "全部风格"),
                     new ChoiceOption("hot", "热血"),
                     new ChoiceOption("love", "恋爱"),
                     new ChoiceOption("fantasy", "奇幻"),
                     new ChoiceOption("mystery", "悬疑"),
                     new ChoiceOption("heal", "治愈"),
                     new ChoiceOption("fun", "搞笑"),
                     new ChoiceOption("scifi", "科幻"),
                     new ChoiceOption("wuxia", "武侠"),
                     new ChoiceOption("daily", "日常"),
                     new ChoiceOption("sport", "运动"),
                     new ChoiceOption("music", "音乐"),
                     new ChoiceOption("history", "历史"),
                     new ChoiceOption("adventure", "冒险"),
                     new ChoiceOption("battle", "战斗"),
                 })
            GenreFilters.Add(option);
        GenreFilters[0].IsSelected = true;

        foreach (var option in new[]
                 {
                     new ChoiceOption("all", "全部片库"),
                     new ChoiceOption("movie", "电影"),
                     new ChoiceOption("tv", "电视剧"),
                     new ChoiceOption("short", "短剧"),
                     new ChoiceOption("anime", "动漫"),
                     new ChoiceOption("jp-anime", "日漫"),
                     new ChoiceOption("jp-kr-comic", "日韩漫画"),
                     new ChoiceOption("cn-anime", "国漫"),
                     new ChoiceOption("west-anime", "欧美动漫"),
                     new ChoiceOption("other-anime", "其他动漫"),
                     new ChoiceOption("anime-movie", "动漫电影"),
                     new ChoiceOption("hk-mo-tw-anime", "中国港澳台动漫"),
                     new ChoiceOption("variety", "综艺"),
                 })
            FeedFilters.Add(option);
        FeedFilters[0].IsSelected = true;
    }

    public ObservableCollection<AnimeItem> Items { get; } = new();
    public ObservableCollection<AnimeItem> Watchlist { get; } = new();
    public ObservableCollection<AnimeItem> Featured { get; } = new();
    public ObservableCollection<AnimeProvider> Providers { get; } = new();
    public ObservableCollection<ChoiceOption> Categories { get; } = new();
    public ObservableCollection<ChoiceOption> TypeFilters { get; } = new();
    public ObservableCollection<ChoiceOption> SortFilters { get; } = new();
    public ObservableCollection<ChoiceOption> RegionFilters { get; } = new();
    public ObservableCollection<ChoiceOption> GenreFilters { get; } = new();
    public ObservableCollection<ChoiceOption> FeedFilters { get; } = new();

    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private bool _isMorePage;
    [ObservableProperty] private bool _isHomeMode = true;
    [ObservableProperty] private bool _isWatchlistPage;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isWatchlistEmpty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private AnimeItem? _hero;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _sectionTitle = "猜你喜欢";
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _countText = string.Empty;
    [ObservableProperty] private string _watchlistCountText = string.Empty;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string _downloadPercentText = string.Empty;
    [ObservableProperty] private string _heroTitle = "追番 / 影视";
    [ObservableProperty] private string _heroMeta = "樱花动漫、樱花TV、PIDEC、B 站等十一个站点，片库直连公开列表，全部在软件内播放";

    private string _categoryKey = "home";
    private string _typeKey = "all";
    private string _sortKey = "default";
    private string _regionKey = "all";
    private string _genreKey = "all";
    private string _feedKey = "all";

    public string ModuleVersion => _module.Manifest?.Version ?? "1.0.0";
    public int ProviderCount => Providers.Count;

    public async Task InitializeAsync(bool refresh = false)
    {
        RefreshWatchlist();
        IsInstalled = _module.TryLoad();
        if (!IsInstalled) return;
        if (Providers.Count == 0) LoadProviders();
        if (refresh || Items.Count == 0) await LoadCatalogAsync();
        else if (!IsMorePage && !IsSearching && !IsWatchlistPage) ApplyFilter();
    }

    private void LoadProviders()
    {
        Providers.Clear();
        foreach (var provider in _module.Providers)
        {
            if (!string.IsNullOrWhiteSpace(provider.Name)) Providers.Add(provider);
        }
    }

    public async Task LoadCatalogAsync(bool resetView = true)
    {
        if (!IsInstalled) return;

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoading = true;
        StatusText = "正在连接数据源…";
        try
        {
            var items = await _catalog.LoadHomeAsync(_module.Providers, cts.Token);
            if (cts.IsCancellationRequested) return;
            _homeItems = items;
            _baseItems = items;
            if (resetView)
            {
                IsSearching = false;
                IsMorePage = false;
                IsWatchlistPage = false;
                IsHomeMode = true;
                _categoryKey = "home";
                SelectCategoryItem("home");
                SectionTitle = "猜你喜欢";
            }
            ApplyFilter();
            StatusText = items.Count == 0
                ? "数据源暂时没有返回内容，请稍后刷新。"
                : string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (cts.IsCancellationRequested) return;
            if (resetView) _homeItems = new List<AnimeItem>();
            _baseItems = _homeItems;
            ApplyFilter();
            StatusText = "数据源连接失败，请稍后重试。";
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsLoading = false;
        }
    }

    private void BuildFeatured(IEnumerable<AnimeItem> source)
    {
        Featured.Clear();
        var mixed = source
            .GroupBy(i => i.Source, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(7)
            .ToList();
        foreach (var item in mixed) Featured.Add(item);
    }

    private void ApplyFilter()
    {
        if (IsWatchlistPage) return;
        if (!IsMorePage && !IsSearching)
        {
            // 首页“猜你喜欢”每次进入都从聚合池里换一批，保持内容新鲜感。
            var picks = _homeItems
                .OrderBy(_ => Random.Shared.Next())
                .Take(14)
                .ToList();
            Items.Clear();
            foreach (var item in picks) Items.Add(item);
            IsEmpty = picks.Count == 0;
            CountText = string.Empty;
            BuildFeatured(picks);
            return;
        }

        IEnumerable<AnimeItem> query = _baseItems;

        if (_typeKey == "anime") query = query.Where(i => i.Kind is "动漫" or "动画");
        else if (_typeKey == "movie") query = query.Where(i => i.Kind is "电影" or "影视");
        else if (_typeKey == "tv") query = query.Where(i => i.Kind is "电视剧" or "剧集");
        else if (_typeKey == "doc") query = query.Where(i => i.Kind == "纪录片");
        else if (_typeKey == "variety") query = query.Where(i => i.Kind == "综艺");

        if (_regionKey == "cn") query = query.Where(i => i.Region is "国产" or "中国大陆");
        else if (_regionKey == "jp") query = query.Where(i => i.Region is "日本" or "日韩");
        else if (_regionKey == "us") query = query.Where(i => i.Region is "美国" or "欧美");
        else if (_regionKey == "other") query = query.Where(i => i.Region is "其他" or "海外");

        if (_genreKey != "all")
        {
            var keyword = GenreKeyword(_genreKey);
            if (keyword.Length > 0)
                query = query.Where(i => (i.Title + " " + i.Note).Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        query = _sortKey switch
        {
            "update" => query.OrderByDescending(i => LooksRecentlyUpdated(i.Note)).ThenByDescending(i => EpisodeNumber(i.Note)),
            "play" => query.OrderByDescending(i => EpisodeNumber(i.Note)),
            "follow" => query.OrderByDescending(i => i.Note.Length).ThenByDescending(i => EpisodeNumber(i.Note)),
            "rating" => query.OrderByDescending(i => StableScore(i.Title)),
            _ => query.OrderBy(_ => Random.Shared.Next()),
        };

        var list = query.Take(180).ToList();
        Items.Clear();
        foreach (var item in list) Items.Add(item);
        IsEmpty = list.Count == 0;
        CountText = $"共 {list.Count} 部";
    }

    private static string GenreKeyword(string key) => key switch
    {
        "hot" => "热血",
        "love" => "恋爱",
        "fantasy" => "奇幻",
        "mystery" => "悬疑",
        "heal" => "治愈",
        "fun" => "搞笑",
        "scifi" => "科幻",
        "wuxia" => "武侠",
        "daily" => "日常",
        "sport" => "运动",
        "music" => "音乐",
        "history" => "历史",
        "adventure" => "冒险",
        "battle" => "战斗",
        _ => string.Empty,
    };

    private static int StableScore(string title)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in title ?? string.Empty) hash = hash * 31 + ch;
            return Math.Abs(hash);
        }
    }

    private static bool LooksRecentlyUpdated(string note)
        => note.Contains("更新", StringComparison.Ordinal) || note.Contains("连载", StringComparison.Ordinal);

    private static int EpisodeNumber(string note)
    {
        var match = Regex.Match(note ?? string.Empty, @"(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 0;
    }

    /// <summary>
    /// 页面右下角刷新的入口：不改变当前所在页面，只重新拉取并刷新内容。
    /// 首页换一批猜你喜欢；查看全部保留当前类型/风格等筛选；搜索页重新执行搜索。
    /// </summary>
    [RelayCommand]
    private async Task RefreshCurrentAsync()
    {
        if (!IsInstalled || IsLoading) return;
        if (IsWatchlistPage)
        {
            RefreshWatchlist();
            WatchlistCountText = $"共 {Watchlist.Count} 部";
            return;
        }
        if (IsSearching)
        {
            if (!string.IsNullOrWhiteSpace(SearchText)) await SearchInternalAsync(SearchText.Trim());
            else ShowHome();
            return;
        }

        if (_feedKey != "all")
        {
            var feed = FeedFilters.FirstOrDefault(f => f.Key == _feedKey);
            if (feed is not null)
            {
                await LoadFeedAsync(feed, resetFilters: false);
                return;
            }
        }

        await LoadCatalogAsync(resetView: false);
        if (IsMorePage) SectionTitle = "全部内容";
    }

    /// <summary>首页滚动到底部时继续追加新海报，不必整页重载。</summary>
    public bool LoadMoreHome()
    {
        if (!IsInstalled || IsLoading || IsMorePage || IsSearching || IsWatchlistPage) return false;
        var existing = Items.Select(i => i.Source + "|" + i.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var next = _homeItems
            .Where(i => !existing.Contains(i.Source + "|" + i.Url))
            .OrderBy(_ => Random.Shared.Next())
            .Take(14)
            .ToList();
        if (next.Count == 0) return false;
        foreach (var item in next) Items.Add(item);
        return true;
    }

    [RelayCommand]
    private void ShowWatchlist()
    {
        if (!IsInstalled) return;
        _searchDebounceCts?.Cancel();
        _loadCts?.Cancel();
        IsLoading = false;
        SetSearchTextSilently(string.Empty);
        IsSearching = false;
        IsMorePage = false;
        IsHomeMode = false;
        IsWatchlistPage = true;
        SectionTitle = "我的追剧";
        StatusText = string.Empty;
        RefreshWatchlist();
        WatchlistCountText = $"共 {Watchlist.Count} 部";
    }

    private void RefreshWatchlist()
    {
        Watchlist.Clear();
        foreach (var follow in _follows.Items)
            Watchlist.Add(follow.ToItem());
        IsWatchlistEmpty = Watchlist.Count == 0;
        WatchlistCountText = $"共 {Watchlist.Count} 部";
    }

    [RelayCommand]
    private async Task DownloadModuleAsync()
    {
        if (IsDownloading) return;
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadPercentText = "0%";
        StatusText = string.Empty;
        try
        {
            var progress = new Progress<double>(value =>
            {
                DownloadProgress = Math.Clamp(value * 100, 0, 100);
                DownloadPercentText = $"{DownloadProgress:0}%";
            });
            var ok = await _module.InstallAsync(progress);
            if (!ok) throw new InvalidOperationException("模块安装未完成。");
            IsInstalled = true;
            DownloadPercentText = "100%";
            LoadProviders();
            await LoadCatalogAsync();
        }
        catch
        {
            StatusText = "模块下载失败，请检查网络后重试。";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void UninstallModule()
    {
        _loadCts?.Cancel();
        _searchDebounceCts?.Cancel();
        _module.Uninstall();
        IsInstalled = false;
        IsMorePage = false;
        IsHomeMode = true;
        IsWatchlistPage = false;
        IsLoading = false;
        IsSearching = false;
        _homeItems = new List<AnimeItem>();
        _baseItems = new List<AnimeItem>();
        Items.Clear();
        Featured.Clear();
        Providers.Clear();
        Hero = null;
        SetSearchTextSilently(string.Empty);
        StatusText = string.Empty;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchDebounceCts?.Cancel();
        var keyword = SearchText.Trim();
        if (!IsInstalled) return;
        if (keyword.Length == 0)
        {
            ShowHome();
            return;
        }
        await SearchInternalAsync(keyword);
    }

    /// <summary>输入即搜索：短暂防抖后直接显示结果，不再先展示全部内容。</summary>
    partial void OnSearchTextChanged(string value)
    {
        if (_suppressSearch || !IsInstalled) return;
        _searchDebounceCts?.Cancel();

        var keyword = value.Trim();
        if (keyword.Length == 0)
        {
            ShowHome();
            return;
        }

        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        _ = DebounceSearchAsync(keyword, cts.Token);
    }

    private async Task DebounceSearchAsync(string keyword, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(550, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            await SearchInternalAsync(keyword);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SearchInternalAsync(string keyword)
    {
        if (!IsInstalled) return;
        keyword = keyword.Trim();
        if (keyword.Length == 0)
        {
            ShowHome();
            return;
        }

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoading = true;
        IsSearching = true;
        IsMorePage = true;
        IsHomeMode = false;
        IsWatchlistPage = false;
        SectionTitle = $"“{keyword}” 的搜索结果";
        StatusText = "正在搜索…";
        Items.Clear();
        IsEmpty = false;
        CountText = string.Empty;
        try
        {
            var remote = await _catalog.SearchAsync(_module.Providers, keyword, cts.Token);
            if (cts.IsCancellationRequested) return;
            var merged = remote
                .GroupBy(i => i.Source + "|" + i.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            _baseItems = merged;
            _categoryKey = "all";
            ResetFilters();
            ResetFeedSelection();
            ApplyFilter();
            StatusText = merged.Count == 0
                ? "各站点都没有搜到相关作品，换个关键词试试。"
                : $"已从站点同步 {merged.Count} 部作品";
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (cts.IsCancellationRequested) return;
            _baseItems = new List<AnimeItem>();
            ApplyFilter();
            StatusText = "站点搜索失败，请稍后重试。";
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        _searchDebounceCts?.Cancel();
        SetSearchTextSilently(string.Empty);
        ShowHome();
    }

    private void SetSearchTextSilently(string value)
    {
        _suppressSearch = true;
        SearchText = value;
        _suppressSearch = false;
    }

    [RelayCommand]
    private void ShowMore()
    {
        if (!IsInstalled) return;
        SetSearchTextSilently(string.Empty);
        IsSearching = false;
        IsMorePage = true;
        IsHomeMode = false;
        IsWatchlistPage = false;
        _baseItems = _homeItems;
        _categoryKey = "all";
        SelectCategoryItem("all");
        ResetFilters();
        ResetFeedSelection();
        SectionTitle = "全部内容";
        ApplyFilter();
        StatusText = string.Empty;
    }

    [RelayCommand]
    private void ShowHome()
    {
        if (!IsInstalled) return;
        _searchDebounceCts?.Cancel();
        SetSearchTextSilently(string.Empty);
        IsSearching = false;
        IsMorePage = false;
        IsHomeMode = true;
        IsWatchlistPage = false;
        _baseItems = _homeItems;
        _categoryKey = "home";
        SelectCategoryItem("home");
        ResetFilters();
        ResetFeedSelection();
        SectionTitle = "猜你喜欢";
        ApplyFilter();
        StatusText = string.Empty;
    }

    [RelayCommand]
    private void SelectCategory(ChoiceOption? option)
    {
        if (option is null || !IsInstalled) return;
        _categoryKey = option.Key;
        foreach (var item in Categories) item.IsSelected = item == option;
        IsSearching = false;
        IsWatchlistPage = false;
        IsHomeMode = option.Key == "home";
        SectionTitle = option.Key switch
        {
            "home" => "猜你喜欢",
            "update" => "今日更新",
            "hot" => "热榜",
            "news" => "影视资讯",
            "cn" => "国产精选",
            "jp" => "日韩精选",
            "us" => "欧美精选",
            "anime" => "动画精选",
            "hk" => "港台精选",
            "oversea" => "海外精选",
            _ => "全部内容",
        };
        IsMorePage = option.Key != "home";
        _baseItems = _homeItems;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task SelectFeedAsync(ChoiceOption? option)
    {
        if (option is null) return;
        await LoadFeedAsync(option, resetFilters: true);
    }

    private async Task LoadFeedAsync(ChoiceOption option, bool resetFilters)
    {
        if (!IsInstalled) return;
        _feedKey = option.Key;
        MarkSelected(FeedFilters, option);
        if (option.Key == "all")
        {
            _baseItems = _homeItems;
            ApplyFilter();
            if (IsMorePage && !IsSearching) SectionTitle = "全部内容";
            return;
        }

        _searchDebounceCts?.Cancel();
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoading = true;
        IsSearching = false;
        IsMorePage = true;
        IsHomeMode = false;
        IsWatchlistPage = false;
        SectionTitle = option.Label + " 片库";
        StatusText = $"正在从 {option.Label} 的站点列表同步…";
        CountText = string.Empty;
        Items.Clear();
        if (resetFilters) ResetFilters();
        try
        {
            var category = _module.Manifest?.Categories.FirstOrDefault(c => c.Key == option.Key);
            if (category is null)
            {
                StatusText = "这个分类暂时没有可用的公开列表。";
                _baseItems = new List<AnimeItem>();
            }
            else
            {
                var items = await _catalog.LoadCategoryAsync(category, cts.Token);
                if (cts.IsCancellationRequested) return;
                _baseItems = items;
                StatusText = items.Count == 0
                    ? "站点列表暂未返回可解析内容，稍后刷新重试。"
                    : $"已同步 {items.Count} 部 {option.Label} 作品";
            }
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!cts.IsCancellationRequested) StatusText = "片库同步失败，请检查网络后刷新。";
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectTypeAsync(ChoiceOption? option)
    {
        if (option is null) return;
        _typeKey = option.Key;
        MarkSelected(TypeFilters, option);
        ApplyFilter();
        if (IsMorePage && !IsSearching && _feedKey == "all") await ReloadFilteredPoolAsync();
    }

    [RelayCommand]
    private void SelectSort(ChoiceOption? option)
    {
        if (option is null) return;
        _sortKey = option.Key;
        MarkSelected(SortFilters, option);
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectRegion(ChoiceOption? option)
    {
        if (option is null) return;
        _regionKey = option.Key;
        MarkSelected(RegionFilters, option);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task SelectGenreAsync(ChoiceOption? option)
    {
        if (option is null) return;
        _genreKey = option.Key;
        MarkSelected(GenreFilters, option);
        ApplyFilter();
        if (IsMorePage && !IsSearching) await ReloadFilteredPoolAsync();
    }

    /// <summary>选中类型 / 风格时重新访问一次数据源，而不是只在旧缓存里筛选。</summary>
    private async Task ReloadFilteredPoolAsync()
    {
        if (IsLoading) return;
        await LoadCatalogAsync(resetView: false);
        if (IsMorePage && string.IsNullOrWhiteSpace(SearchText)) SectionTitle = "全部内容";
    }

    private void ResetFilters()
    {
        _typeKey = "all";
        _sortKey = "default";
        _regionKey = "all";
        _genreKey = "all";
        foreach (var item in TypeFilters) item.IsSelected = item.Key == "all";
        foreach (var item in SortFilters) item.IsSelected = item.Key == "default";
        foreach (var item in RegionFilters) item.IsSelected = item.Key == "all";
        foreach (var item in GenreFilters) item.IsSelected = item.Key == "all";
    }

    private void ResetFeedSelection()
    {
        _feedKey = "all";
        foreach (var item in FeedFilters) item.IsSelected = item.Key == "all";
    }

    private static void MarkSelected(IEnumerable<ChoiceOption> options, ChoiceOption selected)
    {
        foreach (var option in options) option.IsSelected = option == selected;
    }

    [RelayCommand]
    private async Task OpenItem(AnimeItem? item)
    {
        if (item is null || _isOpening) return;
        _isOpening = true;
        StatusText = $"正在软件内提取《{item.Title}》的视频…";
        try
        {
            var playback = await ResolveWithFallbackAsync(item);
            if (playback is null || string.IsNullOrWhiteSpace(playback.StreamUrl))
            {
                StatusText = "各站点都暂时无法提取视频，请稍后重试。";
                return;
            }

            StatusText = string.Empty;
            _openPlayer(playback);
        }
        catch
        {
            StatusText = "视频提取失败，请检查网络后重试。";
        }
        finally
        {
            _isOpening = false;
        }
    }

    /// <summary>
    /// 先解析当前站点；若当前站点无法提取，则按标题到其他可播放站点检索，
    /// 找到第一条能出画的来源后自动切站播放。分类片库里的 B 站 / 腾讯入口也会走这条回退链路。
    /// </summary>
    private async Task<AnimePlayback?> ResolveWithFallbackAsync(AnimeItem item)
    {
        var playback = await _streams.ResolveAsync(item);
        if (playback is not null && !string.IsNullOrWhiteSpace(playback.StreamUrl)) return playback;

        var playable = _module.Providers
            .Where(provider => !string.Equals(provider.Name, item.Source, StringComparison.Ordinal))
            .Where(provider => provider.Parser is "module-poster" or "stui" or "manmankan" or "generic" or "mhys")
            .Where(provider => !string.IsNullOrWhiteSpace(provider.Search))
            .ToList();
        if (playable.Count == 0) return null;

        StatusText = $"当前来源「{item.Source}」无法提取，正在自动检索其他站点…";
        var candidates = await _catalog.SearchAsync(playable, item.Title);
        var normalized = NormalizeTitle(item.Title);
        var ranked = candidates
            .Where(candidate => !string.Equals(candidate.Source, item.Source, StringComparison.Ordinal))
            .Where(candidate => !string.Equals(candidate.Source, "哔哩哔哩", StringComparison.Ordinal))
            .OrderBy(candidate => NormalizeTitle(candidate.Title) == normalized ? 0 :
                NormalizeTitle(candidate.Title).Contains(normalized, StringComparison.Ordinal) ? 1 : 2)
            .Take(4)
            .ToList();

        foreach (var candidate in ranked)
        {
            StatusText = $"当前站点无法提取，正在自动切换到「{candidate.Source}」…";
            var fallback = await _streams.ResolveAsync(candidate);
            if (fallback is not null && !string.IsNullOrWhiteSpace(fallback.StreamUrl))
                return fallback;
        }

        return null;
    }

    private static string NormalizeTitle(string title)
        => Regex.Replace(title ?? string.Empty, @"[\s\p{P}\p{S}]+", string.Empty).ToLowerInvariant();

    private void SelectCategoryItem(string key)
    {
        foreach (var item in Categories) item.IsSelected = item.Key == key;
    }

    /// <summary>开发期截图前复位筛选，避免把鼠标操作带进验收图。</summary>
    public void PrepareForCapture(bool showMore)
    {
        SetSearchTextSilently(string.Empty);
        IsSearching = false;
        IsMorePage = showMore;
        IsHomeMode = !showMore;
        IsWatchlistPage = false;
        _categoryKey = showMore ? "all" : "home";
        SelectCategoryItem(_categoryKey);
        ResetFilters();
        ResetFeedSelection();
        SectionTitle = showMore ? "全部内容" : "猜你喜欢";
        _baseItems = _homeItems;
        ApplyFilter();
    }
}
