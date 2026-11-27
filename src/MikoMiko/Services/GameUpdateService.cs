using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MikoMiko.Services;

/// <summary>官方启动器版本状态：读取预下载版本，并和本地 config.ini 中的游戏版本比对。</summary>
public static class GameUpdateService
{
    private const string LauncherId = "jGHBHlcOq1";
    private const string PackageApi = "https://hyp-api.mihoyo.com/hyp/hyp-connect/api/getGamePackages";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly Dictionary<string, string> BizMap = new()
    {
        ["genshin"] = "hk4e_cn",
        ["starrail"] = "hkrpg_cn",
        ["zzz"] = "nap_cn",
        ["bh3"] = "bh3_cn",
    };

    private static readonly object Gate = new();
    private static DateTime _fetchedUtc;
    private static Dictionary<string, string> _preVersions = new();

    public sealed record UpdateState(string MainVersion, string PreVersion, bool HasPreDownload);

    /// <summary>返回每款游戏的官方预下载状态；网络失败时返回空缓存，不阻断启动页。</summary>
    public static async Task<Dictionary<string, UpdateState>> GetStatesAsync()
    {
        var result = new Dictionary<string, UpdateState>();
        var versions = await GetVersionsAsync().ConfigureAwait(false);
        foreach (var (key, biz) in BizMap)
        {
            var dir = MikoNestBridge.GetGameDir(key);
            var installed = ReadInstalledVersion(dir);
            var pre = versions.TryGetValue(biz, out var p) ? p : "";
            var hasPre = !string.IsNullOrWhiteSpace(pre) && !string.IsNullOrWhiteSpace(installed)
                         && !string.Equals(pre, installed, StringComparison.OrdinalIgnoreCase);
            result[key] = new UpdateState(installed ?? "", pre, hasPre);
        }
        return result;
    }

    private static async Task<Dictionary<string, string>> GetVersionsAsync()
    {
        lock (Gate)
        {
            if (DateTime.UtcNow - _fetchedUtc < TimeSpan.FromMinutes(30))
                return _preVersions;
        }

        var result = new Dictionary<string, string>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{PackageApi}?launcher_id={LauncherId}&language=zh-cn");
            req.Headers.TryAddWithoutValidation("User-Agent", "MikoMiko/0.1.57");
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return _preVersions;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("game_packages", out var packages))
                return _preVersions;

            foreach (var pkg in packages.EnumerateArray())
            {
                var biz = pkg.TryGetProperty("game", out var game) &&
                          game.TryGetProperty("biz", out var b) ? b.GetString() : null;
                if (string.IsNullOrWhiteSpace(biz)) continue;
                var pre = "";
                if (pkg.TryGetProperty("pre_download", out var preNode) &&
                    preNode.TryGetProperty("major", out var major) && major.ValueKind == JsonValueKind.Object &&
                    major.TryGetProperty("version", out var v))
                    pre = v.GetString() ?? "";
                result[biz] = pre;
            }

            lock (Gate)
            {
                _preVersions = result;
                _fetchedUtc = DateTime.UtcNow;
            }
            return result;
        }
        catch
        {
            return _preVersions;
        }
    }

    public static string? ReadInstalledVersion(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return null;
        try
        {
            var config = Path.Combine(dir, "config.ini");
            if (!File.Exists(config)) return null;
            foreach (var line in File.ReadLines(config))
            {
                if (line.StartsWith("game_version=", StringComparison.OrdinalIgnoreCase))
                    return line["game_version=".Length..].Trim();
            }
        }
        catch
        {
            // 读取不到版本时视为没有预下载提示
        }
        return null;
    }
}

