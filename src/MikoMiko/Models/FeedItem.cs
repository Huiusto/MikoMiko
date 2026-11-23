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

    /// <summary>作者认证：1=官方，2=个人认证。</summary>
    public int CertificationType { get; set; }
    public string CertificationLabel { get; set; } = string.Empty;

    /// <summary>开发者账号（灰岁Huiusto / 通行证 310621322）额外显示淡紫色 MikoMiko。</summary>
    public bool IsDeveloper => AuthorUid == "310621322" || Author == "灰岁Huiusto";

    /// <summary>首页作者徽标：官方=金色「官」，其余统一为蓝底 M-STAR。</summary>
    public string BadgeText => IsOfficial ? "官" : "M-STAR";

    public string BadgeBackground => IsOfficial ? "#FFF2D6" : "#E6F4FB";

    public string BadgeForeground => IsOfficial ? "#B8860B" : "#2B8CC4";

    public bool IsWideBadge => false;

    /// <summary>开发者账号在 M-STAR 后追加淡紫色 MikoMiko。</summary>
    public bool ShowMikoBadge => IsDeveloper;
    public string MikoBadgeText => "MikoMiko";
    public string MikoBadgeBackground => "#F1E8FC";
    public string MikoBadgeForeground => "#8B5CF6";

    /// <summary>个人认证与开发者账号在 M-STAR 之后再显示一个粉红色「V」。</summary>
    public bool ShowCertBadge => !IsOfficial && (CertificationType >= 2 || IsDeveloper);
    public string CertificationBadgeText => "V";

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
