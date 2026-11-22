namespace MikoMiko.Models;

/// <summary>帖子详情（含官方统计与我的点赞 / 收藏状态）。</summary>
public sealed class PostDetail
{
    public string PostId { get; set; } = string.Empty;
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorAvatar { get; set; } = string.Empty;
    public string ForumName { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;

    public List<string> Images { get; set; } = new();

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

/// <summary>评论（含楼中楼）。</summary>
public sealed class CommentItem
{
    public string ReplyId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public int Level { get; set; }
    public string IpRegion { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string LikeText { get; set; } = "0";
    public bool IsLiked { get; set; }
    public bool IsAuthor { get; set; }
    public List<CommentItem> Children { get; set; } = new();
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


