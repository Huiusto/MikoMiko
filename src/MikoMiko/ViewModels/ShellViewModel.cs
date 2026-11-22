using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Localization;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>主窗口外壳：导航、页头、页面切换。</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ContentService _content;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;

    private readonly HomeViewModel _home;
    private readonly VideoViewModel _video;

    /// <summary>视频页 ViewModel（截图工具与外部调用用）。</summary>
    public VideoViewModel Video => _video;
    private readonly MomentViewModel _moment;
    private readonly MineViewModel _mine;
    private readonly SettingsViewModel _settingsVm;

    private readonly Dictionary<string, object> _views = new();
    private readonly Stack<string> _history = new();
    private string _currentKey = string.Empty;

    public ShellViewModel(SettingsService settingsService, ContentService content)
    {
        _settingsService = settingsService;
        _settings = settingsService.Current;
        _content = content;

        _home = new HomeViewModel(content, OpenItem);
        _video = new VideoViewModel(content);
        _moment = new MomentViewModel(content);
        _mine = new MineViewModel(content, _settings);
        _settingsVm = new SettingsViewModel(settingsService, content);
        _settingsVm.LanguageChanged = RefreshLocalization;

        Navigation.Add(new NavItem("Home", "Nav.Home", "M3.4 10.2 L12 3.6 L20.6 10.2 M5.6 9.2 V20.4 H18.4 V9.2"));
        Navigation.Add(new NavItem("Video", "Nav.Video", "M3.4 5.2 H20.6 V18.8 H3.4 Z M8.8 8.6 L16.4 12 L8.8 15.4 Z"));
        Navigation.Add(new NavItem("Moment", "Nav.Moment", "M12 3.2 C12.9 7.9 16.1 11.1 20.8 12 C16.1 12.9 12.9 16.1 12 20.8 C11.1 16.1 7.9 12.9 3.2 12 C7.9 11.1 11.1 7.9 12 3.2 Z"));
        Navigation.Add(new NavItem("Mine", "Nav.Mine", "M4.4 7.4 H19.6 V17.6 H4.4 Z M9 4.4 L11.6 7.4 M15 4.4 L12.4 7.4"));
        FooterNavigation.Add(new NavItem("Settings", "Nav.Settings",
            "M12 7.7 A4.3 4.3 0 1 0 12 16.3 A4.3 4.3 0 1 0 12 7.7 M17.5 12 H19.6 M12 6.5 V4.4 M6.5 12 H4.4 M12 17.5 V19.6 M16 8 L17.5 6.5 M8 8 L6.5 6.5 M16 16 L17.5 17.5 M8 16 L6.5 17.5"));

        HomeTabs.Add(new TabOption("Recommend", "Home.Recommend"));
        HomeTabs.Add(new TabOption("Latest", "Home.Latest"));
        HomeTabs[0].IsSelected = true;

        Brand = Loc.Instance["Header.Brand"];
    }

    public ObservableCollection<NavItem> Navigation { get; } = new();
    public ObservableCollection<NavItem> FooterNavigation { get; } = new();
    public ObservableCollection<TabOption> HomeTabs { get; } = new();

    [ObservableProperty] private NavItem? _selectedNav;
    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _isHome = true;
    [ObservableProperty] private string _brand = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary>软件内阅读器（帖子 / 视频帖都在这里打开，不跳页、不弹独立窗口）。</summary>
    [ObservableProperty] private Views.PostReaderView? _readerView;
    [ObservableProperty] private bool _isReaderOpen;
    [ObservableProperty] private Views.VideoView? _videoPage;

    public SettingsViewModel SettingsViewModel => _settingsVm;
    public MineViewModel MineViewModel => _mine;
    public HomeViewModel Home => _home;

    // ---------------------------------------------------------------- 导航

    [RelayCommand]
    private void NavigateTo(NavItem? item)
    {
        if (item is not null) Navigate(item.Key);
    }

    [RelayCommand]
    private void GoBack()
    {
        if (_history.Count == 0) return;
        var previous = _history.Pop();
        Navigate(previous, pushHistory: false);
        CanGoBack = _history.Count > 0;
    }

    [RelayCommand]
    private async Task SelectTab(TabOption? tab)
    {
        if (tab is null) return;
        foreach (var option in HomeTabs) option.IsSelected = option == tab;
        await _home.LoadAsync(tab.Key == "Latest");
        Navigate("Home");
    }

    public void Navigate(string key, bool pushHistory = true)
    {
        if (_currentKey == key)
        {
            if (key == "Home") _ = _home.LoadAsync(_home.IsLatest);
            if (key == "Video") _ = _video.RefreshAsync();
            return;
        }

        // 离开视频页时暂停播放与特效，避免在别的页面继续出声
        if (_currentKey == "Video") VideoPage?.PausePlayback();

        if (!string.IsNullOrEmpty(_currentKey) && pushHistory)
        {
            _history.Push(_currentKey);
            CanGoBack = true;
        }

        _currentKey = key;
        IsHome = key == "Home";

        foreach (var item in Navigation.Concat(FooterNavigation))
        {
            item.IsSelected = item.Key == key;
        }
        SelectedNav = Navigation.Concat(FooterNavigation).FirstOrDefault(i => i.IsSelected);

        if (!_views.TryGetValue(key, out var view))
        {
            view = CreateView(key);
            _views[key] = view;
        }

        CurrentView = view;
        if (view is Views.VideoView videoView) VideoPage = videoView;
        _settings.LastPage = key;
        _ = EnsureLoadedAsync(key);
    }

    private object CreateView(string key) => key switch
    {
        "Video" => new Views.VideoView(_video, _settings),
        "Moment" => new Views.MomentView(_moment),
        "Mine" => new Views.MineView(_mine),
        "Settings" => new Views.SettingsView(_settingsVm),
        _ => new Views.HomeView(_home),
    };

    private async Task EnsureLoadedAsync(string key)
    {
        switch (key)
        {
            case "Home":
                if (_home.Items.Count == 0) await _home.LoadAsync(_home.IsLatest);
                break;
            case "Video":
                if (_video.Videos.Count == 0) await _video.LoadAsync();
                break;
            case "Moment":
                if (_moment.Moments.Count == 0) await _moment.LoadAsync();
                break;
            case "Mine":
                await _mine.LoadAsync();
                break;
            case "Settings":
                _settingsVm.SyncSelections();
                _settingsVm.RefreshCacheSize();
                break;
        }
    }

    public async Task InitializeAsync()
    {
        var start = _settings.StartOnHome || !_settings.RememberLastPage ? "Home" : _settings.LastPage;
        if (Navigation.Concat(FooterNavigation).All(i => i.Key != start)) start = "Home";
        Navigate(start, pushHistory: false);
        await EnsureLoadedAsync(start);
    }

    // ---------------------------------------------------------------- 交互

    partial void OnSearchTextChanged(string value)
    {
        switch (_currentKey)
        {
            case "Home":
                if (string.IsNullOrWhiteSpace(value)) _home.ExitSearch();
                break;
            case "Video":
                _video.ApplySearch(value);
                break;
            case "Moment":
                _moment.ApplySearch(value);
                break;
        }
    }

    /// <summary>回车触发米游社全站搜索（首页）。</summary>
    [RelayCommand]
    public async Task SearchAsync()
    {
        var keyword = SearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyword)) return;
        if (_currentKey != "Home") Navigate("Home");
        await _home.SearchAsync(keyword);
    }

    /// <summary>清空搜索框并回到首页随机流。</summary>
    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
        _home.ExitSearch();
    }

    private void OpenItem(FeedItem item)
    {
        // 从视频页点开帖子时先暂停视频
        VideoPage?.PausePlayback();
        CloseReader();

        var reader = new PostReaderViewModel(_content, _settings, item);
        reader.CloseRequested = CloseReader;
        ReaderView = new Views.PostReaderView(reader);
        IsReaderOpen = true;
        _ = reader.LoadAsync();
    }

    /// <summary>收起阅读器，回到当前页面。</summary>
    [RelayCommand]
    public void CloseReader()
    {
        if (ReaderView is null) return;
        ReaderView = null;
        IsReaderOpen = false;
    }

    /// <summary>开发期截图用：打开首页第一条内容的阅读器。</summary>
    public void OpenReaderForCapture()
    {
        var item = _home.Items.FirstOrDefault();
        if (item is not null) OpenItem(item);
    }

    private void RefreshLocalization()
    {
        Brand = Loc.Instance["Header.Brand"];
        foreach (var item in Navigation.Concat(FooterNavigation)) item.RefreshTitle();
        foreach (var tab in HomeTabs) tab.RefreshTitle();
    }
}
