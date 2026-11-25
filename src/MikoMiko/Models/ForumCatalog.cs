namespace MikoMiko.Models;

/// <summary>米游社分区（游戏）及其下版块。</summary>
public sealed class ForumSection
{
    public int GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccentHex { get; set; } = "#4EA4DD";
    /// <summary>分区小图标（pack URI），无图标时为空串。</summary>
    public string IconPath { get; set; } = string.Empty;
    public List<ForumBoard> Boards { get; set; } = new();

    public string BoardsText => Boards.Count == 0
        ? "暂无版块"
        : string.Join(" · ", Boards.Take(5).Select(b => b.Name));

    // 自定义 ComboBox 模板只渲染 Content 的文本，重写 ToString 保证选中框显示分区名而不是类型名。
    public override string ToString() => Name;
}

/// <summary>分区下的讨论版块。</summary>
public sealed class ForumBoard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
