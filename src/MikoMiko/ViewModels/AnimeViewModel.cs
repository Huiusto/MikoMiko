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
    private readonly Action<string> _openPlayer;
    private List<AnimeItem> _homeItems = new();
    private List<AnimeItem> _baseItems = new();
    private CancellationTokenSource? _loadCts;

    public AnimeViewModel(AnimeModuleService module, AnimeCatalogService catalog, Action<string> openPlayer)
    {
        _module = module;
        _catalog = catalog;
        _openPlayer = openPlayer;

        Categories.Add(new ChoiceOption("home", "首页"));
        Categories.Add(new ChoiceOption("cn", "国产"));
        Categories.Add(new ChoiceOption("jp", "日韩"));
        Categories.Add(new ChoiceOption("us", "欧美"));
        Categories.Add(new ChoiceOption("anime", "动画"));
        Categories.Add(new ChoiceOption("hk", "港台"));
        Categories.Add(new ChoiceOption("oversea", "海外"));
        Categories.Add(new ChoiceOption("all", "综合"));
        Categories.Add(new ChoiceOption("update", "今日更新"));
        Categories.Add(new ChoiceOption("hot", "热榜"));
        Categories.Add(new ChoiceOption("news", "资讯"));
        Categories[0].IsSelected = true;
    }

    public ObservableCollection<AnimeItem> Items { get; } = new();
    public ObservableCollection<AnimeItem> Featured { get; } = new();
    public ObservableCollection<AnimeProvider> Providers { get; } = new();
    public ObservableCollection<ChoiceOption> Categories { get; } = new();

    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private bool _isMorePage;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private AnimeItem? _hero;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _sectionTitle = "猜你喜欢";
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _countText = string.Empty;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string _downloadPercentText = string.Empty;
    [ObservableProperty] private string _heroTitle = "追番 / 影视";
    [ObservableProperty] private string _heroMeta = "樱花动漫、樱花TV、PIDEC 等九个站点，全部在软件内播放";

    private string _categoryKey = "home";

    public string ModuleVersion => _module.Manifest?.Version ?? "1.0.0";
    public int ProviderCount => Providers.Count;

    public async Task InitializeAsync(bool refresh = false)
    {
        IsInstalled = _module.TryLoad();
        if (!IsInstalled) return;
        if (Providers.Count == 0) LoadProviders();
        if (refresh || Items.Count == 0) await LoadCatalogAsync();
    }

    private void LoadProviders()
    {
        Providers.Clear();
        foreach (var provider in _module.Providers)
        {
            if (!string.IsNullOrWhiteSpace(provider.Name)) Providers.Add(provider);
        }
    }

    public async Task LoadCatalogAsync()
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
            IsSearching = false;
            IsMorePage = false;
            _categoryKey = "home";
            SelectCategoryItem("home");
            SectionTitle = "猜你喜欢";
            BuildFeatured();
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
            _homeItems = new List<AnimeItem>();
            _baseItems = _homeItems;
            BuildFeatured();
            ApplyFilter();
            StatusText = "数据源连接失败，请稍后重试。";
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsLoading = false;
        }
    }

    private void BuildFeatured()
    {
        Featured.Clear();
        foreach (var item in _homeItems.Take(7)) Featured.Add(item);
        var hero = _homeItems.FirstOrDefault();
        Hero = hero;
        HeroTitle = hero?.Title ?? "追番 / 影视";
        HeroMeta = hero is null ? "樱花动漫、樱花TV、PIDEC 等九个站点，全部在软件内播放" : $"{hero.Source} · {hero.Note}";
    }

    private void ApplyFilter()
    {
        IEnumerable<AnimeItem> query = _baseItems;
        if (_categoryKey == "cn") query = query.Where(i => i.Region == "国产");
        else if (_categoryKey == "jp") query = query.Where(i => i.Region == "日本");
        else if (_categoryKey == "us") query = query.Where(i => i.Region is "美国" or "欧美");
        else if (_categoryKey == "anime") query = query.Where(i => i.Kind == "动漫");
        else if (_categoryKey == "hk") query = query.Where(i => i.Region == "港台");
        else if (_categoryKey == "oversea") query = query.Where(i => i.Region is "其他" or "海外");

        if (_categoryKey == "update")
            query = query.OrderByDescending(i => LooksRecentlyUpdated(i.Note));
        else if (_categoryKey == "hot")
            query = query.OrderByDescending(i => EpisodeNumber(i.Note));

        var list = query.Take(180).ToList();
        Items.Clear();
        foreach (var item in list) Items.Add(item);
        IsEmpty = list.Count == 0;
        CountText = $"共 {list.Count} 部";
    }

    private static bool LooksRecentlyUpdated(string note)
        => note.Contains("更新", StringComparison.Ordinal) || note.Contains("连载", StringComparison.Ordinal);

    private static int EpisodeNumber(string note)
    {
        var match = Regex.Match(note ?? string.Empty, @"(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 0;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!IsInstalled || IsLoading) return;
        await LoadCatalogAsync();
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
        _module.Uninstall();
        IsInstalled = false;
        IsMorePage = false;
        IsLoading = false;
        IsSearching = false;
        _homeItems = new List<AnimeItem>();
        _baseItems = new List<AnimeItem>();
        Items.Clear();
        Featured.Clear();
        Providers.Clear();
        Hero = null;
        SearchText = string.Empty;
        StatusText = string.Empty;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var keyword = SearchText.Trim();
        if (!IsInstalled) return;
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
        SectionTitle = $"“{keyword}” 的搜索结果";
        StatusText = "正在搜索…";
        try
        {
            var remote = await _catalog.SearchAsync(_module.Providers, keyword, cts.Token);
            if (cts.IsCancellationRequested) return;
            var local = _homeItems.Where(i => i.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
            var merged = remote.Concat(local)
                .GroupBy(i => i.Source + "|" + i.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            _baseItems = merged;
            _categoryKey = "all";
            ApplyFilter();
            StatusText = merged.Count == 0 ? "没有找到相关作品，换个关键词试试。" : string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (cts.IsCancellationRequested) return;
            var local = _homeItems.Where(i => i.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
            _baseItems = local;
            ApplyFilter();
            StatusText = local.Count == 0 ? "搜索失败，请稍后重试。" : string.Empty;
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        ShowHome();
    }

    [RelayCommand]
    private void ShowMore()
    {
        if (!IsInstalled) return;
        IsSearching = false;
        IsMorePage = true;
        _baseItems = _homeItems;
        _categoryKey = "all";
        SelectCategoryItem("all");
        SectionTitle = "全部内容";
        ApplyFilter();
    }

    [RelayCommand]
    private void ShowHome()
    {
        if (!IsInstalled) return;
        SearchText = string.Empty;
        IsSearching = false;
        IsMorePage = false;
        _baseItems = _homeItems;
        _categoryKey = "home";
        SelectCategoryItem("home");
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
    private void OpenItem(AnimeItem? item)
    {
        if (item is null) return;
        var target = string.IsNullOrWhiteSpace(item.PlayUrl) ? item.Url : item.PlayUrl;
        if (string.IsNullOrWhiteSpace(target)) return;
        _openPlayer(target);
    }

    private void SelectCategoryItem(string key)
    {
        foreach (var item in Categories) item.IsSelected = item.Key == key;
    }

    /// <summary>开发期截图前复位筛选，避免把鼠标操作带进验收图。</summary>
    public void PrepareForCapture(bool showMore)
    {
        SearchText = string.Empty;
        IsSearching = false;
        IsMorePage = showMore;
        _categoryKey = showMore ? "all" : "home";
        SelectCategoryItem(_categoryKey);
        SectionTitle = showMore ? "全部内容" : "猜你喜欢";
        _baseItems = _homeItems;
        ApplyFilter();
    }
}
