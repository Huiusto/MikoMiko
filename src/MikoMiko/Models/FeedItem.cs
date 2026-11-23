namespace MikoMiko.Models;

/// <summary>资讯 / 帖子 / 视频卡片。</summary>
public sealed class FeedItem
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorUid { get; set; } = string.Empty;
    public string AuthorFansText { get; set; } = string.Empty;
    public string AuthorAvatar { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string ViewText { get; set; } = string.Empty;
    public string ReplyText { get; set; } = string.Empty;
    public string LikeText { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public string TagText { get; set; } = string.Empty;
    public string ForumName { get; set; } = string.Empty;
    public bool IsVideo { get; set; }
    public bool IsOfficial { get; set; }
    public long CreatedAt { get; set; }

    /// <summary>首页作者徽标：官方=金色「官」，开发者=粉色「MikoMiko」，其余=M-STAR。</summary>
    public string BadgeText => Author == "灰岁Huiusto" ? "MikoMiko" : IsOfficial ? "官" : "M-STAR";

    public string BadgeBackground => Author == "灰岁Huiusto"
        ? "#FFE3F0"
        : IsOfficial ? "#FFF2D6" : "#E6F4FB";

    public string BadgeForeground => Author == "灰岁Huiusto"
        ? "#E7549E"
        : IsOfficial ? "#B8860B" : "#2B8CC4";

    public bool IsWideBadge => BadgeText == "MikoMiko";

    /// <summary>首页卡片封面：没有配图的帖子使用默认占位图，但阅读器正文不插入该图。</summary>
    public string CardCover => string.IsNullOrWhiteSpace(Cover)
        ? "pack://application:,,,/Assets/Demo/placeholder.png"
        : Cover;

    /// <summary>作者是否被我关注（需 Cookie，接口返回 user.is_following）。</summary>
    public bool IsFollowing { get; set; }
    public bool HasCover => !string.IsNullOrEmpty(Cover);
    public bool HasDuration => !string.IsNullOrEmpty(DurationText);
    public bool HasTag => !string.IsNullOrEmpty(TagText);

    /// <summary>作者 · 时间。</summary>
    public string MetaText
    {
        get
        {
            if (string.IsNullOrEmpty(Author)) return TimeText;
            return string.IsNullOrEmpty(TimeText) ? Author : $"{Author} · {TimeText}";
        }
    }

    public string VideoUrl { get; set; } = string.Empty;
    public string Bvid { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;

    /// <summary>播放数前展示：作者关注数（粉丝）· 播放数。</summary>
    public string AudienceText => string.IsNullOrEmpty(AuthorFansText)
        ? $"{ViewText} 播放"
        : $"{AuthorFansText} 关注 · {ViewText} 播放";

    public List<string> Images { get; set; } = new();
    public string Body { get; set; } = string.Empty;
}
