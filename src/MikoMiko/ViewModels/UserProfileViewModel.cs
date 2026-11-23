using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>其他用户的「我的」主页：资料、关注、TA 发布的内容。</summary>
public sealed partial class UserProfileViewModel : ObservableObject
{
    private readonly ContentService _content;
    private readonly Action<FeedItem> _openPost;

    public UserProfileViewModel(ContentService content, string uid, Action<FeedItem> openPost)
    {
        _content = content;
        Uid = uid;
        _openPost = openPost;
    }

    public string Uid { get; }
    public Action? CloseRequested { get; set; }

    public ObservableCollection<FeedItem> Posts { get; } = new();

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
    [ObservableProperty] private bool _isFollowing;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool HasIntroduce => !string.IsNullOrWhiteSpace(Introduce);
    public bool HasCertification => !string.IsNullOrWhiteSpace(Certification);
    public string FollowText => IsFollowing ? "已关注" : "关注";

    partial void OnIntroduceChanged(string value) => OnPropertyChanged(nameof(HasIntroduce));
    partial void OnCertificationChanged(string value) => OnPropertyChanged(nameof(HasCertification));
    partial void OnIsFollowingChanged(bool value) => OnPropertyChanged(nameof(FollowText));

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "正在读取用户主页…";
        try
        {
            var profileTask = _content.Api.GetUserProfileAsync(Uid);
            var postsTask = _content.Api.GetUserPostsAsync(Uid, 2, 20);
            await Task.WhenAll(profileTask, postsTask);
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
            }

            Posts.Clear();
            foreach (var post in postsTask.Result) Posts.Add(post);
            StatusText = Posts.Count == 0 ? "TA 还没有发布内容" : string.Empty;
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

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void OpenPost(FeedItem? post)
    {
        if (post is not null) _openPost(post);
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
}
