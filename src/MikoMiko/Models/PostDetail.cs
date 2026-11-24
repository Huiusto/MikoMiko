namespace MikoMiko.Models;

/// <summary>帖子详情（含官方统计与我的点赞 / 收藏状态）。</summary>
public sealed class PostDetail
{
    public string PostId { get; set; } = string.Empty;
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorUid { get; set; } = string.Empty;
    public string AuthorAvatar { get; set; } = string.Empty;
    public bool IsFollowingAuthor { get; set; }
    public string ForumName { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;

    public List<string> Images { get; set; } = new();

    /// <summary>帖子上原始的话题标签，保留官方展示样式。</summary>
    public List<string> Topics { get; set; } = new();

    /// <summary>按官方正文顺序排列的混排块：文本 / 图片 / 视频。</summary>
    public List<PostBlock> Blocks { get; set; } = new();

    public long LikeCount { get; set; }
    public long ReplyCount { get; set; }
    public long ViewCount { get; set; }
    public long FavoriteCount { get; set; }

    public bool IsLiked { get; set; }
    public bool IsFavorited { get; set; }

    public bool IsVideo { get; set; }
    public string VideoUrl { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;

    public string LikeText => Counts.Format(LikeCount);
    public string ReplyText => Counts.Format(ReplyCount);
    public string ViewText => Counts.Format(ViewCount);
    public string FavoriteText => Counts.Format(FavoriteCount);
}

/// <summary>帖子正文块（保持官方 structured_content 的顺序）。</summary>
public sealed class PostBlock
{
    public string Kind { get; set; } = "text"; // text / image / video
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;

    /// <summary>文本块中的富文本片段，保留官方字号 / 颜色 / 粗斜体 / 链接。</summary>
    public List<PostRun> Runs { get; set; } = new();

    public bool IsText => Kind == "text";
    public bool IsImage => Kind == "image";
    public bool IsVideo => Kind == "video";
}

/// <summary>一段带样式的正文文字（对应米游社 structured_content 的 insert + attributes）。</summary>
public sealed class PostRun
{
    public string Text { get; set; } = string.Empty;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strike { get; set; }
    public string Color { get; set; } = string.Empty;
    public double FontSize { get; set; }
    public string Link { get; set; } = string.Empty;
}

/// <summary>评论（含楼中楼）。</summary>
public sealed class CommentItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isLiked;
    private bool _isDisliked;
    private string _likeText = "0";
    private string _dislikeText = "0";
    private bool _isReplyInputOpen;
    private string _replyInput = string.Empty;
    private bool _isPostingReply;
    private string _inlineStatus = string.Empty;
    private bool _isCollapsed;

    public string ReplyId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public int Level { get; set; }
    public string IpRegion { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;

    /// <summary>服务端发布时间戳，用于“最新”排序。</summary>
    public long CreatedAt { get; set; }

    public bool IsAuthor { get; set; }

    /// <summary>评论里附带的图片（米游社 structured_content / images）。</summary>
    public List<string> Images { get; set; } = new();
    public bool HasImages => Images.Count > 0;

    /// <summary>回复到根楼（点击楼中楼的回复时仍挂到一级评论，保证对方能收到通知）。</summary>
    public string RootReplyId { get; set; } = string.Empty;

    /// <summary>楼中楼显示「回复 @某人：」。</summary>
    public string ReplyToName { get; set; } = string.Empty;
    public bool HasReplyTo => !string.IsNullOrWhiteSpace(ReplyToName);

    public bool IsReplyInputOpen
    {
        get => _isReplyInputOpen;
        set
        {
            if (_isReplyInputOpen == value) return;
            _isReplyInputOpen = value;
            OnPropertyChanged(nameof(IsReplyInputOpen));
        }
    }

    public string ReplyInput
    {
        get => _replyInput;
        set
        {
            if (_replyInput == value) return;
            _replyInput = value;
            OnPropertyChanged(nameof(ReplyInput));
        }
    }

    public bool IsPostingReply
    {
        get => _isPostingReply;
        set
        {
            if (_isPostingReply == value) return;
            _isPostingReply = value;
            OnPropertyChanged(nameof(IsPostingReply));
        }
    }

    public string InlineStatus
    {
        get => _inlineStatus;
        set
        {
            if (_inlineStatus == value) return;
            _inlineStatus = value;
            OnPropertyChanged(nameof(InlineStatus));
        }
    }

    public string LikeText
    {
        get => _likeText;
        set
        {
            if (_likeText == value) return;
            _likeText = value;
            OnPropertyChanged(nameof(LikeText));
        }
    }

    public string DislikeText
    {
        get => _dislikeText;
        set
        {
            if (_dislikeText == value) return;
            _dislikeText = value;
            OnPropertyChanged(nameof(DislikeText));
        }
    }

    public bool IsLiked
    {
        get => _isLiked;
        set
        {
            if (_isLiked == value) return;
            _isLiked = value;
            OnPropertyChanged(nameof(IsLiked));
        }
    }

    public bool IsDisliked
    {
        get => _isDisliked;
        set
        {
            if (_isDisliked == value) return;
            _isDisliked = value;
            OnPropertyChanged(nameof(IsDisliked));
        }
    }

    /// <summary>点踩后折叠正文；取消踩恢复。</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            OnPropertyChanged(nameof(IsCollapsed));
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    public bool IsExpanded => !_isCollapsed;

    public List<CommentItem> Children { get; set; } = new();

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

internal static class Counts
{
    public static string Format(long value)
    {
        if (value <= 0) return "0";
        if (value >= 100_000_000) return $"{value / 100_000_000.0:0.#}亿";
        if (value >= 10_000) return $"{value / 10_000.0:0.#}万";
        return value.ToString();
    }
}


