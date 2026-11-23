using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MikoMiko.ViewModels;

/// <summary>其他用户的「我的」主页：资料、认证、标签、游戏角色与四类内容。</summary>
public sealed partial class UserProfileViewModel : ObservableObject
{
    private readonly ContentService _content;
    private readonly Action<FeedItem> _openPost;
    private readonly MiHoYoLoginService _login = new();
    private readonly List<FeedItem> _allPosts = new();

    public UserProfileViewModel(ContentService content, string uid, Action<FeedItem> openPost)
    {
        _content = content;
        Uid = uid;
        _openPost = openPost;
        Tabs = new ObservableCollection<ChoiceOption>
        {
            new("posts", "TA 的发布") { IsSelected = true },
            new("replies", "TA 的评论"),
            new("collections", "TA 的合集"),
            new("favorites", "TA 的收藏"),
        };
    }

    public string Uid { get; }
    public Action? CloseRequested { get; set; }

    public ObservableCollection<ChoiceOption> Tabs { get; }
    public ObservableCollection<string> Tags { get; } = new();
    public ObservableCollection<FeedItem> Posts { get; } = new();
    public ObservableCollection<UserReplyItem> Replies { get; } = new();
    public ObservableCollection<UserCollectionItem> Collections { get; } = new();
    public ObservableCollection<FeedItem> Favorites { get; } = new();
    public ObservableCollection<GameAccount> Games { get; } = new();

    [ObservableProperty] private string _nickname = "用户";
    [ObservableProperty] private string _avatar = string.Empty;
    [ObservableProperty] private string _introduce = string.Empty;
    [ObservableProperty] private string _levelText = string.Empty;
    [ObservableProperty] private string _ipRegion = string.Empty;
    [ObservableProperty] private string _certification = string.Empty;
    [ObservableProperty] private string _fansText = "0";
    [ObservableProperty] private string _followingText = "0";
    [ObservableProperty] private string _likeText = "0";
    [ObservableProperty] private string _postText = "0";
    [ObservableProperty] private string _userUidText = string.Empty;
    [ObservableProperty] private string _identityBadgeText = string.Empty;
    [ObservableProperty] private string _identityBadgeBackground = "#FF6FA5";
    [ObservableProperty] private string _identityBadgeForeground = "#FFFFFF";
    [ObservableProperty] private bool _showIdentityBadge;
    [ObservableProperty] private bool _isFollowing;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _selectedTab = "posts";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _hasPosts;
    [ObservableProperty] private bool _hasReplies;
    [ObservableProperty] private bool _hasCollections;
    [ObservableProperty] private bool _hasFavorites;
    [ObservableProperty] private bool _hasGames;
    [ObservableProperty] private bool _hasTags;
    [ObservableProperty] private GameAccount? _selectedGame;
    [ObservableProperty] private bool _isGameDetailOpen;

    public bool HasIntroduce => !string.IsNullOrWhiteSpace(Introduce);
    public bool HasCertification => !string.IsNullOrWhiteSpace(Certification);
    public string FollowText => IsFollowing ? "已关注" : "关注";
    public bool IsPostsTab => SelectedTab == "posts";
    public bool IsRepliesTab => SelectedTab == "replies";
    public bool IsCollectionsTab => SelectedTab == "collections";
    public bool IsFavoritesTab => SelectedTab == "favorites";

    partial void OnIntroduceChanged(string value) => OnPropertyChanged(nameof(HasIntroduce));
    partial void OnCertificationChanged(string value) => OnPropertyChanged(nameof(HasCertification));
    partial void OnIsFollowingChanged(bool value) => OnPropertyChanged(nameof(FollowText));
    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsPostsTab));
        OnPropertyChanged(nameof(IsRepliesTab));
        OnPropertyChanged(nameof(IsCollectionsTab));
        OnPropertyChanged(nameof(IsFavoritesTab));
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "正在读取用户主页…";
        try
        {
            var profileTask = _content.Api.GetUserProfileAsync(Uid);
            var postsTask = _content.Api.GetUserPostsAsync(Uid, 0, 20);
            var repliesTask = _content.Api.GetUserRepliesAsync(Uid, 20);
            var collectionsTask = _content.Api.GetUserCollectionsAsync(Uid);
            var favoritesTask = _content.Api.GetFavoritePostsAsync(Uid, 20);
            var cardsTask = _content.Api.HasCookie
                ? _login.GetRecordCardsAsync(_content.Api.Cookie, Uid)
                : Task.FromResult(new List<MiHoYoLoginService.GameRecordCard>());

            await Task.WhenAll(profileTask, postsTask, repliesTask, collectionsTask, favoritesTask, cardsTask);
            var profile = profileTask.Result;
            if (profile is not null)
            {
                Nickname = profile.Nickname;
                Avatar = profile.Avatar;
                Introduce = profile.Signature;
                LevelText = profile.LevelText;
                IpRegion = profile.IpRegion;
                Certification = profile.Certification;
                FansText = profile.Fans;
                FollowingText = profile.Following;
                LikeText = profile.Likes;
                PostText = profile.Posts;
                IsFollowing = profile.IsFollowing;
                UserUidText = profile.UidText;
                IdentityBadgeText = profile.IdentityBadgeText;
                IdentityBadgeBackground = profile.IdentityBadgeBackground;
                IdentityBadgeForeground = profile.IdentityBadgeForeground;
                ShowIdentityBadge = profile.ShowIdentityBadge;

                Tags.Clear();
                foreach (var tag in profile.Tags) Tags.Add(tag);
                HasTags = Tags.Count > 0;
            }

            _allPosts.Clear();
            _allPosts.AddRange(postsTask.Result);
            ApplyPostFilter();

            Replies.Clear();
            foreach (var reply in repliesTask.Result) Replies.Add(reply);
            HasReplies = Replies.Count > 0;

            Collections.Clear();
            foreach (var collection in collectionsTask.Result) Collections.Add(collection);
            HasCollections = Collections.Count > 0;

            Favorites.Clear();
            foreach (var favorite in favoritesTask.Result) Favorites.Add(favorite);
            HasFavorites = Favorites.Count > 0;

            Games.Clear();
            foreach (var card in cardsTask.Result)
            {
                Games.Add(new GameAccount
                {
                    GameId = card.GameId,
                    GameName = card.GameName,
                    RoleName = card.Nickname,
                    Uid = card.RoleUid,
                    Server = string.IsNullOrWhiteSpace(card.RegionName) ? "米游社" : card.RegionName,
                    LevelLabel = "等级",
                    Level = card.Level > 0 ? card.Level.ToString() : string.Empty,
                    ExtraLabel = "角色",
                    Extra = card.Nickname,
                    AccentHex = "#4EA4DD",
                    Badge = card.IsPublic ? "公开" : "私密",
                    Logo = LoadGameLogo(card.GameId, card.GameName),
                });
                Games[^1].SetStats(card.Stats.Take(4)
                    .Select(s => new GameStatItem { Label = s.Label, Value = s.Value })
                    .ToList());
            }
            HasGames = Games.Count > 0;
            StatusText = _allPosts.Count == 0 && Replies.Count == 0 && Collections.Count == 0 && Favorites.Count == 0
                ? "TA 还没有公开内容"
                : string.Empty;
        }
        catch
        {
            StatusText = "用户主页加载失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyPostFilter()
    {
        var keyword = SearchText.Trim();
        var list = string.IsNullOrWhiteSpace(keyword)
            ? _allPosts
            : _allPosts.Where(post =>
                post.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || post.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        Posts.Clear();
        foreach (var post in list) Posts.Add(post);
        HasPosts = Posts.Count > 0;
    }

    [RelayCommand]
    private void SelectTab(ChoiceOption? tab)
    {
        if (tab is null) return;
        foreach (var item in Tabs) item.IsSelected = item.Key == tab.Key;
        SelectedTab = tab.Key;
    }

    /// <summary>点击「TA 的游戏角色」卡片，打开该游戏的战绩详情。</summary>
    [RelayCommand]
    private void OpenGame(GameAccount? game)
    {
        if (game is null) return;
        SelectedGame = game;
        IsGameDetailOpen = true;
    }

    [RelayCommand]
    private void CloseGameDetail() => IsGameDetailOpen = false;

    [RelayCommand]
    private void SearchPosts()
    {
        ApplyPostFilter();
        SelectedTab = "posts";
        foreach (var item in Tabs) item.IsSelected = item.Key == "posts";
        StatusText = HasPosts ? string.Empty : "没有找到匹配的发布内容";
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void OpenPost(FeedItem? post)
    {
        if (post is not null) _openPost(post);
    }

    [RelayCommand]
    private void OpenReply(UserReplyItem? reply)
    {
        if (reply is null || string.IsNullOrWhiteSpace(reply.PostId)) return;
        _openPost(new FeedItem { Id = reply.PostId, PostId = reply.PostId, Title = reply.PostTitle });
    }

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        if (!_content.Api.HasCookie)
        {
            StatusText = "请先在「我的」扫码登录米游社";
            return;
        }

        var target = !IsFollowing;
        if (await _content.Api.SetFollowAsync(Uid, target))
        {
            IsFollowing = target;
            StatusText = target ? "已关注，数据与官方同步" : "已取消关注";
        }
        else
        {
            StatusText = "关注失败，请稍后重试";
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
}
