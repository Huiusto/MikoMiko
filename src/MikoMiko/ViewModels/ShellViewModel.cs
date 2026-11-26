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
    private readonly AnimeViewModel _anime;
    private readonly AnimeStreamService _animeStreams = new();
    private readonly BilibiliAnimeService _bilibiliAnime = new();
    private readonly AnimeFollowService _animeFollows;
    private readonly AnimeDanmakuService _animeDanmaku;

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
        _animeFollows = new AnimeFollowService();
        _animeDanmaku = new AnimeDanmakuService();
        _anime = new AnimeViewModel(new AnimeModuleService(), new AnimeCatalogService(), new AnimeStreamService(),
            _animeFollows, ShowAnimePlayer);
        _moment = new MomentViewModel(content, OpenMoment, OpenUserProfile);
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
        Navigation.Add(new NavItem("Launch", "Nav.Launch", "launch"));
        Navigation.Add(new NavItem("Video", "Nav.Video", "video"));
        Navigation.Add(new NavItem("Moment", "Nav.Moment", "following"));
        Navigation.Add(new NavItem("Gacha", "Nav.Gacha", "gacha"));
        Navigation.Add(new NavItem("Daily", "Nav.Daily", "daily"));
        Navigation.Add(new NavItem("Mine", "Nav.Mine", "mine"));
        FooterNavigation.Add(new NavItem("Upload", "Nav.Upload", "upload"));
        FooterNavigation.Add(new NavItem("Settings", "Nav.Settings", "settings"));

        HomeTabs.Add(new TabOption("Recommend", "Home.Recommend"));
        HomeTabs.Add(new TabOption("Posts", "Home.Posts"));
        HomeTabs.Add(new TabOption("Web", "Home.Web"));
        HomeTabs.Add(new TabOption("Anime", "Home.Anime"));
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
    [ObservableProperty] private bool _showGlobalSearch = true;

    [ObservableProperty] private Views.AnimeHubView? _animePage;
    [ObservableProperty] private bool _isAnimePageOpen;

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

    /// <summary>首页「网页」页签：内置浏览器默认打开米游社，并提供推荐网页入口。</summary>
    [ObservableProperty] private Views.WebHubView? _webHubPage;
    [ObservableProperty] private bool _isWebHubOpen;

    /// <summary>追番/影视的软件内视频播放器，只加载解析出的流地址。</summary>
    [ObservableProperty] private Views.AnimePlayerView? _animePlayerView;
    [ObservableProperty] private bool _isAnimePlayerOpen;

    /// <summary>站点网页播放兜底：直接打开播放页，交给站点自己的播放器。</summary>
    [ObservableProperty] private Views.AnimeSitePlayerView? _animeSitePlayerView;
    [ObservableProperty] private bool _isAnimeSitePlayerOpen;

    /// <summary>上传内容弹层（不新开窗口，先选板块再选发布类型）。</summary>
    [ObservableProperty] private Views.UploadView? _uploadView;
    [ObservableProperty] private bool _isUploadOpen;

    public SettingsViewModel SettingsViewModel => _settingsVm;
    public UploadViewModel Upload => _upload;
    public MineViewModel MineViewModel => _mine;
    public HomeViewModel Home => _home;
    public AnimeViewModel Anime => _anime;

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
        PauseAllPlayback();
        if (item.Key == "Upload")
        {
            OpenUpload();
            return;
        }
        Navigate(item.Key);
    }

    /// <summary>最小化 / 切页 / 打开弹层时，把所有正在播放的视频统一暂停。</summary>
    public void PauseAllPlayback()
    {
        VideoPage?.PausePlayback();
        AnimePlayerView?.PausePlayback();
        AnimeSitePlayerView?.PausePlayback();
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
        // 在推荐 / 帖子 / 网页之间切换时，任何已打开的帖子阅读器都先自动关闭。
        if (IsReaderOpen) CloseReader();
        CloseProfile();
        foreach (var option in HomeTabs) option.IsSelected = option == tab;

        if (tab.Key == "Web")
        {
            _home.DeactivatePosts();
            if (_currentKey != "Home")
            {
                Navigate("Home");
                return;
            }
            ShowWebHub();
            return;
        }

        if (tab.Key == "Anime")
        {
            _home.DeactivatePosts();
            CloseWebHub();
            if (_currentKey != "Home")
            {
                Navigate("Home");
                return;
            }
            ShowAnimePage();
            return;
        }

        CloseWebHub();
        CloseAnimePage();
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

    private void ShowWebHub()
    {
        CloseAnimePage();
        WebHubPage ??= new Views.WebHubView();
        IsWebHubOpen = true;
        ShowGlobalSearch = false;
    }

    private void CloseWebHub()
    {
        if (WebHubPage is not null) WebHubPage.PauseMedia();
        IsWebHubOpen = false;
        ShowGlobalSearch = true;
    }

    private void ShowAnimePage()
    {
        // 追剧页是覆盖层。先摘掉底下的页面，避免启动页 / 网页页的 WebView2
        // 这类原生子窗口盖在 WPF 覆盖层上面，造成页面被透明化。
        CurrentView = null;
        AnimePage ??= new Views.AnimeHubView(OpenBilibiliInApp);
        IsAnimePageOpen = true;
        ShowGlobalSearch = false;
    }

    private void CloseAnimePage()
    {
        // 离开追番页时连带关闭播放器与 Flutter 宿主，停止 WebView2 中的请求。
        CloseAnimePlayer();
        CloseAnimeSitePlayer();
        AnimePage?.Shutdown();
        AnimePage = null;
        IsAnimePageOpen = false;
        ShowGlobalSearch = true;
        // 追剧页作为首页页签时，关闭覆盖层后把首页内容恢复出来。
        if (IsHome && _views.TryGetValue("Home", out var homeView)) CurrentView = homeView;
    }

    /// <summary>Flutter 模块点击选集后，先尝试提取直连流；取不到就转站内网页播放。</summary>
    private async void OpenAnimeFromRule(AnimeItem item, bool anime4K)
    {
        if (item is null) return;
        var pageUrl = string.IsNullOrWhiteSpace(item.PlayUrl) ? item.Url : item.PlayUrl;
        if (string.IsNullOrWhiteSpace(pageUrl)) return;

        AnimePage?.ShowToast($"正在打开《{item.Title}》…");
        AnimePlayback? playback = null;
        using var timeoutSource = new CancellationTokenSource();
        try
        {
            var resolve = _animeStreams.ResolveAsync(item, timeoutSource.Token);
            // 部分站点会直接掐断 .NET 请求，不能让用户干等 18 秒，6 秒拿不到就走网页播放。
            var first = await Task.WhenAny(resolve, Task.Delay(TimeSpan.FromSeconds(6)));
            if (first == resolve)
            {
                playback = await resolve;
            }
            else
            {
                timeoutSource.Cancel();
            }
        }
        catch
        {
            playback = null;
        }

        if (playback is not null && !string.IsNullOrWhiteSpace(playback.StreamUrl))
        {
            ShowAnimePlayer(playback, anime4K);
            return;
        }

        ShowAnimeSitePlayer(item, pageUrl);
    }

    /// <summary>直连流播放器报错时，切到同一集的站点网页播放。</summary>
    private void ShowAnimeSiteFromPlayback(AnimePlayback playback, string pageUrl)
    {
        try
        {
            CloseAnimePlayer();
            CloseAnimeSitePlayer();
            AnimePage?.Suspend();
            IsAnimePageOpen = false;
            AnimeSitePlayerView = new Views.AnimeSitePlayerView(
                pageUrl,
                playback.Referer,
                playback.Title,
                playback.Episode,
                playback.Source,
                CloseAnimeSitePlayer);
            IsAnimeSitePlayerOpen = true;
        }
        catch
        {
            AnimePage?.Resume();
            IsAnimePageOpen = true;
        }
    }

    /// <summary>哔哩哔哩番剧/视频：解析官方直连流后在软件内播放，选集、弹幕与评论一并载入。</summary>
    private async void OpenBilibiliInApp(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        AnimePage?.ShowToast($"正在缓冲《{title}》…");
        AnimePlayback? playback = null;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(14));
            playback = await _bilibiliAnime.ResolveAsync(url, title, timeout.Token);
        }
        catch
        {
            playback = null;
        }

        if (playback is null || string.IsNullOrWhiteSpace(playback.StreamUrl))
        {
            // 版权/地区限制拿不到直连流时，改在软件内 WebView2 打开哔哩哔哩官方播放页，仍然不跳出软件。
            AnimePage?.ShowToast("直连取流失败，已切换为软件内网页播放");
            ShowAnimeSitePlayer(new AnimeItem
            {
                Title = title,
                Url = url,
                PlayUrl = url,
                Source = "哔哩哔哩",
                Note = "软件内网页播放",
                Kind = "番剧",
            }, url);
            return;
        }
        ShowAnimePlayer(playback);
        AnimePage?.ShowToast($"已在软件内缓冲《{playback.Title}》");
    }

    /// <summary>直连流不可用时，用软件内 WebView2 打开站点播放页。</summary>
    private void ShowAnimeSitePlayer(AnimeItem item, string pageUrl)
    {
        if (string.IsNullOrWhiteSpace(pageUrl)) return;
        try
        {
            CloseAnimePlayer();
            CloseAnimeSitePlayer();
            AnimePage?.Suspend();
            IsAnimePageOpen = false;
            AnimeSitePlayerView = new Views.AnimeSitePlayerView(
                pageUrl,
                item.Url,
                item.Title,
                item.Note,
                item.Source,
                CloseAnimeSitePlayer);
            IsAnimeSitePlayerOpen = true;
        }
        catch (Exception ex)
        {
            AnimePage?.Resume();
            IsAnimePageOpen = true;
            AnimePage?.ShowToast("网页播放器打开失败：" + ex.Message);
        }
    }

    public void Navigate(string key, bool pushHistory = true)
    {
        PauseAllPlayback();

        // 追剧 / 影视已回到首页页签，位于「网页」右侧；旧入口"Anime"统一映射到首页该页签。
        if (key == "Anime")
        {
            foreach (var option in HomeTabs) option.IsSelected = option.Key == "Anime";
            if (_currentKey == "Home")
            {
                CloseProfile();
                CloseWebHub();
                ShowAnimePage();
                _settings.LastPage = "Home";
                return;
            }
            Navigate("Home", pushHistory);
            return;
        }

        if (_currentKey == key)
        {
            CloseProfile();
            if (key == "Home")
            {
                if (IsReaderOpen) CloseReader();
                var tab = HomeTabs.FirstOrDefault(t => t.IsSelected)?.Key;
                if (tab == "Web")
                {
                    ShowWebHub();
                }
                else if (tab == "Anime")
                {
                    CloseWebHub();
                    ShowAnimePage();
                }
                else
                {
                    CloseWebHub();
                    CloseAnimePage();
                    if (_home.IsPostsTab) _ = _home.ActivatePostsAsync();
                    else _ = _home.LoadAsync(_home.IsLatest);
                }
            }
            if (key == "Video") _ = _video.RefreshAsync();
            return;
        }

        // 首页打开帖子后切到视频 / 动态等页面时，自动关闭阅读器与浏览器
        if (IsReaderOpen) CloseReader();
        if (IsBrowserOpen) CloseBrowser();
        if (IsWebHubOpen) CloseWebHub();
        if (IsUploadOpen) CloseUpload();
        CloseProfile();
        if (key != "Home") CloseAnimePage();

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

        if (key == "Home")
        {
            var tab = HomeTabs.FirstOrDefault(t => t.IsSelected)?.Key;
            if (tab == "Web")
            {
                ShowWebHub();
            }
            else if (tab == "Anime")
            {
                CloseWebHub();
                ShowAnimePage();
            }
            else
            {
                CloseWebHub();
                CloseAnimePage();
            }
        }

        _settings.LastPage = key;
        _ = EnsureLoadedAsync(key);
    }

    private Views.LaunchView CreateLaunchView()
    {
        var view = new Views.LaunchView();
        view.OpenUrlRequested += url => OpenLink(url);
        return view;
    }

    private object CreateView(string key) => key switch
    {
        "Launch" => CreateLaunchView(),
        "Video" => new Views.VideoView(_video, _settings),
        "Moment" => new Views.MomentView(_moment),
        "Gacha" => new Views.GachaView(),
        "Daily" => new Views.DailyTaskView(),
        "Mine" => new Views.MineView(_mine),
        "Settings" => new Views.SettingsView(_settingsVm),
        _ => new Views.HomeView(_home),
    };

    private async Task EnsureLoadedAsync(string key)
    {
        switch (key)
        {
            case "Home":
                if (IsAnimePageOpen) { /* Flutter 模块自行加载内置规则 */ }
                else if (IsWebHubOpen) { /* 网页页签由 WebView2 自行加载 */ }
                else if (_home.IsPostsTab) await _home.ActivatePostsAsync();
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
        var start = _settings.StartOnLaunch
            ? "Launch"
            : _settings.StartOnVideo
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
        WebHubPage?.PauseMedia();
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
        WebHubPage?.PauseMedia();
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

    /// <summary>追番/影视在软件内播放：先提取真实视频流，再交给内置播放器，不展示第三方网页。</summary>
    private void ShowAnimePlayer(AnimePlayback playback) => ShowAnimePlayer(playback, false);

    /// <summary>开发期截图入口：用公开测试流检查 Anime4K 播放链路。</summary>
    public void OpenAnime4KDemo()
    {
        try
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "miko-flutter.log"),
                $"{DateTime.Now:HH:mm:ss.fff} OpenAnime4KDemo{Environment.NewLine}");
        }
        catch { }
        ShowAnimePlayer(new AnimePlayback
        {
            Title = "Anime4K 实时超分测试",
            Episode = "第 1 集",
            Source = "Miko 测试流",
            StreamUrl = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8",
            Referer = "https://test-streams.mux.dev/",
            Description = "用于验证 WebGL2 自适应边缘超分、播放控制与弹幕层是否正常工作。",
        }, true);
    }

    private void ShowAnimePlayer(AnimePlayback playback, bool anime4K = false)
    {
        if (playback is null || string.IsNullOrWhiteSpace(playback.StreamUrl)) return;
        CloseAnimePlayer();
        CloseAnimeSitePlayer();
        AnimePlayerView = new Views.AnimePlayerView(playback,
            playback.IsBilibili ? Array.Empty<AnimeProvider>() : _anime.Providers,
            _animeFollows, _animeDanmaku, CloseAnimePlayer, anime4K,
            url => ShowAnimeSiteFromPlayback(playback, url));
        // WebView2 是原生子窗口，会盖在 WPF 内容之上；播放时必须先隐藏 Flutter 宿主。
        AnimePage?.Suspend();
        IsAnimePageOpen = false;
        IsAnimePlayerOpen = true;
        try
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "miko-flutter.log"),
                $"{DateTime.Now:HH:mm:ss.fff} player-open={IsAnimePlayerOpen} view={AnimePlayerView is not null} url={playback.StreamUrl}{Environment.NewLine}");
        }
        catch { }
    }

    [RelayCommand]
    public void CloseAnimePlayer()
    {
        if (AnimePlayerView is null)
        {
            if (IsAnimePlayerOpen) IsAnimePlayerOpen = false;
            return;
        }
        AnimePlayerView.Shutdown();
        AnimePlayerView = null;
        IsAnimePlayerOpen = false;
        // 播放器关闭后回到 Flutter 追番页。
        if (AnimePage is not null && !IsAnimeSitePlayerOpen)
        {
            AnimePage.Resume();
            IsAnimePageOpen = true;
        }
    }

    [RelayCommand]
    public void CloseAnimeSitePlayer()
    {
        if (AnimeSitePlayerView is not null)
        {
            AnimeSitePlayerView.Shutdown();
            AnimeSitePlayerView = null;
        }
        IsAnimeSitePlayerOpen = false;
        if (AnimePage is not null)
        {
            AnimePage.Resume();
            IsAnimePageOpen = true;
        }
    }

    /// <summary>打开上传内容弹层；再次打开时回到第一步。</summary>
    public void OpenUpload()
    {
        PauseAllPlayback();
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

