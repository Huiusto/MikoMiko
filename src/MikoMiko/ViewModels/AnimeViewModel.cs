using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace MikoMiko.ViewModels;

/// <summary>追番/影视独立模块：下载引导、影视首页、查看更多筛选与独立搜索。</summary>
public sealed partial class AnimeViewModel : ObservableObject
{
    private readonly AnimeModuleService _module;
    private readonly AnimeCatalogService _catalog;
    private readonly Action<string> _openUrl;
    private List<AnimeItem> _homeItems = new();
    private List<AnimeItem> _baseItems = new();
    private CancellationTokenSource? _loadCts;

    public AnimeViewModel(AnimeModuleService module, AnimeCatalogService catalog, Action<string> openUrl)
    {
        _module = module;
        _catalog = catalog;
        _openUrl = openUrl;

        TypeFilters.Add(new ChoiceOption("all", "全部"));
        TypeFilters.Add(new ChoiceOption("动漫", "动漫"));
        TypeFilters.Add(new ChoiceOption("电影", "电影"));
        TypeFilters.Add(new ChoiceOption("电视剧", "电视剧"));
        TypeFilters.Add(new ChoiceOption("纪录片", "纪录片"));
        TypeFilters.Add(new ChoiceOption("综艺", "综艺"));
        TypeFilters[0].IsSelected = true;

        SortFilters.Add(new ChoiceOption("all", "综合排序"));
        SortFilters.Add(new ChoiceOption("update", "最近更新"));
        SortFilters.Add(new ChoiceOption("hot", "最多播放"));
        SortFilters[0].IsSelected = true;

        RegionFilters.Add(new ChoiceOption("all", "全部地区"));
        RegionFilters.Add(new ChoiceOption("国产", "国产"));
        RegionFilters.Add(new ChoiceOption("日本", "日本"));
        RegionFilters.Add(new ChoiceOption("美国", "美国"));
        RegionFilters.Add(new ChoiceOption("其他", "其他"));
        RegionFilters[0].IsSelected = true;

        // 未下载模块时也展示九个站点入口，下载按钮只负责安装页面与聚合能力。
        foreach (var provider in DefaultProviders)
            PreviewProviders.Add(provider);
    }

    public ObservableCollection<AnimeProvider> PreviewProviders { get; } = new();

    private static readonly AnimeProvider[] DefaultProviders =
    {
        new() { Name = "樱花TV", Home = "https://www.yhdmtv.cc/", Kind = "动漫", Region = "日本" },
        new() { Name = "PIDEC", Home = "https://superpix.com.cn/", Kind = "动漫", Region = "日本" },
        new() { Name = "樱花动漫1", Home = "https://yinghdm.org.cn/", Kind = "动漫", Region = "日本" },
        new() { Name = "樱花动漫2", Home = "https://www.zhuidm.net/", Kind = "动漫", Region = "日本" },
        new() { Name = "南瓜电影", Home = "https://v.vcinema.cn/filtrate", Kind = "电影", Region = "国产" },
        new() { Name = "七猫视频", Home = "https://milace.cn/", Kind = "电影", Region = "国产" },
        new() { Name = "电影先生", Home = "https://silidm.com/", Kind = "电视剧", Region = "国产" },
        new() { Name = "漫漫看", Home = "https://www.manmankan.com/dy2013/dianshiju/dalu/", Kind = "电视剧", Region = "国产" },
        new() { Name = "麻花影视", Home = "https://mh-ys.cc/", Kind = "电影", Region = "国产" },
    };

    public ObservableCollection<AnimeItem> Items { get; } = new();
    public ObservableCollection<AnimeItem> Featured { get; } = new();
    public ObservableCollection<AnimeProvider> Providers { get; } = new();
    public ObservableCollection<ChoiceOption> TypeFilters { get; } = new();
    public ObservableCollection<ChoiceOption> SortFilters { get; } = new();
    public ObservableCollection<ChoiceOption> RegionFilters { get; } = new();
    public ObservableCollection<ChoiceOption> SourceFilters { get; } = new();

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

    private string _typeKey = "all";
    private string _sortKey = "all";
    private string _regionKey = "all";
    private string _sourceKey = "all";

    public string ModuleVersion => _module.Manifest?.Version ?? "1.0.0";
    public int ProviderCount => Providers.Count;

    public async Task InitializeAsync(bool refresh = false)
    {
        IsInstalled = _module.TryLoad();
        if (!IsInstalled) return;
        if (Providers.Count == 0) LoadProviders();
        if (refresh || Items.Count == 0) await LoadCatalogAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!IsInstalled || IsLoading) return;
        await LoadCatalogAsync();
    }

    private void LoadProviders()
    {
        Providers.Clear();
        SourceFilters.Clear();
        SourceFilters.Add(new ChoiceOption("all", "全部来源"));
        SourceFilters[0].IsSelected = true;
        foreach (var provider in _module.Providers)
        {
            if (string.IsNullOrWhiteSpace(provider.Name)) continue;
            Providers.Add(provider);
            SourceFilters.Add(new ChoiceOption(provider.Name, provider.Name));
        }
        _sourceKey = "all";
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
            SectionTitle = "猜你喜欢";
            BuildFeatured();
            ApplyFilter();
            StatusText = items.Count == 0
                ? "数据源暂时没有返回内容，可直接打开下方站点。"
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
            StatusText = "数据源连接失败，可点击下方站点直接访问。";
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
        Hero = _homeItems.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        IEnumerable<AnimeItem> query = _baseItems;
        if (_typeKey != "all") query = query.Where(i => i.Kind == _typeKey);
        if (_regionKey != "all") query = query.Where(i => i.Region == _regionKey);
        if (_sourceKey != "all") query = query.Where(i => i.Source == _sourceKey);

        query = _sortKey switch
        {
            "update" => query.OrderByDescending(i => LooksRecentlyUpdated(i.Note)),
            "hot" => query.OrderByDescending(i => EpisodeNumber(i.Note)),
            _ => query,
        };

        var list = query.Take(160).ToList();
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
    private async Task DownloadModuleAsync()
    {
        if (IsDownloading) return;
        IsDownloading = true;
        DownloadProgress = 0;
        DownloadPercentText = "0%";
        StatusText = "正在获取追番/影视模块…";
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
            StatusText = string.Empty;
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
        SourceFilters.Clear();
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
            ExitSearch();
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
            var items = await _catalog.SearchAsync(_module.Providers, keyword, cts.Token);
            if (cts.IsCancellationRequested) return;
            _baseItems = items;
            ApplyFilter();
            StatusText = items.Count == 0 ? "没有找到相关作品，换个关键词试试。" : string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (cts.IsCancellationRequested) return;
            _baseItems = new List<AnimeItem>();
            ApplyFilter();
            StatusText = "搜索失败，请稍后重试。";
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        await Task.CompletedTask;
        ExitSearch();
    }

    private void ExitSearch()
    {
        if (!IsInstalled) return;
        IsSearching = false;
        IsMorePage = false;
        _baseItems = _homeItems;
        SectionTitle = "猜你喜欢";
        ApplyFilter();
        StatusText = string.Empty;
    }

    [RelayCommand]
    private void ShowMore()
    {
        if (!IsInstalled) return;
        IsSearching = false;
        IsMorePage = true;
        _baseItems = _homeItems;
        SectionTitle = "全部内容";
        ApplyFilter();
    }

    [RelayCommand]
    private void ShowHome()
    {
        SearchText = string.Empty;
        ExitSearch();
    }

    [RelayCommand]
    private void OpenItem(AnimeItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Url)) return;
        _openUrl(item.Url);
    }

    [RelayCommand]
    private void OpenProvider(AnimeProvider? provider)
    {
        if (provider is null || string.IsNullOrWhiteSpace(provider.Home)) return;
        _openUrl(provider.Home);
    }

    [RelayCommand]
    private void SelectType(ChoiceOption? option)
    {
        if (option is null) return;
        _typeKey = option.Key;
        foreach (var item in TypeFilters) item.IsSelected = item == option;
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectSort(ChoiceOption? option)
    {
        if (option is null) return;
        _sortKey = option.Key;
        foreach (var item in SortFilters) item.IsSelected = item == option;
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectRegion(ChoiceOption? option)
    {
        if (option is null) return;
        _regionKey = option.Key;
        foreach (var item in RegionFilters) item.IsSelected = item == option;
        ApplyFilter();
    }

    [RelayCommand]
    private void SelectSource(ChoiceOption? option)
    {
        if (option is null) return;
        _sourceKey = option.Key;
        foreach (var item in SourceFilters) item.IsSelected = item == option;
        ApplyFilter();
    }

    /// <summary>开发期截图前复位筛选，避免把鼠标操作带进验收图。</summary>
    public void PrepareForCapture(bool showMore)
    {
        SearchText = string.Empty;
        _typeKey = "all";
        _sortKey = "all";
        _regionKey = "all";
        _sourceKey = "all";
        foreach (var option in TypeFilters) option.IsSelected = option.Key == "all";
        foreach (var option in SortFilters) option.IsSelected = option.Key == "all";
        foreach (var option in RegionFilters) option.IsSelected = option.Key == "all";
        foreach (var option in SourceFilters) option.IsSelected = option.Key == "all";
        _baseItems = _homeItems;
        IsSearching = false;
        IsMorePage = showMore;
        SectionTitle = showMore ? "全部内容" : "猜你喜欢";
        ApplyFilter();
    }
}
