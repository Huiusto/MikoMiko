namespace MikoMiko.Models;

/// <summary>动态（关注）条目。</summary>
public sealed class MomentItem
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string LikeText { get; set; } = string.Empty;
    public string ReplyText { get; set; } = string.Empty;
    public bool IsOfficial { get; set; }
    public bool IsFollowing { get; set; }
    public bool IsVideo { get; set; }
    public string VideoCover { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();

    public bool HasImages => Images.Count > 0;
    public bool HasTopic => !string.IsNullOrEmpty(Topic);
    public bool HasVideo => IsVideo && !string.IsNullOrEmpty(VideoCover);
    public bool HasSingleImage => Images.Count == 1;
    public bool HasMultiImage => Images.Count > 1;
}
