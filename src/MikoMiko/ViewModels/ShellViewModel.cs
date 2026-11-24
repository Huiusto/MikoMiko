using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Localization;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Threading;

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
    private readonly UploadViewModel _upload;

    private readonly Dictionary<string, object> _views = new();
    private readonly Stack<string> _history = new();
    private string _currentKey = string.Empty;
    private readonly DispatcherTimer _placeholderTimer;
    private static readonly string[] SearchPlaceholders =
    {
        "搜索你感兴趣的内容", "搜一搜", "MikoMiko", "崩坏学园2", "崩坏3", "未定事件簿", "原神",
        "崩坏：星穹铁道", "绝区零", "星布谷地", "崩坏：因缘精灵", "Varsapura", "源初之结",
        "FlyMe2theMoon", "崩坏学园1", "林离", "鹿鸣", "陆离", "米游社", "人工桌面", "AnuNeko",
        "Whispers from the Star",
    };

    [ObservableProperty] private string _searchPlaceholder = "搜索你感兴趣的内容";

    public ShellViewModel(SettingsService settingsService, ContentService content)
    {
        _settingsService = settingsService;
        _settings = settingsService.Current;
        _content = content;
        // 正文中的链接统一交给软件内浏览器 / 阅读器处理，绝不打开系统浏览器
        Controls.LinkTextBlock.LinkRequested = OpenLink;

        _home = new HomeViewModel(content, OpenItem, user => OpenUserProfile(user.Uid));
        _video = new VideoViewModel(content, OpenUserProfile, _settings);
        _moment = new MomentViewModel(content, OpenMoment);
        _mine = new MineViewModel(content, _settings, OpenItem);
        _settingsVm = new SettingsViewModel(settingsService, content);
        _settingsVm.LanguageChanged = RefreshLocalization;
        _upload = new UploadViewModel(OpenUploadUrl);
        _upload.CloseRequested = CloseUpload;

        SearchPlaceholder = PickSearchPlaceholder();
        _placeholderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _placeholderTimer.Tick += (_, _) => SearchPlaceholder = PickSearchPlaceholder();
        _placeholderTimer.Start();

        Navigation.Add(new NavItem("Home", "Nav.Home", "home"));
        Navigation.Add(new NavItem("Video", "Nav.Video", "video"));
        Navigation.Add(new NavItem("Moment", "Nav.Moment", "following"));
        Navigation.Add(new NavItem("Mine", "Nav.Mine", "mine"));
        FooterNavigation.Add(new NavItem("Upload", "Nav.Upload", "upload"));
        FooterNavigation.Add(new NavItem("Settings", "Nav.Settings", "settings"));

        HomeTabs.Add(new TabOption("Recommend", "Home.Recommend"));
        HomeTabs.Add(new TabOption("Posts", "Home.Posts"));
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

    /// <summary>其他用户的「我的」主页覆盖面（不新开窗口）。</summary>
    [ObservableProperty] private Views.UserProfileView? _profileView;
    [ObservableProperty] private bool _isProfileOpen;

    /// <summary>软件内网页视图（正文里的外部链接），不打开系统浏览器。</summary>
    [ObservableProperty] private Views.BrowserView? _browserView;
    [ObservableProperty] private bool _isBrowserOpen;

    /// <summary>上传内容弹层（不新开窗口，先选板块再选发布类型）。</summary>
    [ObservableProperty] private Views.UploadView? _uploadView;
    [ObservableProperty] private bool _isUploadOpen;

    public SettingsViewModel SettingsViewModel => _settingsVm;
    public UploadViewModel Upload => _upload;
    public MineViewModel MineViewModel => _mine;
    public HomeViewModel Home => _home;

    private string PickSearchPlaceholder()
    {
        var index = Random.Shared.Next(SearchPlaceholders.Length);
        if (SearchPlaceholders[index] == SearchPlaceholder)
            index = (index + 1) % SearchPlaceholders.Length;
        return SearchPlaceholders[index];
    }

    // ---------------------------------------------------------------- 导航

    [RelayCommand]
    private void NavigateTo(NavItem? item)
    {
        if (item is null) return;
        if (item.Key == "Upload")
        {
            OpenUpload();
            return;
        }
        Navigate(item.Key);
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
        // 在推荐 / 帖子之间切换时，任何已打开的帖子阅读器都先自动关闭。
        if (IsReaderOpen) CloseReader();
        CloseProfile();
        foreach (var option in HomeTabs) option.IsSelected = option == tab;
        if (tab.Key == "Posts")
        {
            _home.IsPostsTab = true;
            if (_currentKey == "Home") await _home.ActivatePostsAsync();
            else Navigate("Home");
        }
        else
        {
            _home.DeactivatePosts();
            if (_currentKey == "Home") await _home.LoadAsync(false);
            else Navigate("Home");
        }
    }

    public void Navigate(string key, bool pushHistory = true)
    {
        if (_currentKey == key)
        {
            CloseProfile();
            if (key == "Home")
            {
                if (IsReaderOpen) CloseReader();
                if (_home.IsPostsTab) _ = _home.ActivatePostsAsync();
                else _ = _home.LoadAsync(_home.IsLatest);
            }
            if (key == "Video") _ = _video.RefreshAsync();
            return;
        }

        // 首页打开帖子后切到视频 / 动态等页面时，自动关闭阅读器与浏览器
        if (IsReaderOpen) CloseReader();
        if (IsBrowserOpen) CloseBrowser();
        if (IsUploadOpen) CloseUpload();
        CloseProfile();

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
                if (_home.IsPostsTab) await _home.ActivatePostsAsync();
                else if (_home.Items.Count == 0) await _home.LoadAsync(_home.IsLatest);
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
                _settingsVm.RefreshAccount();
                _settingsVm.RefreshCacheSize();
                break;
        }
    }

    public async Task InitializeAsync()
    {
        var start = _settings.StartOnVideo
            ? "Video"
            : _settings.StartOnHome
                ? "Home"
                : _settings.RememberLastPage
                    ? _settings.LastPage
                    : "Home";
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
        CloseBrowser();
        CloseProfile();

        var reader = new PostReaderViewModel(_content, _settings, item);
        reader.CloseRequested = CloseReader;
        reader.OpenAuthorProfile = OpenUserProfile;
        ReaderView = new Views.PostReaderView(reader);
        IsReaderOpen = true;
        _ = reader.LoadAsync();
    }

    /// <summary>打开其他用户主页（软件内覆盖层，不新开窗口）。</summary>
    public void OpenUserProfile(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return;
        VideoPage?.PausePlayback();
        CloseProfile();
        var vm = new UserProfileViewModel(_content, uid, OpenItem);
        vm.CloseRequested = CloseProfile;
        ProfileView = new Views.UserProfileView(vm);
        IsProfileOpen = true;
        _ = vm.LoadAsync();
    }

    private void OpenMoment(MomentItem moment)
    {
        if (string.IsNullOrWhiteSpace(moment.PostId)) return;
        OpenItem(new FeedItem
        {
            PostId = moment.PostId,
            Id = moment.PostId,
            Title = moment.Text.Length > 60 ? moment.Text[..60] : moment.Text,
            Summary = moment.Text,
            Cover = moment.Images.FirstOrDefault() ?? moment.VideoCover,
            Author = moment.Author,
            AuthorAvatar = moment.Avatar,
            TimeText = moment.TimeText,
            LikeText = moment.LikeText,
            ReplyText = moment.ReplyText,
            IsVideo = moment.IsVideo,
            VideoUrl = string.Empty,
            DurationText = moment.DurationText,
            Images = moment.Images.ToList(),
        });
    }

    /// <summary>正文链接统一入口：米游社帖子在阅读器内打开，其他网页用软件内浏览器。</summary>
    public void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var match = Regex.Match(url, @"(?:article|post)/(\d+)", RegexOptions.IgnoreCase);
        if (url.Contains("miyoushe.com", StringComparison.OrdinalIgnoreCase) && match.Success)
        {
            OpenItem(new FeedItem
            {
                PostId = match.Groups[1].Value,
                Id = match.Groups[1].Value,
                Title = "米游社帖子",
                WebUrl = url,
            });
            return;
        }

        CloseBrowser();
        BrowserView = new Views.BrowserView(url, CloseBrowser);
        IsBrowserOpen = true;
    }

    [RelayCommand]
    public void CloseBrowser()
    {
        if (BrowserView is null) return;
        BrowserView = null;
        IsBrowserOpen = false;
    }

    /// <summary>打开上传内容弹层；再次打开时回到第一步。</summary>
    public void OpenUpload()
    {
        _upload.Reset();
        UploadView ??= new Views.UploadView(_upload);
        IsUploadOpen = true;
    }

    [RelayCommand]
    public void CloseUpload()
    {
        IsUploadOpen = false;
        UploadView = null;
    }

    /// <summary>上传页属于米游社编辑页，直接交给软件内浏览器，避免被帖子正则误判。</summary>
    private void OpenUploadUrl(string url)
    {
        CloseUpload();
        CloseBrowser();
        BrowserView = new Views.BrowserView(url, CloseBrowser);
        IsBrowserOpen = true;
    }

    /// <summary>收起阅读器，回到当前页面。</summary>
    [RelayCommand]
    public void CloseReader()
    {
        if (ReaderView is null) return;
        ReaderView = null;
        IsReaderOpen = false;
    }

    /// <summary>收起用户主页覆盖层。</summary>
    [RelayCommand]
    public void CloseProfile()
    {
        if (ProfileView is null) return;
        ProfileView = null;
        IsProfileOpen = false;
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
