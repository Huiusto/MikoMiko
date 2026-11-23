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
    public string IpRegion { get; set; } = "未知";
    public string Posts { get; set; } = "0";
    public string Certification { get; set; } = string.Empty;
    public int CertificationType { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsFollowing { get; set; }

    public bool HasIntroduce => !string.IsNullOrWhiteSpace(Signature);
    public bool HasCertification => !string.IsNullOrWhiteSpace(Certification);

    /// <summary>开发者账号（灰岁Huiusto / 通行证 310621322）在用户主页显示淡紫色 M。</summary>
    public bool IsDeveloper => Uid == "310621322" || Nickname == "灰岁Huiusto";

    public string IdentityBadgeText => IsDeveloper
        ? "M"
        : CertificationType == 1 || Certification.StartsWith("官方") ? "官"
        : CertificationType >= 2 ? "V" : string.Empty;

    public bool ShowIdentityBadge => !string.IsNullOrWhiteSpace(IdentityBadgeText);

    public string IdentityBadgeBackground => IsDeveloper
        ? "#F1E8FC"
        : IdentityBadgeText == "官" ? "#FFF2D6" : "#FF6FA5";

    public string IdentityBadgeForeground => IsDeveloper
        ? "#8B5CF6"
        : IdentityBadgeText == "官" ? "#B8860B" : "#FFFFFF";

    public string UidText => $"米游社 UID {Uid}";

    public string LevelText => Level > 0 ? $"Lv.{Level}" : string.Empty;
    public string IpText => string.IsNullOrWhiteSpace(IpRegion) ? "IP 未知" : $"IP {IpRegion}";
}
