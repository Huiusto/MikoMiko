namespace MikoMiko.Models;

/// <summary>游戏角色（用于签到）。</summary>
public class SignRole
{
    public string Uid { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Region { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string GameBiz { get; set; } = "";
    public string Level { get; set; } = "";
}

/// <summary>一次签到 / 任务操作的日志行。</summary>
public class SignLogEntry
{
    public string Text { get; set; } = "";
    public string Level { get; set; } = "INFO";
    public DateTime Time { get; set; } = DateTime.Now;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsError => Level == "ERROR";

    public string Line => $"[{Level}][{Time:HH:mm:ss}]{Text}";

    public override string ToString() => Line;
}
