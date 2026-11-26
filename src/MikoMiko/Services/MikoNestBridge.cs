using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikoMiko.Services;

/// <summary>MikoNest 本地数据桥接：读取同一台电脑上 MikoNest 的游戏路径、账号与抽卡记录。</summary>
public static class MikoNestBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MikoNest");

    private static string LocalDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MikoMiko");

    private static string LocalPathsFile => Path.Combine(LocalDir, "launch_paths.json");

    private static Dictionary<string, string> LocalGamePaths { get; set; } = new();

    public static MikoNestSettings Settings { get; private set; } = new();

    public static IReadOnlyList<MikoNestAccount> Accounts { get; private set; } = Array.Empty<MikoNestAccount>();

    public static string DeviceId => string.IsNullOrWhiteSpace(Settings.DeviceId)
        ? "58a3eb9c-546c-401c-bb3e-0388a2b2027f"
        : Settings.DeviceId;

    public static bool HasData => File.Exists(Path.Combine(DataDir, "settings.json")) ||
                                 File.Exists(Path.Combine(DataDir, "accounts.json"));

    public static void Reload()
    {
        LoadLocalPaths();
        LoadSettings();
        LoadAccounts();
    }

    private static void LoadLocalPaths()
    {
        try
        {
            if (!File.Exists(LocalPathsFile)) return;
            LocalGamePaths = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(LocalPathsFile), JsonOptions) ?? new();
        }
        catch
        {
            LocalGamePaths = new();
        }
    }

    public static void SetLocalGamePath(string key, string path)
    {
        LocalGamePaths[key] = path;
        try
        {
            Directory.CreateDirectory(LocalDir);
            File.WriteAllText(LocalPathsFile, JsonSerializer.Serialize(LocalGamePaths, JsonOptions));
        }
        catch
        {
            // 路径保存失败不阻断启动
        }
    }

    public static void LoadSettings()
    {
        try
        {
            var file = Path.Combine(DataDir, "settings.json");
            if (!File.Exists(file)) return;
            Settings = JsonSerializer.Deserialize<MikoNestSettings>(File.ReadAllText(file), JsonOptions) ?? new();
        }
        catch
        {
            Settings = new MikoNestSettings();
        }
    }

    public static void LoadAccounts()
    {
        var list = new List<MikoNestAccount>();
        try
        {
            var file = Path.Combine(DataDir, "accounts.json");
            if (File.Exists(file))
            {
                var parsed = JsonSerializer.Deserialize<List<MikoNestAccount>>(File.ReadAllText(file), JsonOptions);
                if (parsed is not null)
                {
                    foreach (var account in parsed)
                    {
                        account.Cookie = Unprotect(account.CookieEncrypted) ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(account.Cookie)) list.Add(account);
                    }
                }
            }
        }
        catch
        {
            // 凭据无法解密时按未登录处理，不影响页面打开
        }
        Accounts = list;
    }

    public static MikoNestAccount? ActiveAccount => Accounts.FirstOrDefault(a => a.IsActive) ?? Accounts.FirstOrDefault();

    public static IReadOnlyList<GachaRecord> LoadGachaRecords()
    {
        try
        {
            var file = Path.Combine(DataDir, "gacha_records.json");
            if (!File.Exists(file)) return Array.Empty<GachaRecord>();
            var doc = JsonSerializer.Deserialize<GachaRecordFile>(File.ReadAllText(file), JsonOptions);
            return doc?.Records ?? new List<GachaRecord>();
        }
        catch
        {
            return Array.Empty<GachaRecord>();
        }
    }

    public static string? GetGameDir(string key)
    {
        if (LocalGamePaths.TryGetValue(key, out var local) && !string.IsNullOrWhiteSpace(local) && Directory.Exists(local))
            return local;
        if (Settings.GamePaths.TryGetValue(key, out var path) && !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            return path;
        return null;
    }

    public static string? FindGameExe(string key)
    {
        var game = Games.FirstOrDefault(g => g.Key == key);
        if (game is null) return null;
        var dir = GetGameDir(key);
        if (dir is null) return null;
        foreach (var exe in game.ExeNames)
        {
            var candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir.TrimEnd('\\'));
            if (parent is not null)
            {
                candidate = Path.Combine(parent, exe);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    private static string? Unprotect(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;
        try
        {
            var data = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }

    public static readonly NestGame[] Games =
    {
        new("genshin", "原神", new[] { "YuanShen.exe", "GenshinImpact.exe", "YS.exe" },
            "pack://application:,,,/Assets/Launch/bg_genshin.jpg", "pack://application:,,,/Assets/Launch/icons/icon_genshin.png", "#6FB8E7"),
        new("starrail", "星穹铁道", new[] { "StarRail.exe" },
            "pack://application:,,,/Assets/Launch/bg_StarRail.jpg", "pack://application:,,,/Assets/Launch/icons/icon_starrail.png", "#9C86F5"),
        new("zzz", "绝区零", new[] { "ZenlessZoneZero.exe" },
            "pack://application:,,,/Assets/Launch/bg_zzz.jpg", "pack://application:,,,/Assets/Launch/icons/icon_zzz.png", "#FFB74D"),
        new("bh3", "崩坏3", new[] { "BH3.exe" },
            "pack://application:,,,/Assets/Launch/bg_bh3.jpg", "pack://application:,,,/Assets/Launch/icons/icon_bh3.png", "#E58BD8"),
    };
}

public sealed class NestGame
{
    public NestGame(string key, string name, string[] exeNames, string background, string icon, string color)
    {
        Key = key;
        Name = name;
        ExeNames = exeNames;
        Background = background;
        Icon = icon;
        Color = color;
    }

    public string Key { get; }
    public string Name { get; }
    public string[] ExeNames { get; }
    public string Background { get; }
    public string Icon { get; }
    public string Color { get; }
}

public sealed class MikoNestSettings
{
    public Dictionary<string, string> GamePaths { get; set; } = new();
    public List<string> LaunchGameOrder { get; set; } = new();
    public string DeviceId { get; set; } = "";
}

public sealed class MikoNestAccount
{
    public string Id { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Uid { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string CookieEncrypted { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime LastLogin { get; set; }

    [JsonIgnore]
    public string Cookie { get; set; } = "";

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? Uid : $"{Nickname}（{Uid}）";
}

public sealed class GachaRecordFile
{
    public int Version { get; set; }
    public List<GachaRecord> Records { get; set; } = new();
}

public sealed class GachaRecord
{
    public string Uid { get; set; } = "";
    public string GameKey { get; set; } = "";
    public string GachaType { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RankType { get; set; } = "";
    public string ItemType { get; set; } = "";
    public string Count { get; set; } = "1";
    public string Time { get; set; } = "";
    public string Id { get; set; } = "";
    public string PoolName { get; set; } = "";
}
