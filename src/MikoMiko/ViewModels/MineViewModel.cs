using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace MikoMiko.ViewModels;

/// <summary>我的：默认未登录，米游社扫码登录后展示真实账号资料。</summary>
public sealed partial class MineViewModel : ObservableObject
{
    private readonly ContentService _content;
    private readonly AppSettings _settings;

    public MineViewModel(ContentService content, AppSettings settings)
    {
        _content = content;
        _settings = settings;
        Profile = BuildGuestProfile();
    }

    public ObservableCollection<GameAccount> Games { get; } = new();
    public ObservableCollection<FeedItem> Favorites { get; } = new();

    public bool HasNoFavorites => Favorites.Count == 0;
    public bool IsGuest => !IsLoggedIn;

    [ObservableProperty] private UserProfile _profile;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _cookieInput = string.Empty;
    [ObservableProperty] private string _bindStatus = string.Empty;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _nicknameDraft = string.Empty;

    partial void OnIsLoggedInChanged(bool value) => OnPropertyChanged(nameof(IsGuest));

    public async Task LoadAsync()
    {
        IsLoggedIn = !string.IsNullOrWhiteSpace(_settings.Cookie);
        if (!IsLoggedIn)
        {
            ResetGuest();
            return;
        }

        Profile = BuildProfile(_settings.Nickname, _settings.Avatar);
        await SyncUserAsync();
    }

    /// <summary>米游社扫码登录（内嵌官方登录页，扫码后自动抓取 Cookie）。</summary>
    [RelayCommand]
    private void Login()
    {
        var window = new Views.LoginWindow
        {
            Owner = Application.Current.MainWindow,
        };
        window.LoggedIn = cookie =>
        {
            _settings.Cookie = cookie;
            _content.RebuildApi();
            IsLoggedIn = true;
            BindStatus = "登录成功，已同步米游社账号";
            _ = SyncUserAsync();
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task BindAsync()
    {
        var cookie = CookieInput.Trim();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            BindStatus = "请先粘贴包含 cookie_token 与 ltuid 的 Cookie";
            return;
        }

        IsBusy = true;
        BindStatus = "正在验证账号…";
        _settings.Cookie = cookie;
        _content.RebuildApi();

        try
        {
            var info = await _content.Api.GetUserInfoAsync(_settings.CommunityId);
            if (info.Ok)
            {
                IsLoggedIn = true;
                ApplyUser(info);
                BindStatus = "账号登录成功";
                CookieInput = string.Empty;
            }
            else
            {
                _settings.Cookie = string.Empty;
                _content.RebuildApi();
                ResetGuest();
                BindStatus = "Cookie 无效或已过期，请重新登录";
            }
        }
        catch
        {
            BindStatus = "网络异常，登录失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Unbind()
    {
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
        try
        {
            var info = await _content.Api.GetUserInfoAsync(_settings.CommunityId);
            if (info.Ok)
            {
                ApplyUser(info);
                return;
            }

            // Cookie 已经拿到但资料接口暂时失败：保留登录态，稍后刷新即可，
            // 不再直接把用户踢回未登录状态。
            IsLoggedIn = true;
            Profile = BuildProfile(string.IsNullOrWhiteSpace(_settings.Nickname) ? "米游社用户" : _settings.Nickname,
                _settings.Avatar);
            BindStatus = "已登录，账号资料同步失败，可点刷新重试";
        }
        catch
        {
            // 网络异常时保留登录态与已缓存的资料
            IsLoggedIn = true;
            if (string.IsNullOrWhiteSpace(_settings.Nickname))
            {
                Profile = BuildProfile("米游社用户", _settings.Avatar);
            }
        }
    }

    private void ApplyUser((bool Ok, string Nickname, int Level, string Avatar, string Uid) info)
    {
        _settings.Nickname = info.Nickname;
        if (!string.IsNullOrWhiteSpace(info.Avatar)) _settings.Avatar = info.Avatar;

        Profile = new UserProfile
        {
            IsLoggedIn = true,
            Nickname = info.Nickname,
            Level = info.Level,
            Avatar = string.IsNullOrWhiteSpace(info.Avatar) ? "pack://application:,,,/Assets/icon.png" : info.Avatar,
            Uid = info.Uid,
            Signature = Profile.Signature,
            Following = "—",
            Fans = "—",
            Likes = "—",
            Views = "—",
        };
        IsLoggedIn = true;
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
        Signature = "登录后同步你的米游社昵称、头像、等级与收藏",
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
