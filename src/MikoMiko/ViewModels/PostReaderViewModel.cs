using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>软件内帖子阅读器：正文、图片、视频、点赞收藏与评论（小红书式）。</summary>
public sealed partial class PostReaderViewModel : ObservableObject
{
    private readonly ContentService _content;
    private readonly AppSettings _settings;

    public PostReaderViewModel(ContentService content, AppSettings settings, FeedItem item)
    {
        _content = content;
        _settings = settings;
        Item = item;
        Title = item.Title;
        Author = item.Author;
        AuthorUid = item.AuthorUid;
        AuthorAvatar = item.AuthorAvatar;
        IsFollowingAuthor = item.IsFollowing;
        TimeText = item.TimeText;
        ForumName = item.ForumName;
        Cover = item.Cover;
        Body = item.Summary;
        LikeText = item.LikeText;
        ReplyText = item.ReplyText;
        ViewText = item.ViewText;
        WebUrl = item.WebUrl;
        IsVideo = item.IsVideo;
        DurationText = item.DurationText;
        VideoUrl = item.VideoUrl;
        Images = new ObservableCollection<string>(item.Images);
        if (Images.Count == 0 && !string.IsNullOrEmpty(item.Cover)) Images.Add(item.Cover);
    }

    public FeedItem Item { get; }

    /// <summary>由 Shell 接手关闭（收起阅读器覆盖层）。</summary>
    public Action? CloseRequested { get; set; }

    /// <summary>由 Shell 接手打开作者主页。</summary>
    public Action<string>? OpenAuthorProfile { get; set; }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void OpenAuthor()
    {
        if (!string.IsNullOrWhiteSpace(AuthorUid)) OpenAuthorProfile?.Invoke(AuthorUid);
    }

    public ObservableCollection<string> Images { get; }
    public ObservableCollection<string> Topics { get; } = new();
    public ObservableCollection<CommentItem> Comments { get; } = new();
    public ObservableCollection<PostBlock> Blocks { get; } = new();

    public bool HasTopics => Topics.Count > 0;

    [ObservableProperty] private bool _isBodyExpanded;
    [ObservableProperty] private double _bodyMaxHeight = 460;
    [ObservableProperty] private bool _isLongBody;

    [RelayCommand]
    private void ToggleBody()
    {
        IsBodyExpanded = !IsBodyExpanded;
        BodyMaxHeight = IsBodyExpanded ? 20000 : 460;
        OnPropertyChanged(nameof(BodyToggleText));
    }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _authorUid = string.Empty;
    [ObservableProperty] private bool _isFollowingAuthor;

    public string FollowText => IsFollowingAuthor ? "已关注" : "关注";

    partial void OnIsFollowingAuthorChanged(bool value) => OnPropertyChanged(nameof(FollowText));
    [ObservableProperty] private string _authorAvatar = string.Empty;
    [ObservableProperty] private string _timeText = string.Empty;
    [ObservableProperty] private string _forumName = string.Empty;
    [ObservableProperty] private string _cover = string.Empty;
    [ObservableProperty] private string _body = string.Empty;
    [ObservableProperty] private string _webUrl = string.Empty;

    [ObservableProperty] private string _likeText = "0";
    [ObservableProperty] private string _replyText = "0";
    [ObservableProperty] private string _viewText = "0";
    [ObservableProperty] private string _favoriteText = "0";

    [ObservableProperty] private bool _isLiked;
    [ObservableProperty] private bool _isFavorited;
    [ObservableProperty] private string _toast = string.Empty;

    [ObservableProperty] private bool _isVideo;
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private string _videoUrl = string.Empty;
    [ObservableProperty] private string? _videoLocalPath;
    [ObservableProperty] private bool _isBuffering;
    [ObservableProperty] private string _bufferText = string.Empty;
    [ObservableProperty] private double _bufferProgress;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingComments;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _hasComments;

    // 评论输入（登录后可发表，直接同步到米游社）
    [ObservableProperty] private string _commentInput = string.Empty;
    [ObservableProperty] private bool _isPostingComment;
    [ObservableProperty] private string _commentStatus = string.Empty;
    [ObservableProperty] private string _replyTargetId = "0";
    [ObservableProperty] private string _replyTargetName = string.Empty;

    public bool HasImages => Images.Count > 0;
    public bool HasBody => !string.IsNullOrWhiteSpace(Body);
    public string BodyToggleText => IsBodyExpanded ? "收起" : "展开全文";
    public bool CanSync => _content.Api.HasCookie;
    public bool HasReplyTarget => ReplyTargetId != "0" && !string.IsNullOrWhiteSpace(ReplyTargetId);

    partial void OnReplyTargetIdChanged(string value) => OnPropertyChanged(nameof(HasReplyTarget));

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            if (!Item.PostId.StartsWith("demo", StringComparison.OrdinalIgnoreCase))
            {
                var detail = await _content.Api.GetPostDetailAsync(Item.PostId);
                if (detail is not null)
                {
                    Title = string.IsNullOrWhiteSpace(detail.Title) ? Title : detail.Title;
                    Author = string.IsNullOrWhiteSpace(detail.Author) ? Author : detail.Author;
                    AuthorUid = string.IsNullOrWhiteSpace(detail.AuthorUid) ? AuthorUid : detail.AuthorUid;
                    IsFollowingAuthor = detail.IsFollowingAuthor;
                    AuthorAvatar = string.IsNullOrWhiteSpace(detail.AuthorAvatar) ? AuthorAvatar : detail.AuthorAvatar;
                    ForumName = string.IsNullOrWhiteSpace(detail.ForumName) ? ForumName : detail.ForumName;
                    TimeText = string.IsNullOrWhiteSpace(detail.TimeText) ? TimeText : detail.TimeText;
                    Body = string.IsNullOrWhiteSpace(detail.Body) ? Body : detail.Body;
                    WebUrl = string.IsNullOrWhiteSpace(detail.WebUrl) ? WebUrl : detail.WebUrl;
                    LikeText = Counts.Format(detail.LikeCount);
                    ReplyText = Counts.Format(detail.ReplyCount);
                    ViewText = Counts.Format(detail.ViewCount);
                    FavoriteText = Counts.Format(detail.FavoriteCount);
                    FavoriteValue = detail.FavoriteCount;
                    IsLiked = detail.IsLiked;
                    IsFavorited = detail.IsFavorited;
                    IsVideo = detail.IsVideo || IsVideo;
                    DurationText = string.IsNullOrWhiteSpace(detail.DurationText) ? DurationText : detail.DurationText;
                    if (!string.IsNullOrWhiteSpace(detail.VideoUrl)) VideoUrl = detail.VideoUrl;

                    if (detail.Images.Count > 0)
                    {
                        Images.Clear();
                        foreach (var image in detail.Images) Images.Add(image);
                    }

                    if (detail.Blocks.Count > 0)
                    {
                        Blocks.Clear();
                        foreach (var block in detail.Blocks) Blocks.Add(block);
                    }

                    if (detail.Topics.Count > 0)
                    {
                        Topics.Clear();
                        foreach (var topic in detail.Topics) Topics.Add(topic);
                    }
                }
            }

            if (Blocks.Count == 0)
            {
                foreach (var line in Body.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line)) Blocks.Add(new PostBlock { Kind = "text", Text = line.Trim() });
                }
                foreach (var image in Images)
                {
                    Blocks.Add(new PostBlock { Kind = "image", Url = image });
                }
            }

            var textLength = Blocks.Where(b => b.IsText).Sum(b => b.Text.Length);
            IsLongBody = textLength > 600;
            BodyMaxHeight = IsLongBody ? 460 : 20000;

            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(HasBody));
            OnPropertyChanged(nameof(HasTopics));

            // 视频帖：先把直链落到本地缓存，再让播放器读本地文件
            if (IsVideo && !string.IsNullOrWhiteSpace(VideoUrl))
            {
                IsBuffering = true;
                BufferText = "正在缓冲视频…";
                var progress = new Progress<double>(p =>
                {
                    BufferProgress = p;
                    BufferText = $"正在缓冲视频 {p:P0}";
                });
                VideoLocalPath = await VideoCacheService.GetLocalAsync(VideoUrl, progress);
                IsBuffering = false;
                BufferText = VideoLocalPath is null ? "视频加载失败，可点击重试" : string.Empty;
            }

            await LoadCommentsAsync();
        }
        catch
        {
            StatusText = "内容加载失败，请稍后重试";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private long FavoriteValue { get; set; }

    [RelayCommand]
    private async Task ReloadCommentsAsync() => await LoadCommentsAsync();

    /// <summary>刷新整篇帖子与评论。</summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        StatusText = string.Empty;
        Toast = "正在刷新…";
        await LoadAsync();
        Toast = "已刷新";
    }

    /// <summary>评论点赞（本地即时反馈；已点赞时取消）。</summary>
    [RelayCommand]
    private void LikeComment(CommentItem? comment)
    {
        if (comment is null) return;
        comment.IsLiked = !comment.IsLiked;
        comment.LikeText = BumpCount(comment.LikeText, comment.IsLiked);
        if (comment.IsLiked) comment.IsDisliked = false;
    }

    /// <summary>评论点踩（本地即时反馈；与点赞互斥）。</summary>
    [RelayCommand]
    private void DislikeComment(CommentItem? comment)
    {
        if (comment is null) return;
        comment.IsDisliked = !comment.IsDisliked;
        comment.DislikeText = BumpCount(comment.DislikeText, comment.IsDisliked);
        if (comment.IsDisliked) comment.IsLiked = false;
    }

    /// <summary>发表评论并同步到米游社；成功后重新拉取评论列表。</summary>
    [RelayCommand]
    private async Task SendCommentAsync()
    {
        if (IsPostingComment) return;
        var text = CommentInput?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            CommentStatus = "先写点什么再发送吧";
            return;
        }

        if (!CanSync)
        {
            CommentStatus = "请先在「我的」扫码登录米游社后再评论";
            return;
        }

        if (Item.PostId.StartsWith("demo", StringComparison.OrdinalIgnoreCase))
        {
            CommentStatus = "演示内容不支持评论";
            return;
        }

        IsPostingComment = true;
        CommentStatus = "正在发布…";
        try
        {
            var result = await _content.Api.ReleaseReplyAsync(Item.PostId, text, ReplyTargetId);
            if (result.Ok)
            {
                CommentInput = string.Empty;
                ReplyTargetId = "0";
                ReplyTargetName = string.Empty;
                CommentStatus = result.Message;
                ReplyText = BumpCount(ReplyText, true);
                await LoadCommentsAsync();
            }
            else
            {
                CommentStatus = result.Message;
            }
        }
        catch
        {
            CommentStatus = "发布失败，请稍后重试";
        }
        finally
        {
            IsPostingComment = false;
        }
    }

    [RelayCommand]
    private void ReplyTo(CommentItem? comment)
    {
        if (comment is null) return;
        var open = !comment.IsReplyInputOpen;
        foreach (var root in Comments)
        {
            root.IsReplyInputOpen = false;
            foreach (var child in root.Children) child.IsReplyInputOpen = false;
        }
        comment.IsReplyInputOpen = open;
    }

    /// <summary>在评论下方直接发送回复：挂到该楼根评论，同步米游社。</summary>
    [RelayCommand]
    private async Task SendInlineReplyAsync(CommentItem? comment)
    {
        if (comment is null || comment.IsPostingReply) return;
        var text = comment.ReplyInput?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            comment.InlineStatus = "先写点什么再发送吧";
            return;
        }
        if (!CanSync)
        {
            comment.InlineStatus = "请先在「我的」扫码登录米游社";
            return;
        }

        comment.IsPostingReply = true;
        comment.InlineStatus = "正在发布…";
        try
        {
            var target = string.IsNullOrWhiteSpace(comment.RootReplyId) ? comment.ReplyId : comment.RootReplyId;
            var result = await _content.Api.ReleaseReplyAsync(Item.PostId, text, target);
            if (result.Ok)
            {
                comment.ReplyInput = string.Empty;
                comment.IsReplyInputOpen = false;
                comment.InlineStatus = string.Empty;
                ReplyText = BumpCount(ReplyText, true);
                Toast = "回复已同步到米游社";
                await LoadCommentsAsync();
            }
            else
            {
                comment.InlineStatus = result.Message;
            }
        }
        catch
        {
            comment.InlineStatus = "发布失败，请稍后重试";
        }
        finally
        {
            comment.IsPostingReply = false;
        }
    }

    [RelayCommand]
    private void CancelReply()
    {
        ReplyTargetId = "0";
        ReplyTargetName = string.Empty;
        CommentStatus = string.Empty;
        foreach (var root in Comments)
        {
            root.IsReplyInputOpen = false;
            foreach (var child in root.Children) child.IsReplyInputOpen = false;
        }
    }

    private async Task LoadCommentsAsync()
    {
        if (Item.PostId.StartsWith("demo", StringComparison.OrdinalIgnoreCase)) return;

        IsLoadingComments = true;
        try
        {
            var replies = await _content.Api.GetRepliesAsync(Item.PostId);
            Comments.Clear();
            foreach (var reply in replies) Comments.Add(reply);
            HasComments = Comments.Count > 0;
        }
        catch
        {
            HasComments = Comments.Count > 0;
        }
        finally
        {
            IsLoadingComments = false;
        }
    }

    [RelayCommand]
    private async Task FollowAuthorAsync()
    {
        if (string.IsNullOrWhiteSpace(AuthorUid)) return;
        if (!CanSync)
        {
            Toast = "请先在「我的」扫码登录米游社";
            return;
        }

        var target = !IsFollowingAuthor;
        if (await _content.Api.SetFollowAsync(AuthorUid, target))
        {
            IsFollowingAuthor = target;
            Toast = target ? "已关注，数据与官方同步" : "已取消关注";
        }
        else
        {
            Toast = "关注失败，请稍后重试";
        }
    }

    [RelayCommand]
    private async Task Like()
    {
        var target = !IsLiked;
        if (await _content.Api.LikePostAsync(Item.PostId))
        {
            IsLiked = target;
            LikeText = BumpCount(LikeText, target);
            Toast = "已同步到米游社";
        }
        else
        {
            Toast = CanSync ? "同步失败，请稍后重试" : "请先在「我的」扫码登录米游社";
        }
    }

    [RelayCommand]
    private async Task Favorite()
    {
        var target = !IsFavorited;
        if (await _content.Api.FavoritePostAsync(Item.PostId))
        {
            IsFavorited = target;
            FavoriteValue += target ? 1 : -1;
            FavoriteText = Counts.Format(Math.Max(0, FavoriteValue));
            Toast = "已同步到米游社";
        }
        else
        {
            Toast = CanSync ? "同步失败，请稍后重试" : "请先在「我的」扫码登录米游社";
        }
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (string.IsNullOrWhiteSpace(WebUrl)) return;
        try
        {
            App.Shell.OpenLink(WebUrl);
        }
        catch
        {
            // 忽略
        }
    }

    [RelayCommand]
    private async Task RetryVideoAsync()
    {
        if (string.IsNullOrWhiteSpace(VideoUrl)) return;
        IsBuffering = true;
        BufferText = "正在缓冲视频…";
        var progress = new Progress<double>(p =>
        {
            BufferProgress = p;
            BufferText = $"正在缓冲视频 {p:P0}";
        });
        VideoLocalPath = await VideoCacheService.GetLocalAsync(VideoUrl, progress);
        IsBuffering = false;
        BufferText = VideoLocalPath is null ? "视频加载失败，可点击重试" : string.Empty;
    }

    private static string BumpCount(string text, bool add)
    {
        var value = ParseCount(text);
        value = Math.Max(0, value + (add ? 1 : -1));
        return Counts.Format(value);
    }

    private static long ParseCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (text.EndsWith("亿") && double.TryParse(text[..^1], out var yi)) return (long)(yi * 100_000_000);
        if (text.EndsWith("万") && double.TryParse(text[..^1], out var wan)) return (long)(wan * 10_000);
        return long.TryParse(text, out var value) ? value : 0;
    }
}

