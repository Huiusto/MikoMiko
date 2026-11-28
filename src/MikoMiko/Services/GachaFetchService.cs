using System.IO;
using System.Text;
using System.Text.Json;
using MikoMiko.Models;

namespace MikoMiko.Services;

/// <summary>
/// 官方抽卡记录拉取：优先读取游戏 webCache 里的 authkey，也可用当前米游社登录态换取 authkey。
/// 接口口径与 Starward / MikoNest 一致（UIGF 卡池编号）。
/// </summary>
public static class GachaFetchService
{
    private const string GenAuthKeyUrl = "https://api-takumi.mihoyo.com/binding/api/genAuthKey";
    private const string BbsUa = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/2.115.0";

    public sealed record PoolDef(string Code, string Name, params string[] ExtraCodes);

    private sealed record GameDef(
        string Key,
        string Biz,
        string DirHint,
        string AltDirHint,
        string CachePrefix,
        string CacheAltPrefix,
        string ApiPrefix,
        PoolDef[] Pools);

    private static readonly Dictionary<string, GameDef> Games = new()
    {
        ["genshin"] = new("genshin", "hk4e_cn", "YuanShen_Data", "GenshinImpact_Data",
            "https://webstatic.mihoyo.com/hk4e/event/e20190909gacha",
            "https://gs.hoyoverse.com/genshin/event/e20190909gacha",
            "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog",
            new[]
            {
                new PoolDef("200", "常驻祈愿"),
                new PoolDef("301", "角色活动祈愿", "400"),
                new PoolDef("302", "武器活动祈愿"),
                new PoolDef("500", "集录祈愿"),
                new PoolDef("100", "新手祈愿"),
            }),
        ["starrail"] = new("starrail", "hkrpg_cn", "StarRail_Data", "StarRail_Data",
            "https://webstatic.mihoyo.com/hkrpg/event/e20211215gacha",
            "https://webstatic.mihoyo.com/hkrpg/event/e20211215gacha",
            "https://public-operation-hkrpg.mihoyo.com/common/hkrpg_gacha_record/api/getGachaLog",
            new[]
            {
                new PoolDef("1", "群星跃迁"),
                new PoolDef("11", "角色活动跃迁"),
                new PoolDef("12", "光锥活动跃迁", "22"),
                new PoolDef("21", "角色联动跃迁"),
                new PoolDef("2", "新手跃迁"),
            }),
        ["zzz"] = new("zzz", "nap_cn", "ZenlessZoneZero_Data", "ZenlessZoneZero_Data",
            "https://webstatic.mihoyo.com/nap/event/e20230424gacha",
            "https://webstatic.mihoyo.com/nap/event/e20230424gacha",
            "https://public-operation-nap.mihoyo.com/common/gacha_record/api/getGachaLog",
            new[]
            {
                new PoolDef("2", "独家频段", "102"),
                new PoolDef("3", "音擎频段", "103"),
                new PoolDef("1", "常驻频段"),
                new PoolDef("5", "邦布频段"),
            }),
        ["bh3"] = new("bh3", "bh3_cn", "BH3_Data", "BH3_Data",
            "https://webstatic.mihoyo.com/bh3/event/e20210309gacha",
            "https://webstatic.mihoyo.com/bh3/event/e20210309gacha",
            "https://api-takumi.mihoyo.com/common/gacha_record/api/getGachaLog",
            new[]
            {
                new PoolDef("1", "标配补给"),
                new PoolDef("2", "精准补给"),
                new PoolDef("3", "扩充补给"),
                new PoolDef("4", "装备补给"),
                new PoolDef("5", "人偶补给"),
            }),
    };

    public static IReadOnlyList<string> SupportedGames => Games.Keys.ToList();

    public static PoolDef[] GetPools(string gameKey)
        => Games.TryGetValue(gameKey, out var g) ? g.Pools : Array.Empty<PoolDef>();

    public static string PoolName(string gameKey, string code)
    {
        var pools = GetPools(gameKey);
        foreach (var pool in pools)
        {
            if (pool.Code == code || pool.ExtraCodes.Contains(code)) return pool.Name;
        }
        return code switch
        {
            "200" => "常驻祈愿",
            "301" or "400" => "角色活动祈愿",
            "302" => "武器活动祈愿",
            "500" => "集录祈愿",
            "100" => "新手祈愿",
            _ => $"卡池 {code}",
        };
    }

    /// <summary>从游戏缓存中找出最近一次打开的抽卡记录页 URL（含 authkey）。</summary>
    public static string? FindCachedUrl(string gameKey)
    {
        if (!Games.TryGetValue(gameKey, out var game)) return null;
        var root = MikoNestBridge.GetGameDir(gameKey);
        if (string.IsNullOrWhiteSpace(root)) return null;

        var dirs = new List<string> { game.DirHint, game.AltDirHint }.Distinct().ToList();
        foreach (var dirName in dirs)
        {
            var dataDir = Path.Combine(root, dirName);
            if (!Directory.Exists(dataDir)) continue;
            var cacheRoot = Path.Combine(dataDir, "webCaches");
            if (!Directory.Exists(cacheRoot)) continue;

            var files = new List<string>();
            var direct = Path.Combine(cacheRoot, "Cache", "Cache_Data");
            if (Directory.Exists(direct)) files.AddRange(EnumerateCacheFiles(direct));
            foreach (var versionDir in Directory.EnumerateDirectories(cacheRoot))
            {
                var versionCache = Path.Combine(versionDir, "Cache", "Cache_Data");
                if (Directory.Exists(versionCache)) files.AddRange(EnumerateCacheFiles(versionCache));
            }

            foreach (var file in files.OrderByDescending(File.GetLastWriteTimeUtc))
            {
                var url = ReadUrlFromCache(file, game.CachePrefix, game.CacheAltPrefix);
                if (!string.IsNullOrWhiteSpace(url)) return url;
            }
        }
        return null;
    }

    private static IEnumerable<string> EnumerateCacheFiles(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "data_*");
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? ReadUrlFromCache(string file, string prefix, string altPrefix)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();
                var index = Math.Max(text.LastIndexOf(prefix, StringComparison.OrdinalIgnoreCase),
                    text.LastIndexOf(altPrefix, StringComparison.OrdinalIgnoreCase));
                if (index < 0) return null;
                var end = text.IndexOfAny(new[] { '\0', '"' }, index);
                if (end < 0) end = text.Length;
                var url = text[index..end].Trim();
                return url.Contains("authkey", StringComparison.OrdinalIgnoreCase) ? url : null;
            }
            catch (IOException)
            {
                Thread.Sleep(120);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>把缓存的抽卡页面 URL 换成官方 API 地址。</summary>
    public static string ToApiUrl(string url, string gameKey)
    {
        if (string.IsNullOrWhiteSpace(url) || !Games.TryGetValue(gameKey, out var game)) return url;
        var q = url.IndexOf('?');
        if (q < 0) return url;
        var query = url[q..].Replace("#/log", "", StringComparison.OrdinalIgnoreCase);
        var result = game.ApiPrefix + query;
        if (!query.Contains("game_biz=", StringComparison.OrdinalIgnoreCase))
            result += "&game_biz=" + game.Biz;
        return result;
    }

    public static async Task<(string? Url, string Message)> ResolveUrlAsync(string gameKey, string cookie,
        string uid, string region, bool preferCache)
    {
        if (preferCache)
        {
            var cached = FindCachedUrl(gameKey);
            if (!string.IsNullOrWhiteSpace(cached))
                return (ToApiUrl(cached, gameKey), "已读取游戏缓存中的 authkey");
        }

        var generated = await GenerateAuthKeyAsync(gameKey, cookie, uid, region);
        if (generated.Url is not null) return generated;

        var cachedFallback = FindCachedUrl(gameKey);
        if (!string.IsNullOrWhiteSpace(cachedFallback))
            return (ToApiUrl(cachedFallback, gameKey), "账号换取失败，已回退到游戏缓存 authkey");
        return (null, generated.Message);
    }

    public static async Task<(string? Url, string Message)> GenerateAuthKeyAsync(string gameKey, string cookie,
        string uid, string region)
    {
        if (!Games.TryGetValue(gameKey, out var game))
            return (null, "暂不支持该游戏");
        var mid = ExtractCookie(cookie, "account_mid_v2");
        if (mid.Length == 0) mid = ExtractCookie(cookie, "ltmid_v2");
        if (mid.Length == 0) mid = ExtractCookie(cookie, "mid");
        if (mid.Length == 0) mid = ExtractCookie(cookie, "stuid");
        if (mid.Length == 0) mid = ExtractCookie(cookie, "account_id_v2");
        if (mid.Length == 0) mid = ExtractCookie(cookie, "ltuid_v2");
        if (mid.Length == 0) mid = ExtractCookie(cookie, "account_id");
        if (mid.Length == 0) return (null, "Cookie 缺少 mid / stuid，请重新登录米游社");

        var body = JsonSerializer.Serialize(new
        {
            auth_appid = "webview_gacha",
            authkey_type = 2,
            game_biz = game.Biz,
            mid,
            region,
            uid,
        });
        var headers = new Dictionary<string, string>
        {
            ["user-agent"] = BbsUa,
            ["referer"] = "https://webstatic.mihoyo.com",
            ["cookie"] = cookie,
            ["x-rpc-app_version"] = "2.115.0",
            ["x-rpc-client_type"] = "5",
        };
        var api = await MiHoYoHttp.SendWithHeadersAsync("POST", GenAuthKeyUrl, body, headers);
        if (api.Retcode != 0 || api.Data is null)
            return (null, $"authkey 换取失败：{api.Message}（{api.Retcode}）");

        var data = api.Data.Value;
        var authkey = data.GetProp("authkey");
        if (string.IsNullOrWhiteSpace(authkey))
            return (null, "响应中没有 authkey");
        var ver = data.GetProp("authkey_ver");
        var signType = data.GetProp("sign_type");
        if (ver.Length == 0) ver = "1";
        if (signType.Length == 0) signType = "2";
        var url = game.ApiPrefix +
                  $"?authkey_ver={ver}&sign_type={signType}&auth_appid=webview_gacha&init_type=301" +
                  $"&lang=zh-cn&authkey={Uri.EscapeDataString(authkey)}&game_biz={game.Biz}" +
                  $"&region={Uri.EscapeDataString(region)}&uid={Uri.EscapeDataString(uid)}";
        return (url, "authkey 换取成功");
    }

    /// <summary>读取某个账号的某个游戏的全部卡池记录。</summary>
    public static async Task<(List<GachaRecord> Records, string Message)> FetchAllAsync(string gameKey, string uid,
        string apiUrl, CancellationToken ct)
    {
        var result = new List<GachaRecord>();
        if (!Games.TryGetValue(gameKey, out var game)) return (result, "暂不支持该游戏");
        var seen = new HashSet<string>();
        var error = "";

        foreach (var pool in game.Pools)
        {
            var codes = new List<string> { pool.Code };
            codes.AddRange(pool.ExtraCodes);
            foreach (var code in codes)
            {
                var page = await FetchPoolAsync(gameKey, uid, apiUrl, code, ct);
                if (!string.IsNullOrEmpty(page.Error) && error.Length == 0) error = page.Error;
                foreach (var record in page.Records)
                {
                    var key = $"{record.GameKey}|{record.Uid}|{record.Id}";
                    if (seen.Add(key)) result.Add(record);
                }
            }
        }

        var ordered = result
            .OrderByDescending(r => ParseTime(r.Time))
            .ThenByDescending(r => r.Id, StringComparer.Ordinal)
            .ToList();
        return (ordered, ordered.Count > 0 ? $"已获取 {ordered.Count} 条记录" : error.Length > 0 ? error : "该账号暂无抽卡记录");
    }

    private static async Task<(List<GachaRecord> Records, string Error)> FetchPoolAsync(string gameKey, string uid,
        string apiUrl, string gachaType, CancellationToken ct)
    {
        var records = new List<GachaRecord>();
        var endpoint = apiUrl;
        if (gameKey == "starrail" && gachaType is "21" or "22")
            endpoint = endpoint.Replace("/getGachaLog", "/getLdGachaLog", StringComparison.OrdinalIgnoreCase);

        var page = 1;
        var endId = "";
        var seen = new HashSet<string>();
        for (var guard = 0; guard < 600; guard++)
        {
            ct.ThrowIfCancellationRequested();
            var url = endpoint;
            url += (url.Contains('?') ? "&" : "?") + $"gacha_type={gachaType}&page={page}&size=20&lang=zh-cn";
            if (endId.Length > 0) url += "&end_id=" + Uri.EscapeDataString(endId);

            var api = await MiHoYoHttp.GetPlainAsync(url, null, BbsUa);
            if (api.Retcode == -110)
            {
                await Task.Delay(1200, ct);
                api = await MiHoYoHttp.GetPlainAsync(url, null, BbsUa);
                if (api.Retcode == -110)
                {
                    await Task.Delay(2400, ct);
                    api = await MiHoYoHttp.GetPlainAsync(url, null, BbsUa);
                }
            }
            if (api.Retcode != 0 || api.Data is null)
                return (records, $"{api.Message}（{api.Retcode}）");

            var data = api.Data.Value;
            if (!data.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
                return (records, "接口没有返回记录列表");

            var count = 0;
            var lastId = endId;
            foreach (var item in list.EnumerateArray())
            {
                count++;
                var id = item.GetProp("id");
                if (!seen.Add(id)) continue;
                lastId = id;
                var rank = item.GetProp("rank_type");
                if (gameKey == "zzz" && int.TryParse(rank, out var zzzRank) && zzzRank is >= 2 and <= 4)
                    rank = (zzzRank + 1).ToString();
                records.Add(new GachaRecord
                {
                    Uid = uid,
                    GameKey = gameKey,
                    GachaType = item.GetProp("gacha_type").Length > 0 ? item.GetProp("gacha_type") : gachaType,
                    ItemId = item.GetProp("item_id"),
                    Name = item.GetProp("name"),
                    RankType = rank,
                    ItemType = item.GetProp("item_type"),
                    Count = item.GetProp("count").Length > 0 ? item.GetProp("count") : "1",
                    Time = item.GetProp("time"),
                    Id = id,
                    PoolName = PoolName(gameKey, gachaType),
                });
            }
            if (count < 20) break;
            if (lastId == endId) break;
            endId = lastId;
            page++;
        }
        return (records, "");
    }

    private static DateTime ParseTime(string text)
        => DateTime.TryParse(text, out var value) ? value : DateTime.MinValue;

    private static string ExtractCookie(string cookie, string name)
    {
        foreach (var part in cookie.Split(';'))
        {
            var kv = part.Trim().Split('=');
            if (kv.Length == 2 && kv[0].Trim() == name) return kv[1].Trim();
        }
        return "";
    }

    /// <summary>把新拉取的记录合并进 MikoNest 抽卡库（不覆盖旧记录，空结果不写盘）。</summary>
    public static int MergeRecords(IEnumerable<GachaRecord> records)
    {
        var incoming = records.ToList();
        if (incoming.Count == 0) return 0;
        var existing = MikoNestBridge.LoadGachaRecords().ToList();
        var map = new Dictionary<string, GachaRecord>(StringComparer.Ordinal);
        foreach (var record in existing)
            map[$"{record.GameKey}|{record.Uid}|{record.Id}"] = record;
        var added = 0;
        foreach (var record in incoming)
        {
            var key = $"{record.GameKey}|{record.Uid}|{record.Id}";
            if (map.ContainsKey(key)) continue;
            map[key] = record;
            added++;
        }
        if (added == 0) return 0;

        try
        {
            var dir = MikoNestBridge.DataDir;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "gacha_records.json");
            if (File.Exists(file))
            {
                File.Copy(file, file + ".bak", true);
                File.Copy(file, file + ".lastgood", true);
            }
            var doc = new GachaRecordFile
            {
                Version = 1,
                Records = map.Values
                    .OrderByDescending(r => ParseTime(r.Time))
                    .ThenByDescending(r => r.Id, StringComparer.Ordinal)
                    .ToList(),
            };
            var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = false });
            var temp = file + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, file, true);
        }
        catch
        {
            return 0;
        }
        return added;
    }

    /// <summary>清理指定游戏 + 游戏 UID 的本地抽卡记录，清理前保留时间戳备份。</summary>
    public static int ClearRecords(string gameKey, string uid)
    {
        if (string.IsNullOrWhiteSpace(gameKey) || string.IsNullOrWhiteSpace(uid)) return -1;
        var existing = MikoNestBridge.LoadGachaRecords().ToList();
        var remaining = existing
            .Where(r => !(string.Equals(r.GameKey, gameKey, StringComparison.Ordinal) &&
                          string.Equals(r.Uid, uid, StringComparison.Ordinal)))
            .ToList();
        var removed = existing.Count - remaining.Count;
        if (removed <= 0) return 0;

        try
        {
            var dir = MikoNestBridge.DataDir;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "gacha_records.json");
            if (File.Exists(file))
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(file, Path.Combine(dir, $"gacha_records.{stamp}.bak"), true);
                File.Copy(file, file + ".bak", true);
                File.Copy(file, file + ".lastgood", true);
            }
            var doc = new GachaRecordFile
            {
                Version = 1,
                Records = remaining
                    .OrderByDescending(r => ParseTime(r.Time))
                    .ThenByDescending(r => r.Id, StringComparer.Ordinal)
                    .ToList(),
            };
            var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = false });
            var temp = file + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, file, true);
        }
        catch
        {
            return -1;
        }
        return removed;
    }
}
