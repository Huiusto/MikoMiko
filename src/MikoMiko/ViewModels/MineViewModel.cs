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
        LoadBackgroundSource();
    }

    public ObservableCollection<GameAccount> Games { get; } = new();
    public ObservableCollection<FeedItem> Favorites { get; } = new();
    public ObservableCollection<FeedItem> GamePosts { get; } = new();

    public bool HasNoFavorites => Favorites.Count == 0;
    public bool IsGuest => !IsLoggedIn;

    [ObservableProperty] private UserProfile _profile;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _isBusy;
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

    /// <summary>选择本地图片作为「我的」页面背景，每次使用独立文件名后立即生效。</summary>
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
            var extension = Path.GetExtension(dialog.FileName);
            var destination = Path.Combine(SettingsService.DataDirectory,
                $"profile-background-{DateTime.Now:yyyyMMddHHmmssfff}{extension}");
            File.Copy(dialog.FileName, destination, overwrite: true);

            var old = _settings.ProfileBackground;
            _settings.ProfileBackground = destination;
            LoadBackgroundSource();

            // 清理上一张自定义背景；旧图已 Freeze 在内存中，不影响当前显示。
            try
            {
                if (!string.IsNullOrWhiteSpace(old) && File.Exists(old) && old != destination) File.Delete(old);
            }
            catch
            {
                // 旧图被占用时忽略
            }


        }
        catch (Exception ex)
        {
            BindStatus = $"背景设置失败：{ex.Message}";
        }
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
        GameStatus = "正在读取该游戏内容…";
        GamePosts.Clear();
        IsGamePostsLoading = true;
        try
        {
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
                return;
            }

            // 资料接口失败但登录态仍在：保留已缓存资料，不踢回未登录。
            IsLoggedIn = true;
            Profile = BuildProfile(string.IsNullOrWhiteSpace(_settings.Nickname) ? "米游社用户" : _settings.Nickname,
                _settings.Avatar);
            BindStatus = "已登录，账号资料同步失败，可点刷新重试";
            await LoadGamesAsync();
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
                });
            }
        }
        catch
        {
            // 角色接口失败不影响主页资料
        }
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
        OnPropertyChanged(nameof(HasNoFavorites));
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
