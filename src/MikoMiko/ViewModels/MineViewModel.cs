using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using QRCoder;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MikoMiko.ViewModels;

/// <summary>我的：内嵌官方二维码登录，登录后展示米游社个人主页与角色管理。</summary>
public sealed partial class MineViewModel : ObservableObject
{
    private readonly ContentService _content;
    private readonly AppSettings _settings;
    private readonly MiHoYoLoginService _login = new();
    private readonly Action<FeedItem>? _openPost;
    private DispatcherTimer? _pollTimer;
    private string _ticket = string.Empty;

    public MineViewModel(ContentService content, AppSettings settings, Action<FeedItem>? openPost = null)
    {
        _content = content;
        _settings = settings;
        _openPost = openPost;
        Profile = BuildGuestProfile();
        MineTabs = new ObservableCollection<ChoiceOption>
        {
            new("Posts", "我的发布") { IsSelected = true },
            new("Replies", "我的评论"),
            new("Collections", "我的合集"),
            new("Favorites", "我的收藏"),
        };
        LoadBackgroundSource();
    }

    public ObservableCollection<ChoiceOption> MineTabs { get; }

    public ObservableCollection<GameAccount> Games { get; } = new();
    public ObservableCollection<GameAccount> VisibleGames { get; } = new();
    public ObservableCollection<FeedItem> Favorites { get; } = new();
    public ObservableCollection<FeedItem> MyPosts { get; } = new();
    public ObservableCollection<UserReplyItem> MyReplies { get; } = new();
    public ObservableCollection<UserCollectionItem> MyCollections { get; } = new();
    public ObservableCollection<GameStatItem> GameStats { get; } = new();
    public ObservableCollection<FeedItem> GamePosts { get; } = new();

    public bool HasNoFavorites => Favorites.Count == 0;
    public bool HasNoMyPosts => MyPosts.Count == 0;
    public bool HasNoReplies => MyReplies.Count == 0;
    public bool HasNoCollections => MyCollections.Count == 0;
    public bool IsGuest => !IsLoggedIn;

    [ObservableProperty] private UserProfile _profile;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _isGameSettingsOpen;
    [ObservableProperty] private bool _hasVisibleGames = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _mineTab = "Posts";

    public bool ShowPostsTab => MineTab == "Posts";
    public bool ShowRepliesTab => MineTab == "Replies";
    public bool ShowCollectionsTab => MineTab == "Collections";
    public bool ShowFavoritesTab => MineTab == "Favorites";

    partial void OnMineTabChanged(string value)
    {
        OnPropertyChanged(nameof(ShowPostsTab));
        OnPropertyChanged(nameof(ShowRepliesTab));
        OnPropertyChanged(nameof(ShowCollectionsTab));
        OnPropertyChanged(nameof(ShowFavoritesTab));
    }
    [ObservableProperty] private string _cookieInput = string.Empty;
    [ObservableProperty] private string _bindStatus = string.Empty;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _nicknameDraft = string.Empty;
    [ObservableProperty] private bool _isQrOpen;
    [ObservableProperty] private ImageSource? _qrImage;
    [ObservableProperty] private string _loginStatus = string.Empty;
    [ObservableProperty] private ImageSource? _backgroundImage;
    [ObservableProperty] private GameAccount? _selectedGame;
    [ObservableProperty] private bool _isGameDetailOpen;
    [ObservableProperty] private bool _isGamePostsLoading;
    [ObservableProperty] private string _gameStatus = string.Empty;
    [ObservableProperty] private string _gameSummary = string.Empty;
    [ObservableProperty] private string _gameLevelText = string.Empty;
    [ObservableProperty] private string _gameUidText = string.Empty;
    [ObservableProperty] private string _gameRoleName = string.Empty;
    [ObservableProperty] private bool _gameDataPublic = true;

    // 背景裁剪：选图后先预览，用户调水平/垂直位置与缩放，确认后才写入设置
    private BitmapSource? _cropSource;
    private string _cropFilePath = string.Empty;
    [ObservableProperty] private bool _isCropOpen;
    [ObservableProperty] private ImageSource? _cropPreview;
    [ObservableProperty] private double _cropOffsetX = 0.5;
    [ObservableProperty] private double _cropOffsetY = 0.5;
    [ObservableProperty] private double _cropZoom = 1.0;

    /// <summary>自定义个人页背景；为空时显示默认蓝色渐变。</summary>
    public bool HasCustomBackground => BackgroundImage is not null;

    partial void OnIsLoggedInChanged(bool value) => OnPropertyChanged(nameof(IsGuest));

    private void LoadBackgroundSource()
    {
        var path = _settings.ProfileBackground;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            BackgroundImage = null;
            OnPropertyChanged(nameof(HasCustomBackground));
            return;
        }

        try
        {
            // OnLoad + Freeze：立即释放文件句柄，第二次更换背景才不会被 WPF 锁住。
            var image = new BitmapImage();
            using var stream = File.OpenRead(path);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            BackgroundImage = image;
        }
        catch
        {
            BackgroundImage = null;
        }

        OnPropertyChanged(nameof(HasCustomBackground));
    }

    /// <summary>选图后进入裁剪预览：用户自己选择图片要显示的区域。</summary>
    [RelayCommand]
    private void ChangeBackground()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择个人主页背景",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var image = new BitmapImage();
            using var stream = File.OpenRead(dialog.FileName);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            _cropSource = image;
            _cropFilePath = dialog.FileName;
            CropOffsetX = 0.5;
            CropOffsetY = 0.5;
            CropZoom = 1.0;
            IsCropOpen = true;
            UpdateCropPreview();
        }
        catch (Exception ex)
        {
            BindStatus = $"背景读取失败：{ex.Message}";
        }
    }

    partial void OnCropOffsetXChanged(double value) => UpdateCropPreview();
    partial void OnCropOffsetYChanged(double value) => UpdateCropPreview();
    partial void OnCropZoomChanged(double value) => UpdateCropPreview();

    private void UpdateCropPreview()
    {
        if (_cropSource is null) return;
        try
        {
            CropPreview = BuildCrop(_cropSource, CropOffsetX, CropOffsetY, CropZoom, 1200);
        }
        catch
        {
            // 预览失败不阻断确认
        }
    }

    /// <summary>按主页背景比例（约 2.2:1）裁出可见区域。</summary>
    private static CroppedBitmap BuildCrop(BitmapSource source, double offsetX, double offsetY, double zoom, int targetWidth)
    {
        const double aspect = 2.2;
        var maxWidth = Math.Min(source.PixelWidth, source.PixelHeight * aspect);
        var width = Math.Max(2, maxWidth * Math.Clamp(zoom, 0.35, 1.0));
        var height = width / aspect;
        var maxX = Math.Max(0, source.PixelWidth - width);
        var maxY = Math.Max(0, source.PixelHeight - height);
        var x = (int)Math.Round(Math.Clamp(offsetX, 0, 1) * maxX);
        var y = (int)Math.Round(Math.Clamp(offsetY, 0, 1) * maxY);
        return new CroppedBitmap(source, new Int32Rect(x, y, Math.Max(2, (int)width), Math.Max(2, (int)height)));
    }

    [RelayCommand]
    private void ConfirmBackgroundCrop()
    {
        if (_cropSource is null) return;
        try
        {
            var crop = BuildCrop(_cropSource, CropOffsetX, CropOffsetY, CropZoom, 1);
            var destination = Path.Combine(SettingsService.DataDirectory,
                $"profile-background-{DateTime.Now:yyyyMMddHHmmssfff}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(crop));
            using (var output = File.Create(destination)) encoder.Save(output);

            var old = _settings.ProfileBackground;
            _settings.ProfileBackground = destination;
            LoadBackgroundSource();
            IsCropOpen = false;
            CropPreview = null;
            _cropSource = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(old) && File.Exists(old) && old != destination) File.Delete(old);
            }
            catch
            {
                // 旧图被占用时忽略
            }
            BindStatus = "背景已按所选区域更新";
        }
        catch (Exception ex)
        {
            BindStatus = $"背景保存失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelBackgroundCrop()
    {
        IsCropOpen = false;
        CropPreview = null;
        _cropSource = null;
    }

    /// <summary>恢复默认渐变背景。</summary>
    [RelayCommand]
    private void RestoreDefaultBackground()
    {
        var old = _settings.ProfileBackground;
        _settings.ProfileBackground = string.Empty;
        LoadBackgroundSource();
        try
        {
            if (!string.IsNullOrWhiteSpace(old) && File.Exists(old)) File.Delete(old);
        }
        catch
        {
            // 忽略
        }

        BindStatus = "已恢复默认背景";
        OnPropertyChanged(nameof(HasCustomBackground));
    }

    /// <summary>角色管理：打开游戏详情与该游戏的官方帖。</summary>
    [RelayCommand]
    private async Task OpenGameAsync(GameAccount? game)
    {
        if (game is null) return;
        SelectedGame = game;
        IsGameDetailOpen = true;
        GameStatus = "正在读取该游戏战绩…";
        GamePosts.Clear();
        GameStats.Clear();
        GameSummary = string.Empty;
        GameLevelText = string.Empty;
        GameUidText = string.Empty;
        IsGamePostsLoading = true;
        try
        {
            var recordUid = CookieValue("ltuid_v2") is { Length: > 0 } v2
                ? v2
                : CookieValue("ltuid") is { Length: > 0 } v1 ? v1 : CookieValue("account_id");
            var cards = await _login.GetRecordCardsAsync(_settings.Cookie, recordUid);
            var card = cards.FirstOrDefault(c => c.GameId == game.GameId);
            if (card is not null)
            {
                GameRoleName = string.IsNullOrWhiteSpace(card.Nickname) ? game.RoleName : card.Nickname;
                GameLevelText = card.Level > 0 ? $"Lv.{card.Level}" : game.Level;
                GameUidText = $"UID {card.RoleUid} · {card.RegionName}";
                GameDataPublic = card.IsPublic;
                GameSummary = card.Stats.Count > 0 ? $"共 {card.Stats.Count} 项公开战绩" : "该游戏暂未公开战绩";
                foreach (var item in card.Stats)
                {
                    GameStats.Add(new GameStatItem { Label = item.Label, Value = item.Value });
                }
            }
            else
            {
                GameRoleName = game.RoleName;
                GameLevelText = game.Level;
                GameUidText = $"UID {game.Uid} · {game.Server}";
                GameSummary = "该角色暂无可读取的公开战绩";
            }

            var posts = await _content.GetGamePostsAsync(game.GameId);
            foreach (var post in posts) GamePosts.Add(post);
            GameStatus = GamePosts.Count == 0 ? "暂无该游戏的最新帖子" : string.Empty;
        }
        catch
        {
            GameStatus = "该游戏内容读取失败，请稍后重试";
        }
        finally
        {
            IsGamePostsLoading = false;
        }
    }

    [RelayCommand]
    private void CloseGameDetail() => IsGameDetailOpen = false;

    [RelayCommand]
    private void OpenGamePost(FeedItem? post)
    {
        if (post is null) return;
        IsGameDetailOpen = false;
        _openPost?.Invoke(post);
    }

    public async Task LoadAsync()
    {
        LoadBackgroundSource();
        IsLoggedIn = !string.IsNullOrWhiteSpace(_settings.Cookie);
        if (!IsLoggedIn)
        {
            ResetGuest();
            return;
        }

        Profile = BuildProfile(_settings.Nickname, _settings.Avatar);
        await SyncUserAsync();
    }

    // ---------------------------------------------------------------- 内嵌扫码登录

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsQrOpen = true;
        LoginStatus = "正在获取登录二维码…";
        QrImage = null;
        await CreateQrAsync();
    }

    private async Task CreateQrAsync()
    {
        try
        {
            var session = await _login.CreateQrAsync();
            if (session is null)
            {
                LoginStatus = "二维码获取失败，请检查网络后重试";
                return;
            }

            _ticket = session.Ticket;
            QrImage = RenderQr(session.Url);
            LoginStatus = "请用米游社 App 扫描二维码";
            StartPolling();
        }
        catch
        {
            LoginStatus = "二维码获取失败，请稍后重试";
        }
    }

    [RelayCommand]
    private async Task RefreshQrAsync()
    {
        StopPolling();
        LoginStatus = "正在刷新二维码…";
        await CreateQrAsync();
    }

    [RelayCommand]
    private void CancelLogin()
    {
        StopPolling();
        IsQrOpen = false;
        LoginStatus = string.Empty;
        QrImage = null;
    }

    private void StartPolling()
    {
        StopPolling();
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _pollTimer.Start();
    }

    private void StopPolling()
    {
        _pollTimer?.Stop();
        _pollTimer = null;
    }

    private async Task PollAsync()
    {
        if (string.IsNullOrEmpty(_ticket) || IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await _login.QueryAsync(_ticket);
            LoginStatus = result.Message;
            if (result.Status is "Waiting" or "Scanned") return;

            StopPolling();
            if (result.Status != "Confirmed" || string.IsNullOrWhiteSpace(result.Cookie))
            {
                IsBusy = false;
                return;
            }

            var cookie = result.Cookie;
            LoginStatus = "正在校验登录态…";
            var check = await _login.ValidateCookieAsync(cookie);
            if (!check.Ok)
            {
                LoginStatus = $"登录校验失败：{check.Message}";
                IsBusy = false;
                return;
            }

            _settings.Cookie = cookie;
            _content.RebuildApi();
            BindStatus = "登录成功，已同步米游社账号";
            IsQrOpen = false;
            IsLoggedIn = true;
            await SyncUserAsync();
        }
        catch
        {
            LoginStatus = "网络异常，登录失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static ImageSource RenderQr(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10);
        var image = new BitmapImage();
        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    // ---------------------------------------------------------------- Cookie / 资料

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task BindAsync()
    {
        var cookie = CookieInput.Trim();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            BindStatus = "请先粘贴包含 ltuid 与 cookie_token 的完整 Cookie";
            return;
        }

        IsBusy = true;
        BindStatus = "正在验证账号…";
        try
        {
            var check = await _login.ValidateCookieAsync(cookie);
            if (!check.Ok)
            {
                BindStatus = $"Cookie 无效：{check.Message}";
                return;
            }

            _settings.Cookie = cookie;
            _content.RebuildApi();
            IsLoggedIn = true;
            BindStatus = "账号登录成功";
            CookieInput = string.Empty;
            await SyncUserAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Unbind()
    {
        StopPolling();
        _settings.Cookie = string.Empty;
        _content.RebuildApi();
        ResetGuest();
        BindStatus = "已退出登录";
    }

    [RelayCommand]
    private void ToggleEdit()
    {
        if (!IsLoggedIn) return;

        if (!IsEditing)
        {
            NicknameDraft = Profile.Nickname;
            IsEditing = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(NicknameDraft))
        {
            _settings.Nickname = NicknameDraft.Trim();
            Profile.Nickname = _settings.Nickname;
            OnPropertyChanged(nameof(Profile));
        }

        IsEditing = false;
    }

    private async Task SyncUserAsync()
    {
        var uid = CookieValue("ltuid") is { Length: > 0 } value ? value : CookieValue("account_id");
        try
        {
            var stat = await _login.GetUserStatAsync(_settings.Cookie, uid);
            if (stat is not null)
            {
                ApplyStat(stat);
                await LoadGamesAsync();
                await LoadMineContentAsync(uid);
                return;
            }

            // 资料接口失败但登录态仍在：保留已缓存资料，不踢回未登录。
            IsLoggedIn = true;
            Profile = BuildProfile(string.IsNullOrWhiteSpace(_settings.Nickname) ? "米游社用户" : _settings.Nickname,
                _settings.Avatar);
            BindStatus = "已登录，账号资料同步失败，可点刷新重试";
            await LoadGamesAsync();
            await LoadMineContentAsync(uid);
        }
        catch
        {
            IsLoggedIn = true;
        }
    }

    private async Task LoadGamesAsync()
    {
        try
        {
            var roles = await _login.GetGameRolesAsync(_settings.Cookie);
            Games.Clear();
            foreach (var role in roles)
            {
                Games.Add(new GameAccount
                {
                    GameId = role.GameId,
                    GameBiz = role.GameBiz,
                    RoleName = role.Nickname,
                    GameName = role.GameName,
                    Uid = role.GameUid,
                    Server = string.IsNullOrWhiteSpace(role.Server) ? "米游社" : role.Server,
                    LevelLabel = "等级",
                    Level = role.Level.ToString(),
                    ExtraLabel = "角色",
                    Extra = role.Nickname,
                    AccentHex = role.AccentHex,
                    Badge = "已绑定",
                    Logo = LoadGameLogo(role.GameId, role.GameName),
                });
            }

            ApplyGamePreferences();
            await FillGameCardsAsync();
        }
        catch
        {
            // 角色接口失败不影响主页资料
        }
    }

    /// <summary>应用「我的」页角色卡的显示开关与排序。</summary>
    private void ApplyGamePreferences()
    {
        foreach (var game in Games)
            game.IsVisible = !_settings.HiddenGameIds.Contains(game.Key);

        if (_settings.GameOrder.Count > 0)
        {
            var ordered = Games
                .OrderBy(game =>
                {
                    var index = _settings.GameOrder.IndexOf(game.Key);
                    return index < 0 ? int.MaxValue : index;
                })
                .ToList();
            for (var target = 0; target < ordered.Count; target++)
            {
                var current = Games.IndexOf(ordered[target]);
                if (current != target) Games.Move(current, target);
            }
        }

        RefreshVisibleGames();
    }

    private void RefreshVisibleGames()
    {
        VisibleGames.Clear();
        foreach (var game in Games.Where(game => game.IsVisible)) VisibleGames.Add(game);
        HasVisibleGames = VisibleGames.Count > 0;
    }

    private void SyncGamePreferences()
    {
        _settings.GameOrder = Games.Select(game => game.Key).ToList();
        _settings.HiddenGameIds = Games.Where(game => !game.IsVisible).Select(game => game.Key).ToList();
    }

    [RelayCommand]
    private void ToggleGameSettings() => IsGameSettingsOpen = !IsGameSettingsOpen;

    [RelayCommand]
    private void ToggleGameVisible(GameAccount? game)
    {
        if (game is null) return;
        game.IsVisible = !game.IsVisible;
        RefreshVisibleGames();
        SyncGamePreferences();
    }

    [RelayCommand]
    private void MoveGameUp(GameAccount? game)
    {
        if (game is null) return;
        var index = Games.IndexOf(game);
        if (index <= 0) return;
        Games.Move(index, index - 1);
        RefreshVisibleGames();
        SyncGamePreferences();
    }

    [RelayCommand]
    private void MoveGameDown(GameAccount? game)
    {
        if (game is null) return;
        var index = Games.IndexOf(game);
        if (index < 0 || index >= Games.Count - 1) return;
        Games.Move(index, index + 1);
        RefreshVisibleGames();
        SyncGamePreferences();
    }

    /// <summary>把所有游戏的公开战绩补到角色卡上（与游戏详情共用 getGameRecordCard）。</summary>
    private async Task FillGameCardsAsync()
    {
        if (Games.Count == 0) return;
        var accountUid = CookieValue("ltuid") is { Length: > 0 } ltuid ? ltuid : CookieValue("account_id");
        try
        {
            var cards = await _login.GetRecordCardsAsync(_settings.Cookie, accountUid);
            foreach (var game in Games)
            {
                var card = cards.FirstOrDefault(c => c.GameId == game.GameId && c.RoleUid == game.Uid)
                           ?? cards.FirstOrDefault(c => c.GameId == game.GameId);
                if (card is null) continue;
                game.SetStats(card.Stats.Take(4)
                    .Select(s => new GameStatItem { Label = s.Label, Value = s.Value })
                    .ToList());
            }
        }
        catch
        {
            // 个别游戏未公开战绩时保持空卡片
        }
    }

    /// <summary>游戏图标：按游戏名优先匹配，避免渠道服 GameId 与官方编号不一致。</summary>
    private static ImageSource? LoadGameLogo(int gameId, string gameName)
    {
        var name = gameName ?? string.Empty;
        var file = name.Contains("原神") ? "genshin"
            : name.Contains("星穹") || name.Contains("星铁") ? "starrail"
            : name.Contains("绝区零") ? "zzz"
            : name.Contains("崩坏3") || name.Contains("崩坏三") ? "honkai3"
            : name.Contains("崩坏学园") ? "ggz"
            : name.Contains("未定") ? "tot"
            : gameId switch
            {
                2 => "genshin",
                6 => "starrail",
                8 => "zzz",
                1 => "honkai3",
                5 => "ggz",
                4 => "tot",
                _ => "icon",
            };
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri($"pack://application:,,,/Assets/{(file == "icon" ? "icon" : "Games/" + file)}.png", UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>「我的」页四个内容入口：发布 / 评论 / 合集 / 收藏，全部读取官方接口。</summary>
    private async Task LoadMineContentAsync(string uid)
    {
        try
        {
            var postsTask = _content.Api.GetUserPostsAsync(uid, 2, 20);
            var repliesTask = _content.Api.GetUserRepliesAsync(uid, 20);
            var collectionsTask = _content.Api.GetUserCollectionsAsync();
            var favoritesTask = _content.Api.GetFavoritePostsAsync(uid, 20);
            await Task.WhenAll(postsTask, repliesTask, collectionsTask, favoritesTask);

            MyPosts.Clear();
            foreach (var post in postsTask.Result) MyPosts.Add(post);
            MyReplies.Clear();
            foreach (var reply in repliesTask.Result) MyReplies.Add(reply);
            MyCollections.Clear();
            foreach (var collection in collectionsTask.Result) MyCollections.Add(collection);
            Favorites.Clear();
            foreach (var favorite in favoritesTask.Result) Favorites.Add(favorite);

            OnPropertyChanged(nameof(HasNoMyPosts));
            OnPropertyChanged(nameof(HasNoReplies));
            OnPropertyChanged(nameof(HasNoCollections));
            OnPropertyChanged(nameof(HasNoFavorites));
        }
        catch
        {
            BindStatus = "「我的」内容同步失败，可点刷新重试";
        }
    }

    [RelayCommand]
    private void SelectMineTab(ChoiceOption? tab)
    {
        if (tab is null) return;
        foreach (var item in MineTabs) item.IsSelected = item.Key == tab.Key;
        MineTab = tab.Key;
    }

    [RelayCommand]
    private void OpenMyPost(FeedItem? post)
    {
        if (post is not null) _openPost?.Invoke(post);
    }

    [RelayCommand]
    private void OpenMyReply(UserReplyItem? reply)
    {
        if (reply is null || string.IsNullOrWhiteSpace(reply.PostId)) return;
        var post = MyPosts.FirstOrDefault(p => p.PostId == reply.PostId);
        if (post is not null)
        {
            _openPost?.Invoke(post);
            return;
        }

        // 评论所在帖子不在“我的发布”里时，构造最小 FeedItem 交给阅读器按 post_id 拉全文。
        _openPost?.Invoke(new FeedItem
        {
            PostId = reply.PostId,
            Title = string.IsNullOrWhiteSpace(reply.PostTitle) ? "我评论过的帖子" : reply.PostTitle,
            Author = Profile.Nickname,
            AuthorUid = Profile.Uid,
            AuthorAvatar = Profile.Avatar,
        });
    }

    private void ApplyStat(MiHoYoLoginService.UserStat stat)
    {
        _settings.Nickname = stat.Nickname;
        if (!string.IsNullOrWhiteSpace(stat.Avatar)) _settings.Avatar = stat.Avatar;

        Profile = new UserProfile
        {
            IsLoggedIn = true,
            Nickname = stat.Nickname,
            Level = stat.Level,
            Avatar = string.IsNullOrWhiteSpace(stat.Avatar) ? "pack://application:,,,/Assets/icon.png" : stat.Avatar,
            Uid = stat.Uid,
            Signature = string.IsNullOrWhiteSpace(stat.Signature) ? "这个人很懒，什么都没写" : stat.Signature,
            Following = Compact(stat.Following),
            Fans = Compact(stat.Fans),
            Likes = Compact(stat.Likes),
            Views = Compact(stat.Posts),
            IpRegion = stat.IpRegion,
            Posts = stat.Posts,
        };
        IsLoggedIn = true;
    }

    private string CookieValue(string name)
    {
        foreach (var part in _settings.Cookie.Split(';'))
        {
            var index = part.IndexOf('=');
            if (index <= 0) continue;
            if (part[..index].Trim() == name) return part[(index + 1)..].Trim();
        }

        return "";
    }

    private static string Compact(string value)
    {
        if (!int.TryParse(value, out var number)) return string.IsNullOrWhiteSpace(value) ? "0" : value;
        if (number >= 10000) return (number / 10000.0).ToString("0.#") + "万";
        return number.ToString();
    }

    private void ResetGuest()
    {
        IsLoggedIn = false;
        Profile = BuildGuestProfile();
        Games.Clear();
        Favorites.Clear();
        MyPosts.Clear();
        MyReplies.Clear();
        MyCollections.Clear();
        GameStats.Clear();
        OnPropertyChanged(nameof(HasNoFavorites));
        OnPropertyChanged(nameof(HasNoMyPosts));
        OnPropertyChanged(nameof(HasNoReplies));
        OnPropertyChanged(nameof(HasNoCollections));
    }

    private static UserProfile BuildGuestProfile() => new()
    {
        IsLoggedIn = false,
        Nickname = "未登录",
        Avatar = "pack://application:,,,/Assets/icon.png",
        Level = 0,
        Signature = "登录后同步你的米游社昵称、头像、等级与游戏角色",
        Following = "—",
        Fans = "—",
        Likes = "—",
        Views = "—",
        Uid = string.Empty,
    };

    private static UserProfile BuildProfile(string nickname, string avatar) => new()
    {
        IsLoggedIn = true,
        Nickname = nickname,
        Avatar = string.IsNullOrWhiteSpace(avatar) ? "pack://application:,,,/Assets/icon.png" : avatar,
        Level = 6,
        Signature = "米游社账号已登录",
        Following = "—",
        Fans = "—",
        Likes = "—",
        Views = "—",
    };
}
