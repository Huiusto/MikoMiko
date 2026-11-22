namespace MikoMiko.Models;

/// <summary>当前登录（或演示）的账号信息。</summary>
public sealed class UserProfile
{
    public bool IsLoggedIn { get; set; }
    public string Nickname { get; set; } = "灰岁Huiusto";
    public string Avatar { get; set; } = string.Empty;
    public int Level { get; set; } = 6;
    public string Signature { get; set; } = "提瓦特大陆不知名人口普查员，喂你一下，风神护佑你，陌生人！";
    public string Following { get; set; } = "128";
    public string Fans { get; set; } = "36";
    public string Likes { get; set; } = "1.2万";
    public string Views { get; set; } = "8.6万";
    public string Uid { get; set; } = string.Empty;

    public string LevelText => $"Lv.{Level}";
}
