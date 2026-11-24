using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>关注页：左侧关注用户导航 + 右侧该用户（或全部）的动态流。</summary>
public sealed partial class MomentViewModel : ObservableObject
{
    private readonly ContentService _content;
    private readonly Action<MomentItem> _openMoment;
    private readonly Action<string> _openUser;
    private List<MomentItem> _all = new();

    public MomentViewModel(ContentService content, Action<MomentItem> openMoment, Action<string> openUser)
    {
        _content = content;
        _openMoment = openMoment;
        _openUser = openUser;
    }

    public ObservableCollection<MomentItem> Moments { get; } = new();
    public ObservableCollection<FollowUserItem> FollowingUsers { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private bool _onlyFollowing;
    [ObservableProperty] private string _filter = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _needsLogin;
    [ObservableProperty] private string _selectedAuthorUid = string.Empty;
    [ObservableProperty] private string _selectedAuthorName = "全部动态";
    [ObservableProperty] private bool _isAllSelected = true;

    public bool ShowAll => !OnlyFollowing;

    partial void OnOnlyFollowingChanged(bool value) => OnPropertyChanged(nameof(ShowAll));

    partial void OnIsAllSelectedChanged(bool value) => OnPropertyChanged(nameof(AllBadgeText));
    public string AllBadgeText => IsAllSelected ? "全部" : "全部动态";

    public async Task LoadAsync()
    {
        IsLoading = true;
        NeedsLogin = !_content.Api.HasCookie;
        if (NeedsLogin)
        {
            _all.Clear();
            Moments.Clear();
            FollowingUsers.Clear();
            IsEmpty = false;
            IsLoading = false;
            return;
        }

        try
        {
            var usersTask = _content.GetFollowingUsersAsync();
            var momentsTask = _content.GetMomentsAsync();
            await Task.WhenAll(usersTask, momentsTask);
            var users = usersTask.Result;
            _all = momentsTask.Result.ToList();
            IsDemo = _content.IsDemo;
            OnlyFollowing = true;

            FollowingUsers.Clear();
            foreach (var user in users)
            {
                var count = _all.Count(m => m.AuthorUid == user.Uid);
                FollowingUsers.Add(new FollowUserItem
                {
                    Uid = user.Uid,
                    Name = user.Nickname,
                    Avatar = string.IsNullOrWhiteSpace(user.Avatar)
                        ? "pack://application:,,,/Assets/avatar-placeholder.png"
                        : user.Avatar,
                    PostCount = count,
                    HasUpdate = count > 0,
                });
            }

            SelectAllInternal();
            Apply();
        }
        catch
        {
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    [RelayCommand]
    private void Open(MomentItem? item)
    {
        if (item is not null) _openMoment(item);
    }

    /// <summary>点击左侧「全部动态」。</summary>
    [RelayCommand]
    private void SelectAll()
    {
        SelectAllInternal();
        Apply();
    }

    /// <summary>点击左侧某位关注用户。</summary>
    [RelayCommand]
    private void SelectAuthor(FollowUserItem? user)
    {
        foreach (var item in FollowingUsers) item.IsSelected = item == user;
        SelectedAuthorUid = user?.Uid ?? string.Empty;
        SelectedAuthorName = user?.Name ?? "全部动态";
        IsAllSelected = false;
        Apply();
    }

    private void SelectAllInternal()
    {
        foreach (var item in FollowingUsers) item.IsSelected = false;
        SelectedAuthorUid = string.Empty;
        SelectedAuthorName = "全部动态";
        IsAllSelected = true;
    }

    [RelayCommand]
    private void OpenMomentAuthor(MomentItem? moment)
    {
        if (moment is null || string.IsNullOrWhiteSpace(moment.AuthorUid)) return;
        _openUser(moment.AuthorUid);
    }

    [RelayCommand]
    private void SelectMomentAuthor(MomentItem? moment)
    {
        if (moment is null || string.IsNullOrWhiteSpace(moment.AuthorUid)) return;
        var user = FollowingUsers.FirstOrDefault(u => u.Uid == moment.AuthorUid);
        if (user is not null)
        {
            SelectAuthor(user);
            return;
        }

        foreach (var item in FollowingUsers) item.IsSelected = false;
        SelectedAuthorUid = moment.AuthorUid;
        SelectedAuthorName = moment.Author;
        IsAllSelected = false;
        Apply();
    }

    [RelayCommand]
    private async Task ToggleLikeAsync(MomentItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.PostId)) return;
        var target = !item.IsLiked;
        if (!await _content.Api.LikePostAsync(item.PostId)) return;
        item.IsLiked = target;
        item.LikeText = BumpCount(item.LikeText, target);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(MomentItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.PostId)) return;
        if (!await _content.Api.FavoritePostAsync(item.PostId)) return;
        item.IsFavorited = !item.IsFavorited;
    }

    private static string BumpCount(string text, bool up)
    {
        var number = double.TryParse((text ?? string.Empty).Trim(), out var n) ? n : 0;
        number = Math.Max(0, number + (up ? 1 : -1));
        return number <= 0 ? string.Empty : ((long)number).ToString();
    }

    [RelayCommand]
    private void ToggleOnlyFollowing()
    {
        OnlyFollowing = !OnlyFollowing;
        Apply();
    }

    public void ApplySearch(string text)
    {
        Filter = text ?? string.Empty;
        Apply();
    }

    private void Apply()
    {
        var keyword = Filter.Trim();
        Moments.Clear();
        var list = _all.Where(m => !OnlyFollowing || m.IsFollowing);
        if (!string.IsNullOrEmpty(SelectedAuthorUid))
            list = list.Where(m => m.AuthorUid == SelectedAuthorUid);
        if (!string.IsNullOrEmpty(keyword))
        {
            list = list.Where(m => m.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                   || m.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in list.OrderByDescending(m => m.CreatedAt)) Moments.Add(item);
        IsEmpty = Moments.Count == 0;
    }
}
