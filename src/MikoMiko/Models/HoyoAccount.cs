using System;
using System.Text.Json.Serialization;

namespace MikoMiko.Models;

/// <summary>MikoMiko 独立维护的米游社通行证账号（Cookie 使用 DPAPI 加密保存）。</summary>
public sealed class HoyoAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nickname { get; set; } = "";
    public string Uid { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string CookieEncrypted { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime LastLogin { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string Cookie { get; set; } = "";

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? Uid : $"{Nickname}（{Uid}）";
}
