namespace MikoMiko.Models;

/// <summary>搜索结果中的用户卡片。</summary>
public sealed class SearchUser
{
    public string Uid { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string Introduce { get; set; } = string.Empty;
    public string Certification { get; set; } = string.Empty;
    public string FansText { get; set; } = "0";
}

/// <summary>「我的」页里我发布过的一条评论。</summary>
public sealed class UserReplyItem
{
    public string PostId { get; set; } = string.Empty;
    public string ReplyId { get; set; } = string.Empty;
    public string PostTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string LikeText { get; set; } = "0";
}

/// <summary>我创建的合集。</summary>
public sealed class UserCollectionItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public int PostCount { get; set; }
    public string CountText => PostCount > 0 ? $"{PostCount} 篇内容" : "查看合集";
}

/// <summary>游戏详情里的单条统计卡。</summary>
public sealed class GameStatItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
