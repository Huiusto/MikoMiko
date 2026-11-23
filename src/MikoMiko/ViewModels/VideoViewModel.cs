using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>视频页：随机视频流，上下滑动切换。</summary>
public sealed partial class VideoViewModel : ObservableObject
{
    private readonly ContentService _content;
    private List<FeedItem> _all = new();
    private readonly HashSet<string> _seen = new();

    public VideoViewModel(ContentService content)
    {
        _content = content;
    }

    public ObservableCollection<FeedItem> Videos { get; } = new();
    public ObservableCollection<FeedItem> Related { get; } = new();
    public ObservableCollection<CommentItem> Comments { get; } = new();

    [ObservableProperty] private FeedItem? _selected;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private bool _hasSource;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private double _volume = 60;
    [ObservableProperty] private bool _showDanmaku = true;
    [ObservableProperty] private string _danmakuText = string.Empty;
    [ObservableProperty] private string _speedText = "倍速";
    [ObservableProperty] private string _filter = string.Empty;
    [ObservableProperty] private string _indexText = string.Empty;
    [ObservableProperty] private bool _isLiked;
    [ObservableProperty] private bool _isFavorited;
    [ObservableProperty] private bool _isFollowingAuthor;
    [ObservableProperty] private string _toast = string.Empty;
    [ObservableProperty] private bool _isCommentsOpen;
    [ObservableProperty] private string _commentInput = string.Empty;
    [ObservableProperty] private string _commentStatus = string.Empty;
    [ObservableProperty] private bool _canComment;
    [ObservableProperty] private string _qualityText = "自动";
    [ObservableProperty] private bool _isBuffering;
    [ObservableProperty] private bool _isQualityOpen;
    [ObservableProperty] private bool _isSpeedOpen;
    [ObservableProperty] private double _speedRatio = 1.0;
    [ObservableProperty] private string _replyTargetId = "0";
    [ObservableProperty] private string _replyTargetName = string.Empty;
    [ObservableProperty] private bool _hasReplyTarget;

    public IReadOnlyList<string> QualityOptions { get; } = new[] { "自动", "1080P", "720P", "480P" };
    public IReadOnlyList<string> SpeedOptions { get; } = new[] { "0.5×", "0.75×", "1.0×", "1.25×", "1.5×", "2.0×" };

    public string FavoriteText => IsFavorited ? "已收藏" : Localization.Loc.Instance["Video.Favorite"];
    partial void OnIsFavoritedChanged(bool value) => OnPropertyChanged(nameof(FavoriteText));

    public string FollowText => IsFollowingAuthor ? "已关注" : "关注";
    partial void OnIsFollowingAuthorChanged(bool value) => OnPropertyChanged(nameof(FollowText));

    public string CommentCountText
    {
        get
        {
            if (Selected is null) return "0";
            if (Comments.Count > 0) return Comments.Count.ToString();
            return string.IsNullOrWhiteSpace(Selected.ReplyText) ? "0" : Selected.ReplyText;
        }
    }

    public async Task LoadAsync()
    {
        if (_all.Count > 0)
        {
            return;
        }

        await ReloadAsync();
    }

    /// <summary>点「视频」导航时刷新一批新的随机视频（覆盖所有用户 / 所有板块）。</summary>
    public async Task RefreshAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IsLoading = true;
        IsPlaying = false;
        StatusText = Localization.Loc.Instance["Common.Loading"];
        try
        {
            _seen.Clear();
            _all = (await _content.GetRandomVideosAsync(12, _seen)).ToList();
            IsDemo = _content.IsDemo;
            Videos.Clear();
            foreach (var item in _all) Videos.Add(item);
            if (Videos.Count > 0) Select(Videos[0]);
            StatusText = string.Empty;
        }
        catch
        {
            StatusText = Localization.Loc.Instance["Common.Fail"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>离开视频页时暂停，不保留声音与播放状态。</summary>
    public void Pause()
    {
        IsPlaying = false;
    }

    public void Select(FeedItem item)
    {
        Selected = item;
        HasSource = !string.IsNullOrWhiteSpace(item.VideoUrl);
        IsPlaying = false;
        IsLiked = false;
        IsFavorited = false;
        ReplyTargetId = "0";
        ReplyTargetName = string.Empty;
        HasReplyTarget = false;
        IsFollowingAuthor = item.IsFollowing;
        Toast = string.Empty;
        StatusText = string.Empty;
        Related.Clear();
        foreach (var other in _all.Where(v => v.PostId != item.PostId).Take(8)) Related.Add(other);

        // 0.1.4 去掉「第 n/N 个」序号；在播放数前展示作者关注数。
        IndexText = string.Empty;
        CanComment = _content.Api.HasCookie;
        CommentStatus = string.Empty;
        Comments.Clear();
        _ = LoadCommentsAsync(item);
        if (string.IsNullOrWhiteSpace(item.AuthorFansText))
        {
            _ = LoadAuthorFansAsync(item);
        }

        if (!HasSource) _ = ResolveSourceAsync(item);
        _ = PreloadNextAsync();
    }

    private async Task LoadAuthorFansAsync(FeedItem item)
    {
        var fans = await _content.Api.GetAuthorFansAsync(item.AuthorUid);
        if (string.IsNullOrEmpty(fans) || Selected?.PostId != item.PostId) return;
        item.AuthorFansText = fans;
        if (Selected?.PostId == item.PostId) OnPropertyChanged(nameof(Selected));
    }

    /// <summary>右侧评论：同步米游社该视频帖的一级热评。</summary>
    private async Task LoadCommentsAsync(FeedItem item)
    {
        try
        {
            var replies = await _content.Api.GetRepliesAsync(item.PostId);
            if (Selected?.PostId != item.PostId) return;
            Comments.Clear();
            foreach (var reply in replies) Comments.Add(reply);
            OnPropertyChanged(nameof(CommentCountText));
        }
        catch
        {
            // 评论区加载失败不影响播放
        }
    }

    [RelayCommand]
    private void ToggleComments() => IsCommentsOpen = !IsCommentsOpen;

    [RelayCommand]
    private async Task SendCommentAsync()
    {
        if (Selected is null) return;
        CanComment = _content.Api.HasCookie;
        if (!CanComment)
        {
            CommentStatus = "请先在「我的」扫码登录后再评论";
            return;
        }

        var text = CommentInput.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        CommentStatus = "正在发送…";
        var result = await _content.Api.ReleaseReplyAsync(Selected.PostId, text,
            string.IsNullOrWhiteSpace(ReplyTargetId) ? "0" : ReplyTargetId);
        if (!result.Ok)
        {
            CommentStatus = result.Message;
            return;
        }

        CommentInput = string.Empty;
        CommentStatus = "评论成功";
        ReplyTargetId = "0";
        ReplyTargetName = string.Empty;
        HasReplyTarget = false;
        await LoadCommentsAsync(Selected);
    }

    /// <summary>选择要回复的评论（与米游社楼中楼一致，挂到该一级评论下）。</summary>
    [RelayCommand]
    private void ReplyTo(CommentItem? comment)
    {
        if (comment is null) return;
        var target = !string.IsNullOrWhiteSpace(comment.RootReplyId) ? comment.RootReplyId : comment.ReplyId;
        ReplyTargetId = string.IsNullOrWhiteSpace(target) ? "0" : target;
        ReplyTargetName = comment.Author;
        HasReplyTarget = true;
        CommentStatus = $"正在回复 {comment.Author}";
    }

    [RelayCommand]
    private void CancelReply()
    {
        ReplyTargetId = "0";
        ReplyTargetName = string.Empty;
        HasReplyTarget = false;
        CommentStatus = string.Empty;
    }

    [RelayCommand]
    private void LikeComment(CommentItem? comment)
    {
        if (comment is null) return;
        comment.IsLiked = !comment.IsLiked;
        comment.LikeText = BumpCount(comment.LikeText, comment.IsLiked);
    }

    [RelayCommand]
    private void DislikeComment(CommentItem? comment)
    {
        if (comment is null) return;
        comment.IsDisliked = !comment.IsDisliked;
        comment.DislikeText = BumpCount(comment.DislikeText, comment.IsDisliked);
        if (comment.IsDisliked) comment.IsLiked = false;
    }

    private static string BumpCount(string text, bool up)
    {
        var trimmed = (text ?? string.Empty).Trim();
        var unit = string.Empty;
        var number = 0.0;
        if (trimmed.EndsWith("万"))
        {
            unit = "万";
            number = double.TryParse(trimmed.TrimEnd('万'), out var w) ? w * 10000 : 0;
        }
        else
        {
            number = double.TryParse(trimmed, out var n) ? n : 0;
        }
        number = Math.Max(0, number + (up ? 1 : -1));
        if (number >= 10000 || unit == "万" && number >= 10000)
            return $"{number / 10000:0.#}万";
        return number <= 0 ? string.Empty : ((long)number).ToString();
    }

    [RelayCommand]
    private void ToggleQuality()
    {
        IsQualityOpen = !IsQualityOpen;
        IsSpeedOpen = false;
    }

    [RelayCommand]
    private async Task PickQuality(string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality)) return;
        IsQualityOpen = false;
        if (QualityText == quality) return;
        QualityText = quality;
        _content.Api.Quality = quality == "自动" ? "1080P" : quality;
        var wasPlaying = IsPlaying;
        await RefreshSourceAsync();
        if (wasPlaying) IsPlaying = true;
    }

    [RelayCommand]
    private void ToggleSpeed()
    {
        IsSpeedOpen = !IsSpeedOpen;
        IsQualityOpen = false;
    }

    [RelayCommand]
    private void PickSpeed(string? speed)
    {
        if (string.IsNullOrWhiteSpace(speed)) return;
        IsSpeedOpen = false;
        var value = double.TryParse(speed.TrimEnd('×', 'x'), out var parsed) ? parsed : 1.0;
        SpeedRatio = Math.Clamp(value, 0.5, 2.0);
        SpeedText = $"{SpeedRatio:0.##}×";
    }

    [RelayCommand]
    private void ToggleDanmaku() => ShowDanmaku = !ShowDanmaku;

    /// <summary>「自动」按钮真实切清晰度：自动 → 1080P → 720P → 480P 循环。</summary>
    public async Task ChangeQualityAsync()
    {
        var next = QualityText switch
        {
            "自动" => "1080P",
            "1080P" => "720P",
            "720P" => "480P",
            _ => "自动",
        };
        QualityText = next;
        _content.Api.Quality = next == "自动" ? "1080P" : next;
        await RefreshSourceAsync();
    }

    /// <summary>预加载后三个视频的详情与播放直链，下滑时无需再等接口。</summary>
    private async Task PreloadNextAsync()
    {
        if (Selected is null) return;
        var index = Videos.IndexOf(Selected);
        for (var i = 1; i <= 3 && index + i < Videos.Count; i++)
        {
            var item = Videos[index + i];
            try
            {
                if (string.IsNullOrWhiteSpace(item.VideoUrl))
                {
                    var detail = await _content.GetDetailAsync(item);
                    if (!string.IsNullOrWhiteSpace(detail.VideoUrl)) item.VideoUrl = detail.VideoUrl;
                }

                if (string.IsNullOrWhiteSpace(item.AuthorFansText))
                {
                    item.AuthorFansText = await _content.Api.GetAuthorFansAsync(item.AuthorUid);
                }
            }
            catch
            {
                // 预加载失败下次进入再补
            }
        }
    }

    /// <summary>向下滑：切到下一个随机视频，接近末尾时自动补充。</summary>
    public async Task NextAsync()
    {
        if (Videos.Count == 0) return;

        var index = Selected is null ? 0 : Videos.IndexOf(Selected);
        if (index + 4 >= Videos.Count - 1) await LoadMoreAsync();
        if (index >= Videos.Count - 1) return;

        var wasPlaying = IsPlaying;
        Select(Videos[index + 1]);
        if (wasPlaying) IsPlaying = true;
    }

    /// <summary>向上滑：回到上一个视频。</summary>
    public void Previous()
    {
        if (Selected is null) return;
        var index = Videos.IndexOf(Selected);
        if (index <= 0) return;

        var wasPlaying = IsPlaying;
        Select(Videos[index - 1]);
        if (wasPlaying) IsPlaying = true;
    }

    /// <summary>追加一批随机视频。</summary>
    public async Task LoadMoreAsync()
    {
        if (IsLoadingMore || IsLoading) return;

        IsLoadingMore = true;
        try
        {
            var more = await _content.GetRandomVideosAsync(8, _seen);
            foreach (var item in more)
            {
                if (!_seen.Add(item.PostId)) continue;
                _all.Add(item);
                Videos.Add(item);
            }

            if (Selected is not null)
            {
                IndexText = string.Empty;
            }
        }
        catch
        {
            // 忽略
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <summary>播放失败时重新向详情接口要一次带签名的直链（列表里的地址常已过期）。</summary>
    public async Task<bool> RefreshSourceAsync()
    {
        if (Selected is null) return false;
        try
        {
            var detail = await _content.GetDetailAsync(Selected);
            if (string.IsNullOrWhiteSpace(detail.VideoUrl)) return false;
            Selected.VideoUrl = detail.VideoUrl;
            HasSource = true;
            OnPropertyChanged(nameof(HasSource));
            OnPropertyChanged(nameof(Selected));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int _skipAttempts;

    /// <summary>列表页可能只给出封面，点开后再补一次详情以拿到可播放直链。</summary>
    private async Task ResolveSourceAsync(FeedItem item)
    {
        try
        {
            var detail = await _content.GetDetailAsync(item);
            if (!string.IsNullOrWhiteSpace(detail.VideoUrl))
            {
                _skipAttempts = 0;
                item.VideoUrl = detail.VideoUrl;
                if (Selected?.PostId == item.PostId)
                {
                    HasSource = true;
                    OnPropertyChanged(nameof(Selected));
                }
                return;
            }
        }
        catch
        {
            // 保持无源状态
        }

        // 这条内容确实没有视频（图文帖混进了视频流），自动滑到下一条
        if (Selected?.PostId == item.PostId && _skipAttempts < 4)
        {
            _skipAttempts++;
            await NextAsync();
        }
    }

    [RelayCommand]
    private void Play(FeedItem? item)
    {
        if (item is not null) Select(item);
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (Selected is null) return;
        if (!HasSource)
        {
            StatusText = "正在缓冲视频…";
            _ = ResolveAndPlayAsync(Selected);
            return;
        }
        IsPlaying = !IsPlaying;
    }

    private async Task ResolveAndPlayAsync(FeedItem item)
    {
        await ResolveSourceAsync(item);
        if (Selected?.PostId == item.PostId && HasSource) IsPlaying = true;
    }

    [RelayCommand]
    private Task Next() => NextAsync();

    [RelayCommand]
    private void PreviousVideo() => Previous();

    /// <summary>关注 / 取消关注视频作者（同步米游社）。</summary>
    [RelayCommand]
    private async Task FollowAuthor()
    {
        if (Selected is null || string.IsNullOrWhiteSpace(Selected.AuthorUid)) return;
        if (!_content.Api.HasCookie)
        {
            Toast = "请先在「我的」扫码登录后再关注";
            return;
        }

        var next = !IsFollowingAuthor;
        var ok = await _content.Api.SetFollowAsync(Selected.AuthorUid, next);
        if (ok)
        {
            IsFollowingAuthor = next;
            Selected.IsFollowing = next;
            Toast = next ? "已关注作者" : "已取消关注";
        }
        else
        {
            Toast = "关注失败，请稍后重试";
        }
    }

    /// <summary>点赞：本地立即反馈，绑定 Cookie 时同步到米游社账号。</summary>
    [RelayCommand]
    private async Task Like()
    {
        if (Selected is null) return;
        if (!_content.Api.HasCookie)
        {
            Toast = "请先在「我的」扫码登录后再点赞";
            return;
        }

        var next = !IsLiked;
        IsLiked = next;
        Selected.LikeText = BumpCount(Selected.LikeText, next);
        OnPropertyChanged(nameof(Selected));
        if (!next)
        {
            Toast = "已取消点赞";
            return;
        }

        var ok = await _content.Api.LikePostAsync(Selected.PostId);
        Toast = ok
            ? Localization.Loc.Instance["Video.Sync"]
            : Localization.Loc.Instance["Video.SyncFail"];
    }

    /// <summary>收藏：同上。</summary>
    [RelayCommand]
    private async Task Favorite()
    {
        if (Selected is null) return;
        if (!_content.Api.HasCookie)
        {
            Toast = "请先在「我的」扫码登录后再收藏";
            return;
        }

        var next = !IsFavorited;
        IsFavorited = next;
        if (!next)
        {
            Toast = "已取消收藏";
            return;
        }

        var ok = await _content.Api.FavoritePostAsync(Selected.PostId);
        Toast = ok
            ? Localization.Loc.Instance["Video.Sync"]
            : Localization.Loc.Instance["Video.SyncFail"];
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (Selected is null || string.IsNullOrWhiteSpace(Selected.WebUrl)) return;
        try
        {
            App.Shell.OpenLink(Selected.WebUrl);
        }
        catch
        {
            // 忽略打开失败
        }
    }

    public void ApplySearch(string text)
    {
        Filter = text ?? string.Empty;
        var keyword = Filter.Trim();
        Videos.Clear();
        var list = string.IsNullOrEmpty(keyword)
            ? _all
            : _all.Where(v => v.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                              || v.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        foreach (var item in list) Videos.Add(item);
    }
}
