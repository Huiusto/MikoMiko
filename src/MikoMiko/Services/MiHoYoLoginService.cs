using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MikoMiko.Services;

/// <summary>
/// 米游社（通行证 App）扫码登录：创建二维码 → 轮询状态 → 用 stoken 升级 v2 并换
/// cookie_token / ltoken → 组装 Cookie；校验改用现行可用的
/// binding/api/getUserGameRolesByCookie，避免再被已下线的 getUserFullInfo 判死。
/// </summary>
public sealed class MiHoYoLoginService
{
    private const string QrSalt = "xV8v4Qu54lUKrEYFZkJhB8cuOh9Asafs";
    private const string AppLoginSalt = "6s25p5ox5y14umn1p61aqyyvbvvl3lrt";
    private const string AppId = "bll8iq97cem8";
    private const string BbsVersion = "2.115.0";
    private static readonly string DeviceName = "MIKOMIKO" + RandomLetters(4);
    private static readonly string DeviceModel = RandomLetters(6);
    private static readonly string DeviceId = Guid.NewGuid().ToString("N");

    private readonly HttpClient _http = new(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    public sealed record QrLogin(string Url, string Ticket);

    public sealed record LoginResult(string Status, string Message, string? Cookie);

    // ---------------------------------------------------------------- 扫码

    public async Task<QrLogin?> CreateQrAsync()
    {
        var body = "{}";
        var res = await SendAsync(HttpMethod.Post,
            "https://passport-api.mihoyo.com/account/ma-cn-passport/app/createQRLogin",
            body, BuildQrHeaders(body)).ConfigureAwait(false);
        if (res is null || GetInt(res, "retcode") != 0) return null;
        var data = GetObject(res, "data");
        var url = GetString(data, "url");
        var ticket = GetString(data, "ticket");
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(ticket)) return null;
        return new QrLogin(url, ticket);
    }

    public async Task<LoginResult> QueryAsync(string ticket)
    {
        var body = "{\"ticket\":\"" + ticket + "\"}";
        var res = await SendAsync(HttpMethod.Post,
            "https://passport-api.mihoyo.com/account/ma-cn-passport/app/queryQRLoginStatus",
            body, BuildQrHeaders(body)).ConfigureAwait(false);
        if (res is null) return new LoginResult("Error", "网络异常，请重试", null);

        var code = GetInt(res, "retcode");
        if (code != 0) return new LoginResult("Error", GetString(res, "message"), null);
        var data = GetObject(res, "data");
        var status = GetString(data, "status");
        switch (status)
        {
            case "Init":
            case "Created":
                return new LoginResult("Waiting", "等待扫码…", null);
            case "Scanned":
                return new LoginResult("Scanned", "已扫码，请在手机上确认", null);
            case "Confirmed":
                var cookie = await BuildCookieAsync(data).ConfigureAwait(false);
                return cookie is null
                    ? new LoginResult("Error", "登录凭证解析失败，请重试", null)
                    : new LoginResult("Confirmed", "登录成功", cookie);
            default:
                return new LoginResult("Waiting", $"等待扫码…（{status}）", null);
        }
    }

    // ---------------------------------------------------------------- Cookie 组装

    /// <summary>
    /// 旧登录流程可能只拿到 ltoken 而缺 cookie_token。发布网页需要 cookie_token 才会识别登录态，
    /// 这里用已保存的 stoken 重新换一次网页 Cookie，失败时保留原值。
    /// </summary>
    public async Task<string?> RefreshWebCookieAsync(string cookie)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in cookie.Split(';'))
        {
            var index = part.IndexOf('=');
            if (index <= 0) continue;
            map[part[..index].Trim()] = part[(index + 1)..].Trim();
        }

        var aid = map.GetValueOrDefault("account_id") ?? map.GetValueOrDefault("ltuid") ?? map.GetValueOrDefault("stuid");
        var mid = map.GetValueOrDefault("mid") ?? map.GetValueOrDefault("ltmid_v2") ?? map.GetValueOrDefault("account_mid_v2");
        var stoken = map.GetValueOrDefault("stoken") ?? string.Empty;
        if (string.IsNullOrEmpty(aid) || string.IsNullOrEmpty(mid) || string.IsNullOrEmpty(stoken)) return null;

        var stokenV2 = map.GetValueOrDefault("stoken_v2") ?? string.Empty;
        if (string.IsNullOrEmpty(stokenV2))
            stokenV2 = await UpgradeStokenAsync(mid, aid, stoken).ConfigureAwait(false);

        var cookieToken = map.GetValueOrDefault("cookie_token") ?? map.GetValueOrDefault("cookie_token_v2") ?? string.Empty;
        if (string.IsNullOrEmpty(cookieToken))
            cookieToken = await GetCookieTokenAsync(mid, aid, stoken, stokenV2).ConfigureAwait(false);

        var ltoken = map.GetValueOrDefault("ltoken") ?? map.GetValueOrDefault("ltoken_v2") ?? string.Empty;
        if (string.IsNullOrEmpty(ltoken))
            ltoken = await GetLTokenAsync(mid, aid, stoken, stokenV2).ConfigureAwait(false);

        if (string.IsNullOrEmpty(cookieToken)) return null;

        var sb = new StringBuilder();
        sb.Append($"account_id={aid}; stuid={aid}; ltuid={aid}; mid={mid}; stoken={stoken}; ");
        if (!string.IsNullOrEmpty(ltoken)) sb.Append($"ltoken={ltoken}; ");
        sb.Append($"cookie_token={cookieToken}; ");
        sb.Append($"ltuid_v2={aid}; account_id_v2={aid}; account_mid_v2={mid}; ");
        if (!string.IsNullOrEmpty(stokenV2)) sb.Append($"stoken_v2={stokenV2}; ");
        if (!string.IsNullOrEmpty(ltoken)) sb.Append($"ltoken_v2={ltoken}; ");
        sb.Append($"cookie_token_v2={cookieToken}; ltmid_v2={mid}");
        return sb.ToString();
    }

    private async Task<string?> BuildCookieAsync(JsonElement data)
    {
        var user = GetObject(data, "user_info");
        var mid = GetString(user, "mid");
        var aid = GetString(user, "aid");
        var stoken = "";
        var tokens = data.TryGetProperty("tokens", out var list) && list.ValueKind == JsonValueKind.Array ? list : default;
        if (tokens.ValueKind == JsonValueKind.Array)
        {
            foreach (var token in tokens.EnumerateArray())
            {
                if (GetString(token, "token_type") == "1")
                {
                    stoken = GetString(token, "token");
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(stoken) || string.IsNullOrEmpty(aid)) return null;

        var stokenV2 = await UpgradeStokenAsync(mid, aid, stoken).ConfigureAwait(false);
        var cookieToken = await GetCookieTokenAsync(mid, aid, stoken, stokenV2).ConfigureAwait(false);
        var ltoken = await GetLTokenAsync(mid, aid, stoken, stokenV2).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.Append($"account_id={aid}; stuid={aid}; ltuid={aid}; mid={mid}; stoken={stoken}; ");
        if (!string.IsNullOrEmpty(ltoken)) sb.Append($"ltoken={ltoken}; ");
        if (!string.IsNullOrEmpty(cookieToken)) sb.Append($"cookie_token={cookieToken}; ");
        sb.Append($"ltuid_v2={aid}; account_id_v2={aid}; account_mid_v2={mid}; ");
        if (!string.IsNullOrEmpty(stokenV2)) sb.Append($"stoken_v2={stokenV2}; ");
        if (!string.IsNullOrEmpty(ltoken)) sb.Append($"ltoken_v2={ltoken}; ");
        if (!string.IsNullOrEmpty(cookieToken)) sb.Append($"cookie_token_v2={cookieToken}; ");
        sb.Append($"ltmid_v2={mid}");
        return sb.ToString();
    }

    private async Task<string> UpgradeStokenAsync(string mid, string aid, string stoken)
    {
        const string url = "https://passport-api.mihoyo.com/account/ma-cn-session/app/getTokenBySToken";
        var res = await SendAsync(HttpMethod.Post, url, "{}", BuildAppLoginHeaders(aid, mid, stoken)).ConfigureAwait(false);
        if (res is null || GetInt(res, "retcode") != 0) return "";
        var data = GetObject(res, "data");
        var token = GetObject(data, "token");
        var value = GetString(token, "token");
        return string.IsNullOrEmpty(value) ? GetString(data, "token") : value;
    }

    private async Task<string> GetCookieTokenAsync(string mid, string aid, string stoken, string stokenV2)
    {
        if (!string.IsNullOrEmpty(stokenV2))
        {
            var res = await SendAsync(HttpMethod.Get,
                "https://passport-api.mihoyo.com/account/auth/api/getCookieAccountInfoBySToken",
                null, BuildAppLoginHeaders(aid, mid, stokenV2)).ConfigureAwait(false);
            if (res is not null && GetInt(res, "retcode") == 0)
            {
                var token = GetString(GetObject(res, "data"), "cookie_token");
                if (!string.IsNullOrEmpty(token)) return token;
            }
        }

        var query = $"uid={Uri.EscapeDataString(aid)}&stoken={Uri.EscapeDataString(stoken)}";
        foreach (var url in new[]
                 {
                     $"https://api-takumi.mihoyo.com/auth/api/getCookieAccountInfoBySToken?{query}",
                     $"https://passport-api.mihoyo.com/account/auth/api/getCookieAccountInfoBySToken?{query}",
                 })
        {
            var res = await SendAsync(HttpMethod.Get, url, null, BuildPlainHeaders(aid, mid, stoken)).ConfigureAwait(false);
            if (res is not null && GetInt(res, "retcode") == 0)
            {
                var token = GetString(GetObject(res, "data"), "cookie_token");
                if (!string.IsNullOrEmpty(token)) return token;
            }
        }

        // 兜底：stoken → game_token → cookie_token
        var game = await SendAsync(HttpMethod.Get, "https://api-takumi.mihoyo.com/auth/api/getGameToken",
            null, BuildPlainHeaders(aid, mid, stoken)).ConfigureAwait(false);
        var gameToken = game is null ? "" : GetString(GetObject(game, "data"), "game_token");
        if (!string.IsNullOrEmpty(gameToken))
        {
            var url = $"https://api-takumi.mihoyo.com/auth/api/getCookieAccountInfoByGameToken?account_id={Uri.EscapeDataString(aid)}&game_token={Uri.EscapeDataString(gameToken)}";
            var res = await SendAsync(HttpMethod.Get, url, null, new Dictionary<string, string>
            {
                ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
                ["x-rpc-app_version"] = BbsVersion,
                ["x-rpc-client_type"] = "5",
                ["referer"] = "https://user.mihoyo.com/",
            }).ConfigureAwait(false);
            if (res is not null && GetInt(res, "retcode") == 0)
                return GetString(GetObject(res, "data"), "cookie_token");
        }

        return "";
    }

    private async Task<string> GetLTokenAsync(string mid, string aid, string stoken, string stokenV2)
    {
        if (!string.IsNullOrEmpty(stokenV2))
        {
            var res = await SendAsync(HttpMethod.Get,
                "https://passport-api.mihoyo.com/account/auth/api/getLTokenBySToken",
                null, BuildAppLoginHeaders(aid, mid, stokenV2)).ConfigureAwait(false);
            if (res is not null && GetInt(res, "retcode") == 0)
            {
                var token = GetString(GetObject(res, "data"), "ltoken");
                if (!string.IsNullOrEmpty(token)) return token;
            }
        }

        var query = $"uid={Uri.EscapeDataString(aid)}&stoken={Uri.EscapeDataString(stoken)}";
        foreach (var url in new[]
                 {
                     $"https://passport-api.mihoyo.com/account/auth/api/getLTokenBySToken?{query}",
                     $"https://api-takumi.mihoyo.com/auth/api/getLTokenBySToken?{query}",
                 })
        {
            var res = await SendAsync(HttpMethod.Get, url, null, BuildPlainHeaders(aid, mid, stoken)).ConfigureAwait(false);
            if (res is not null && GetInt(res, "retcode") == 0)
            {
                var token = GetString(GetObject(res, "data"), "ltoken");
                if (!string.IsNullOrEmpty(token)) return token;
            }
        }

        return "";
    }

    // ---------------------------------------------------------------- 资料

    /// <summary>校验 Cookie：现行可用端点，不依赖已 404 的 getUserFullInfo。</summary>
    public async Task<(bool Ok, string Nickname, string GameUid, string Message)> ValidateCookieAsync(string cookie)
    {
        var res = await SendAsync(HttpMethod.Get,
            "https://api-takumi.mihoyo.com/binding/api/getUserGameRolesByCookie?game_biz=hk4e_cn",
            null, new Dictionary<string, string>
            {
                ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
                ["x-rpc-app_version"] = BbsVersion,
                ["x-rpc-client_type"] = "5",
                ["x-requested-with"] = "com.mihoyo.hyperion",
                ["referer"] = "https://webstatic.mihoyo.com",
                ["cookie"] = cookie,
            }).ConfigureAwait(false);
        if (res is null) return (false, "", "", "网络异常");
        var code = GetInt(res, "retcode");
        if (code != 0) return (false, "", "", $"{GetString(res, "message")}（{code}）");

        var data = GetObject(res, "data");
        var roles = data.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array ? list : default;
        if (roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in roles.EnumerateArray())
            {
                var nick = GetString(role, "nickname");
                if (!string.IsNullOrEmpty(nick)) return (true, nick, GetString(role, "game_uid"), "ok");
            }
        }

        return (true, "米游社用户", "", "ok");
    }

    /// <summary>社区资料：昵称/签名/头像/等级/IP/粉丝/关注/获赞/发帖数。</summary>
    public async Task<UserStat?> GetUserStatAsync(string cookie, string uid)
    {
        var url = $"https://bbs-api.miyoushe.com/user/wapi/getUserFullInfo?uid={Uri.EscapeDataString(uid)}";
        var res = await SendAsync(HttpMethod.Get, url, null, new Dictionary<string, string>
        {
            ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
            ["referer"] = "https://www.miyoushe.com/",
            ["cookie"] = cookie,
        }).ConfigureAwait(false);
        if (res is null || GetInt(res, "retcode") != 0) return null;

        var info = GetObject(GetObject(res, "data"), "user_info");
        if (info.ValueKind != JsonValueKind.Object) return null;

        var achieve = GetObject(info, "achieve");
        var levelExp = GetObject(info, "level_exp");
        var stat = new UserStat
        {
            Uid = GetString(info, "uid"),
            Nickname = GetString(info, "nickname"),
            Avatar = GetString(info, "avatar_url"),
            Signature = GetString(info, "introduce"),
            IpRegion = GetString(info, "ip_region"),
            Level = GetInt(levelExp, "level"),
            Fans = GetString(achieve, "followed_cnt"),
            Following = GetString(achieve, "follow_cnt"),
            Likes = GetString(achieve, "like_num"),
            Posts = GetString(achieve, "post_num"),
        };
        return stat;
    }

    /// <summary>该账号在各游戏下的角色（原神/星铁/绝区零/崩坏3/崩坏学园2/未定）。</summary>
    public async Task<List<GameRole>> GetGameRolesAsync(string cookie)
    {
        var games = new (string Biz, string Name, string Accent, int Gid)[]
        {
            ("hk4e_cn", "原神", "#4EA4DD", 2),
            ("hkrpg_cn", "崩坏：星穹铁道", "#8B7BE8", 6),
            ("nap_cn", "绝区零", "#F5B94A", 8),
            ("bh3_cn", "崩坏3", "#E86A9A", 1),
            ("bh2_cn", "崩坏学园2", "#7EC8E3", 3),
            ("nxx_cn", "未定事件簿", "#C9A063", 4),
        };

        var result = new List<GameRole>();
        foreach (var game in games)
        {
            var url = $"https://api-takumi.mihoyo.com/binding/api/getUserGameRolesByCookie?game_biz={game.Biz}";
            var res = await SendAsync(HttpMethod.Get, url, null, new Dictionary<string, string>
            {
                ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
                ["x-rpc-app_version"] = BbsVersion,
                ["x-rpc-client_type"] = "5",
                ["x-requested-with"] = "com.mihoyo.hyperion",
                ["referer"] = "https://webstatic.mihoyo.com",
                ["cookie"] = cookie,
            }).ConfigureAwait(false);
            if (res is null || GetInt(res, "retcode") != 0) continue;

            var list = GetObject(res, "data").TryGetProperty("list", out var roles) && roles.ValueKind == JsonValueKind.Array
                ? roles : default;
            if (list.ValueKind != JsonValueKind.Array) continue;
            foreach (var role in list.EnumerateArray())
            {
                result.Add(new GameRole
                {
                    GameId = game.Gid,
                    GameBiz = game.Biz,
                    GameName = game.Name,
                    AccentHex = game.Accent,
                    Nickname = GetString(role, "nickname"),
                    GameUid = GetString(role, "game_uid"),
                    Server = GetString(role, "region_name"),
                    Level = GetInt(role, "level"),
                });
                break; // 每个游戏展示首个角色
            }
        }

        return result;
    }

    /// <summary>游戏战绩卡片：官方稳定的通用统计（原神活跃/成就/深渊等）。</summary>
    public async Task<List<GameRecordCard>> GetRecordCardsAsync(string cookie, string uid)
    {
        var result = new List<GameRecordCard>();
        if (string.IsNullOrWhiteSpace(uid)) return result;
        var query = $"uid={Uri.EscapeDataString(uid)}";
        const string salt = "xV8v4Qu54lUKrEYFZkJhB8cuOh9Asafs";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var r = RandomNumberGenerator.GetInt32(100000, 200000).ToString();
        var raw = $"salt={salt}&t={t}&r={r}&b=&q={query}";
        var ds = $"{t},{r},{Md5(raw)}";
        var url = $"https://api-takumi-record.mihoyo.com/game_record/card/wapi/getGameRecordCard?{query}";
        var res = await SendAsync(HttpMethod.Get, url, null, new Dictionary<string, string>
        {
            ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
            ["x-rpc-app_version"] = BbsVersion,
            ["x-rpc-client_type"] = "5",
            ["x-requested-with"] = "com.mihoyo.hyperion",
            ["referer"] = "https://webstatic.mihoyo.com",
            ["DS"] = ds,
            ["cookie"] = cookie,
        }).ConfigureAwait(false);
        if (res is null || GetInt(res, "retcode") != 0) return result;

        var data = GetObject(res, "data");
        if (!data.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array) return result;
        foreach (var card in list.EnumerateArray())
        {
            if (!card.TryGetProperty("has_role", out var hasRole) || hasRole.ValueKind != JsonValueKind.True) continue;
            var item = new GameRecordCard
            {
                GameId = GetInt(card, "game_id"),
                GameName = GetString(card, "game_name"),
                Nickname = GetString(card, "nickname"),
                RoleUid = GetString(card, "game_role_id"),
                RegionName = GetString(card, "region_name"),
                Level = GetInt(card, "level"),
                IsPublic = card.TryGetProperty("is_public", out var pub) && pub.ValueKind == JsonValueKind.True,
            };
            if (card.TryGetProperty("data", out var stats) && stats.ValueKind == JsonValueKind.Array)
            {
                foreach (var stat in stats.EnumerateArray())
                {
                    var label = GetString(stat, "name");
                    var value = GetString(stat, "value");
                    if (string.IsNullOrWhiteSpace(label)) continue;
                    item.Stats.Add(new GameRecordStat { Label = label, Value = value });
                }
            }
            result.Add(item);
        }

        return result;
    }

    public sealed class GameRecordCard
    {
        public int GameId { get; set; }
        public string GameName { get; set; } = "";
        public string Nickname { get; set; } = "";
        public string RoleUid { get; set; } = "";
        public string RegionName { get; set; } = "";
        public int Level { get; set; }
        public bool IsPublic { get; set; }
        public List<GameRecordStat> Stats { get; } = new();
    }

    public sealed class GameRecordStat
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public sealed class UserStat
    {
        public string Uid { get; set; } = "";
        public string Nickname { get; set; } = "";
        public string Avatar { get; set; } = "";
        public string Signature { get; set; } = "";
        public string IpRegion { get; set; } = "";
        public int Level { get; set; }
        public string Fans { get; set; } = "0";
        public string Following { get; set; } = "0";
        public string Likes { get; set; } = "0";
        public string Posts { get; set; } = "0";
    }

    public sealed class GameRole
    {
        public int GameId { get; set; }
        public string GameBiz { get; set; } = "";
        public string GameName { get; set; } = "";
        public string AccentHex { get; set; } = "#4EA4DD";
        public string Nickname { get; set; } = "";
        public string GameUid { get; set; } = "";
        public string Server { get; set; } = "";
        public int Level { get; set; }
    }

    // ---------------------------------------------------------------- 请求头 / 工具

    private Dictionary<string, string> BuildQrHeaders(string body)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var r = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var raw = $"salt={QrSalt}&t={t}&r={r}&b={body}&q=";
        var ds = $"{t},{r},{Md5(raw)}";
        return new Dictionary<string, string>
        {
            ["user-agent"] = $"Mozilla/5.0 miHoYoBBS/{BbsVersion} Capture/2.2.0",
            ["content-type"] = "application/json",
            ["x-rpc-client_type"] = "3",
            ["x-rpc-app_version"] = BbsVersion,
            ["x-rpc-sdk_version"] = BbsVersion,
            ["x-rpc-account_version"] = BbsVersion,
            ["x-rpc-app_id"] = AppId,
            ["x-rpc-game_biz"] = "bbs_cn",
            ["x-rpc-device_id"] = DeviceId,
            ["x-rpc-device_name"] = DeviceName,
            ["x-rpc-device_model"] = DeviceModel,
            ["DS"] = ds,
            ["referer"] = "https://app.mihoyo.com",
        };
    }

    private Dictionary<string, string> BuildAppLoginHeaders(string aid, string mid, string stoken)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var r = RandomNumberGenerator.GetInt32(100000, 199999).ToString();
        var ds = $"{t},{r},{Md5($"salt={AppLoginSalt}&t={t}&r={r}")}";
        return new Dictionary<string, string>
        {
            ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
            ["accept"] = "application/json",
            ["x-rpc-app_version"] = BbsVersion,
            ["x-rpc-client_type"] = "2",
            ["x-rpc-app_id"] = AppId,
            ["x-rpc-device_id"] = DeviceId,
            ["x-rpc-device_name"] = DeviceName,
            ["x-rpc-device_model"] = DeviceModel,
            ["x-requested-with"] = "com.mihoyo.hyperion",
            ["referer"] = "https://user.mihoyo.com/",
            ["DS"] = ds,
            ["cookie"] = $"stuid={aid}; ltuid={aid}; account_id={aid}; mid={mid}; stoken={stoken}; ltuid_v2={aid}; ltmid_v2={mid};",
        };
    }

    private static Dictionary<string, string> BuildPlainHeaders(string aid, string mid, string stoken) => new()
    {
        ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
        ["x-rpc-app_version"] = BbsVersion,
        ["x-rpc-client_type"] = "5",
        ["x-rpc-app_id"] = AppId,
        ["x-requested-with"] = "com.mihoyo.hyperion",
        ["referer"] = "https://user.mihoyo.com/",
        ["cookie"] = $"stuid={aid}; ltuid={aid}; account_id={aid}; mid={mid}; stoken={stoken};",
    };

    private async Task<JsonElement?> SendAsync(HttpMethod method, string url, string? body,
        Dictionary<string, string> headers)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            foreach (var header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static string Md5(string raw) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    private static string RandomLetters(int count)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = RandomNumberGenerator.GetBytes(count);
        var builder = new StringBuilder(count);
        foreach (var b in bytes) builder.Append(chars[b % chars.Length]);
        return builder.ToString();
    }

    private static JsonElement GetObject(JsonElement? element, string name)
    {
        if (element is null) return default;
        return element.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value : default;
    }

    private static JsonElement GetObject(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Object ? value : default;

    private static string GetString(JsonElement? element, string name)
    {
        if (element is null) return "";
        return GetString(element.Value, name);
    }

    private static string GetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            _ => "",
        };
    }

    private static int GetInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number) return value.GetInt32();
        if (value.ValueKind == JsonValueKind.String)
        {
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
            return parsed;
        }
        return 0;
    }

    private static int GetInt(JsonElement? element, string name) =>
        element is null ? 0 : GetInt(element.Value, name);
}
