using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MikoMiko.Models;
using MikoMiko.Services;

namespace MikoMiko.Services;

/// <summary>
/// MikoMiko 自己的米游社多账号服务：扫码添加、Cookie 添加、切换与本地持久化。
/// Cookie 使用当前 Windows 用户的 DPAPI 加密，首次运行会合并 MikoNest 已有账号。
/// </summary>
public static class HoyoAccountService
{
    private static readonly object Gate = new();
    private static readonly List<HoyoAccount> Items = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MikoMiko");

    public static string AccountsFile => Path.Combine(DataDir, "hoyo_accounts.json");

    public static IReadOnlyList<HoyoAccount> All { get { lock (Gate) return Items.ToList(); } }

    public static HoyoAccount? Active
    {
        get
        {
            lock (Gate)
                return Items.FirstOrDefault(a => a.IsActive) ?? Items.FirstOrDefault();
        }
    }

    public static event Action? Changed;

    static HoyoAccountService() => Reload();

    public static void Reload()
    {
        lock (Gate)
        {
            Items.Clear();
            try
            {
                if (File.Exists(AccountsFile))
                {
                    var json = File.ReadAllText(AccountsFile);
                    var list = JsonSerializer.Deserialize<List<HoyoAccount>>(json) ?? new List<HoyoAccount>();
                    foreach (var account in list)
                    {
                        account.Cookie = Unprotect(account.CookieEncrypted) ?? "";
                        if (!string.IsNullOrWhiteSpace(account.Cookie) || !string.IsNullOrWhiteSpace(account.Uid))
                            Items.Add(account);
                    }
                }
                else
                {
                    // 首次运行：把 MikoNest 已登录账号迁入，避免用户重复扫码。
                    MikoNestBridge.Reload();
                    foreach (var source in MikoNestBridge.Accounts)
                    {
                        if (string.IsNullOrWhiteSpace(source.Cookie)) continue;
                        Items.Add(new HoyoAccount
                        {
                            Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id,
                            Nickname = source.Nickname,
                            Uid = source.Uid,
                            AvatarUrl = source.AvatarUrl,
                            Cookie = source.Cookie,
                            CookieEncrypted = Protect(source.Cookie) ?? "",
                            IsActive = source.IsActive,
                            LastLogin = source.LastLogin,
                        });
                    }
                    if (Items.Count > 0)
                    {
                        if (!Items.Any(a => a.IsActive)) Items[0].IsActive = true;
                        SaveInternal();
                    }
                }

                if (Items.Count > 0 && !Items.Any(a => a.IsActive))
                    Items[0].IsActive = true;
            }
            catch
            {
                // 账号文件损坏时不阻断页面；用户可重新添加。
            }
        }
        Changed?.Invoke();
    }

    public static bool SetActive(string uidOrId)
    {
        lock (Gate)
        {
            var target = Items.FirstOrDefault(a => a.Uid == uidOrId || a.Id == uidOrId);
            if (target is null) return false;
            foreach (var item in Items) item.IsActive = ReferenceEquals(item, target);
            SaveInternal();
        }
        Changed?.Invoke();
        return true;
    }

    public static void Remove(string uidOrId)
    {
        lock (Gate)
        {
            var target = Items.FirstOrDefault(a => a.Uid == uidOrId || a.Id == uidOrId);
            if (target is null) return;
            Items.Remove(target);
            if (target.IsActive && Items.Count > 0) Items[0].IsActive = true;
            SaveInternal();
        }
        Changed?.Invoke();
    }

    public static HoyoAccount AddOrUpdate(string cookie, string nickname, string uid, string avatarUrl)
    {
        lock (Gate)
        {
            var existing = Items.FirstOrDefault(a => !string.IsNullOrEmpty(uid) && a.Uid == uid);
            if (existing is null)
            {
                existing = new HoyoAccount { Id = Guid.NewGuid().ToString("N") };
                Items.Add(existing);
            }
            existing.Nickname = string.IsNullOrWhiteSpace(nickname) ? existing.Nickname : nickname;
            existing.Uid = string.IsNullOrWhiteSpace(uid) ? existing.Uid : uid;
            existing.AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? existing.AvatarUrl : avatarUrl;
            existing.Cookie = cookie;
            existing.CookieEncrypted = Protect(cookie) ?? "";
            existing.LastLogin = DateTime.Now;
            foreach (var item in Items) item.IsActive = ReferenceEquals(item, existing);
            SaveInternal();
            Changed?.Invoke();
            return existing;
        }
    }

    /// <summary>合并 MikoNest 中的全部账号；返回新增数量。</summary>
    public static int ImportFromMikoNest()
    {
        MikoNestBridge.Reload();
        var added = 0;
        lock (Gate)
        {
            foreach (var source in MikoNestBridge.Accounts)
            {
                if (string.IsNullOrWhiteSpace(source.Cookie)) continue;
                if (Items.Any(a => !string.IsNullOrEmpty(source.Uid) && a.Uid == source.Uid)) continue;
                Items.Add(new HoyoAccount
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Nickname = source.Nickname,
                    Uid = source.Uid,
                    AvatarUrl = source.AvatarUrl,
                    Cookie = source.Cookie,
                    CookieEncrypted = Protect(source.Cookie) ?? "",
                    LastLogin = source.LastLogin,
                });
                added++;
            }
            if (Items.Count > 0 && !Items.Any(a => a.IsActive)) Items[0].IsActive = true;
            if (added > 0) SaveInternal();
        }
        if (added > 0) Changed?.Invoke();
        return added;
    }

    private static void SaveInternal()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var copy = Items.Select(a => new HoyoAccount
            {
                Id = a.Id,
                Nickname = a.Nickname,
                Uid = a.Uid,
                AvatarUrl = a.AvatarUrl,
                CookieEncrypted = Protect(a.Cookie) ?? a.CookieEncrypted,
                IsActive = a.IsActive,
                LastLogin = a.LastLogin,
            }).ToList();
            File.WriteAllText(AccountsFile, JsonSerializer.Serialize(copy, JsonOptions));
        }
        catch
        {
            // 保存失败不阻断当前会话。
        }
    }

    // ---------- 加密 ----------
    private static string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        try
        {
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }
        catch { return null; }
    }

    private static string? Unprotect(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;
        try
        {
            var data = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch { return null; }
    }

    // ---------- 扫码登录 ----------
    private const string X4Salt = "xV8v4Qu54lUKrEYFZkJhB8cuOh9Asafs";
    private const string AppLoginSalt = "6s25p5ox5y14umn1p61aqyyvbvvl3lrt";
    private const string ExchangeSalt = "JwYDpKvLj6MrMqqYU6jTKF17KNO2PXoS";
    private const string AppId = "bll8iq97cem8";
    private const string BbsVersion = "2.115.0";
    private const string CaptchaVersion = "2.119.0";
    private const string PassportSdkVersion = "2.44.0";
    private const string MobileUa = "Mozilla/5.0 (Linux; Android 12) Mobile miHoYoBBS/2.119.0";
    private const string DeviceName = "MIKOMIKO0135";
    private const string DeviceModel = "MM0135";
    private static readonly string LifecycleId = Guid.NewGuid().ToString("N");
    private static readonly Random Random = new();

    private static string DeviceId => MikoNestBridge.DeviceId;

    private static string RandomAlnum(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++) sb.Append(chars[Random.Next(chars.Length)]);
        return sb.ToString();
    }

    private static string GetDsX4(string bodyJson)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var r = Random.Next(100000, 200000).ToString();
        var hash = $"salt={X4Salt}&t={t}&r={r}&b={bodyJson}&q=";
        return $"{t},{r},{MiHoYoHttp.Md5(hash)}";
    }

    private static Dictionary<string, string> BuildQrHeaders(string bodyJson) => new()
    {
        ["user-agent"] = $"Mozilla/5.0 miHoYoBBS/{CaptchaVersion} Capture/2.2.0",
        ["accept"] = "*/*",
        ["accept-language"] = "zh-cn",
        ["x-rpc-client_type"] = "3",
        ["x-rpc-app_version"] = CaptchaVersion,
        ["x-rpc-sdk_version"] = CaptchaVersion,
        ["x-rpc-account_version"] = CaptchaVersion,
        ["x-rpc-app_id"] = AppId,
        ["x-rpc-game_biz"] = "bbs_cn",
        ["x-rpc-device_id"] = DeviceId,
        ["x-rpc-device_fp"] = "",
        ["x-rpc-device_name"] = DeviceName,
        ["x-rpc-device_model"] = DeviceModel,
        ["DS"] = GetDsX4(bodyJson),
        ["content-type"] = "application/json",
        ["referer"] = "https://app.mihoyo.com",
    };

    public static async Task<(bool ok, string message, string? qrPayload, string? ticket)> CreateQrAsync()
    {
        var api = await MiHoYoHttp.SendWithHeadersAsync("POST",
            "https://passport-api.mihoyo.com/account/ma-cn-passport/app/createQRLogin", "{}", BuildQrHeaders("{}"));
        if (api.Retcode == 0 && api.Data is { } data)
            return (true, "ok", data.GetProp("url"), data.GetProp("ticket"));
        return (false, $"{api.Message}（{api.Retcode}）", null, null);
    }

    public static async Task<(bool ok, string message, string? cookie)> PollQrAsync(string ticket)
    {
        var body = JsonSerializer.Serialize(new { ticket });
        var api = await MiHoYoHttp.SendWithHeadersAsync("POST",
            "https://passport-api.mihoyo.com/account/ma-cn-passport/app/queryQRLoginStatus", body, BuildQrHeaders(body));
        if (api.Retcode != 0) return (false, $"{api.Message}（{api.Retcode}）", null);
        if (api.Data is not { } data) return (false, "无数据", null);
        return data.GetProp("status") switch
        {
            "Init" or "Created" => (false, "等待扫码…", null),
            "Scanned" => (false, "已扫码，请在手机上确认", null),
            "Confirmed" => await BuildCookieFromLoginDataAsync(data, api.SetCookies),
            var status => (false, $"未知状态：{status}", null),
        };
    }

    private static bool IsStokenToken(JsonElement token)
    {
        if (!token.TryGetProperty("token_type", out var type)) return false;
        var isStoken = type.ValueKind == JsonValueKind.Number && type.TryGetInt32(out var n) && n == 1
                       || type.ValueKind == JsonValueKind.String && type.GetString() == "1";
        return isStoken && token.TryGetProperty("token", out var value) &&
               !string.IsNullOrEmpty(value.GetString());
    }

    private static async Task<(bool ok, string message, string? cookie)> BuildCookieFromLoginDataAsync(
        JsonElement data, IReadOnlyList<string>? setCookies = null)
    {
        var cookies = ParseSetCookies(setCookies);
        var mid = "";
        var aid = "";
        var accountName = "";
        if (data.TryGetProperty("user_info", out var userInfo))
        {
            mid = userInfo.GetProp("mid");
            aid = userInfo.GetProp("aid");
            accountName = userInfo.GetProp("account_name");
            if (string.IsNullOrEmpty(accountName)) accountName = userInfo.GetProp("nickname");
            if (string.IsNullOrEmpty(accountName)) accountName = userInfo.GetProp("name");
        }
        if (string.IsNullOrEmpty(aid)) aid = MapGet(cookies, "account_id", "account_id_v2", "ltuid", "ltuid_v2", "stuid");
        if (string.IsNullOrEmpty(mid)) mid = MapGet(cookies, "mid", "ltmid_v2", "account_mid_v2");
        if (string.IsNullOrEmpty(accountName)) accountName = MapGet(cookies, "account_name");

        string? stoken = null;
        var stokenV2 = MapGet(cookies, "stoken_v2");
        if (data.TryGetProperty("tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Array)
        {
            JsonElement? first = null;
            foreach (var token in tokens.EnumerateArray())
            {
                first ??= token;
                if (IsStokenToken(token)) { stoken = token.GetProp("token"); break; }
            }
            if (string.IsNullOrEmpty(stoken) && first is { } ft) stoken = ft.GetProp("token");
        }
        else if (data.TryGetProperty("token", out var tokenObject) && tokenObject.ValueKind == JsonValueKind.Object)
        {
            stoken = tokenObject.GetProp("token");
        }
        if (string.IsNullOrEmpty(stoken)) stoken = MapGet(cookies, "stoken", "stoken_v2");
        if (string.IsNullOrEmpty(stoken) || string.IsNullOrEmpty(aid) || string.IsNullOrEmpty(mid))
            return (false, "登录凭证解析失败", null);

        var cookieToken = MapGet(cookies, "cookie_token", "cookie_token_v2");
        var ltoken = MapGet(cookies, "ltoken", "ltoken_v2");
        if (string.IsNullOrEmpty(stokenV2))
        {
            try { stokenV2 = await UpgradeStokenV2Async(mid, aid, stoken); } catch { }
        }
        string? tokenError = null;
        if (string.IsNullOrEmpty(cookieToken))
        {
            try
            {
                var result = await GetCookieTokenAsync(mid, aid, stoken, stokenV2);
                cookieToken = result.token;
                tokenError = result.error;
            }
            catch (Exception ex) { tokenError = ex.Message; }
        }
        if (string.IsNullOrEmpty(ltoken))
        {
            try { ltoken = await GetLTokenAsync(mid, aid, stoken, stokenV2); } catch { }
        }
        if (!IsValidToken(cookieToken)) cookieToken = "";
        if (!IsValidToken(ltoken)) ltoken = "";

        var built = AssembleCookie(aid, mid, stoken, ltoken, cookieToken, accountName, stokenV2);
        var message = string.IsNullOrEmpty(cookieToken)
            ? $"登录成功（stoken 已就绪，cookie_token 暂未获取：{tokenError ?? "接口未返回"}）"
            : "登录成功";
        return (true, message, built);
    }

    private static string AssembleCookie(string aid, string mid, string stoken, string ltoken,
        string cookieToken, string accountName, string stokenV2)
    {
        var sb = new StringBuilder();
        sb.Append($"account_id={aid}; stuid={aid}; ltuid={aid}; mid={mid}; stoken={stoken}; ");
        if (!string.IsNullOrEmpty(accountName)) sb.Append($"account_name={accountName}; ");
        if (!string.IsNullOrEmpty(ltoken)) sb.Append($"ltoken={ltoken}; ");
        if (!string.IsNullOrEmpty(cookieToken)) sb.Append($"cookie_token={cookieToken}; ");
        sb.Append($"ltuid_v2={aid}; account_id_v2={aid}; account_mid_v2={mid}; ");
        if (!string.IsNullOrEmpty(stokenV2)) sb.Append($"stoken_v2={stokenV2}; ");
        if (!string.IsNullOrEmpty(ltoken)) sb.Append($"ltoken_v2={ltoken}; ");
        if (!string.IsNullOrEmpty(cookieToken)) sb.Append($"cookie_token_v2={cookieToken}; ");
        sb.Append($"ltmid_v2={mid};");
        return sb.ToString().TrimEnd(' ', ';');
    }

    private static async Task<string> UpgradeStokenV2Async(string mid, string aid, string stokenV1)
    {
        var headers = BuildAppLoginHeaders(aid, mid, $"stoken={stokenV1}", "{}");
        var api = await MiHoYoHttp.SendWithHeadersAsync("POST",
            "https://passport-api.mihoyo.com/account/ma-cn-session/app/getTokenBySToken", "{}", headers);
        if (api.Retcode != 0 || api.Data is not { } data) return "";
        return data.GetStringProp("token");
    }

    private static async Task<string> ExchangeTokenAsync(string stoken, string mid, int dstTokenType)
    {
        if (string.IsNullOrEmpty(stoken) || string.IsNullOrEmpty(mid)) return "";
        var body = JsonSerializer.Serialize(new
        {
            dst_token_type = dstTokenType,
            src_token = new { token = stoken, token_type = 1 },
            mid,
        });
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var r = RandomAlnum(6);
        var headers = new Dictionary<string, string>
        {
            ["accept"] = "application/json",
            ["content-type"] = "application/json",
            ["DS"] = $"{t},{r},{MiHoYoHttp.Md5($"salt={ExchangeSalt}&t={t}&r={r}&b={body}&q=")}",
            ["user-agent"] = MobileUa,
            ["x-rpc-app_version"] = CaptchaVersion,
            ["x-rpc-client_type"] = "2",
            ["x-rpc-app_id"] = AppId,
            ["x-rpc-game_biz"] = "bbs_cn",
            ["x-rpc-sys_version"] = "12",
            ["x-rpc-sdk_version"] = PassportSdkVersion,
            ["x-rpc-account_version"] = PassportSdkVersion,
            ["x-rpc-lifecycle_id"] = LifecycleId,
            ["x-rpc-device_id"] = DeviceId,
            ["x-rpc-device_name"] = DeviceName,
            ["x-rpc-device_model"] = DeviceModel,
        };
        var api = await MiHoYoHttp.SendWithHeadersAsync("POST",
            "https://passport-api.mihoyo.com/account/ma-cn-session/app/exchange", body, headers);
        if (api.Retcode != 0 || api.Data is not { } data) return "";
        return data.GetStringProp("token");
    }

    private static async Task<(string token, string error)> GetCookieTokenAsync(
        string mid, string aid, string stokenV1, string stokenV2)
    {
        var lastError = "";
        try
        {
            var exchanged = await ExchangeTokenAsync(stokenV1, mid, 4);
            if (!string.IsNullOrEmpty(exchanged)) return (exchanged, "");
            lastError = "exchange 未返回 cookie_token";
        }
        catch (Exception ex) { lastError = ex.Message; }

        if (!string.IsNullOrEmpty(stokenV2))
        {
            try
            {
                var headers = BuildAppLoginHeaders(aid, mid, $"stoken={stokenV2}", "");
                var api = await MiHoYoHttp.SendWithHeadersAsync("GET",
                    "https://passport-api.mihoyo.com/account/auth/api/getCookieAccountInfoBySToken", null, headers);
                if (api.Retcode == 0 && api.Data is { } data)
                {
                    var token = data.GetProp("cookie_token");
                    if (!string.IsNullOrEmpty(token)) return (token, "");
                    lastError = "接口返回成功但没有 cookie_token";
                }
                else lastError = $"{api.Message}（{api.Retcode}）";
            }
            catch (Exception ex) { lastError = ex.Message; }
        }

        var query = $"uid={Uri.EscapeDataString(aid)}&stoken={Uri.EscapeDataString(stokenV1)}";
        foreach (var baseUrl in new[]
                 {
                     "https://api-takumi.mihoyo.com/auth/api/getCookieAccountInfoBySToken",
                     "https://passport-api.mihoyo.com/account/auth/api/getCookieAccountInfoBySToken",
                 })
        {
            try
            {
                var api = await MiHoYoHttp.SendWithHeadersAsync("GET",
                    $"{baseUrl}?{query}", null, BuildPlainStokenHeaders(mid, aid, stokenV1));
                if (api.Retcode == 0 && api.Data is { } data)
                {
                    var token = data.GetProp("cookie_token");
                    if (!string.IsNullOrEmpty(token)) return (token, "");
                    lastError = "接口返回成功但没有 cookie_token";
                }
                else lastError = $"{api.Message}（{api.Retcode}）";
            }
            catch (Exception ex) { lastError = ex.Message; }
        }
        return ("", lastError);
    }

    private static async Task<string> GetLTokenAsync(string mid, string aid, string stokenV1, string stokenV2)
    {
        try
        {
            var exchanged = await ExchangeTokenAsync(stokenV1, mid, 2);
            if (!string.IsNullOrEmpty(exchanged)) return exchanged;
        }
        catch { }

        if (!string.IsNullOrEmpty(stokenV2))
        {
            try
            {
                var headers = BuildAppLoginHeaders(aid, mid, $"stoken={stokenV2}", "");
                var api = await MiHoYoHttp.SendWithHeadersAsync("GET",
                    "https://passport-api.mihoyo.com/account/auth/api/getLTokenBySToken", null, headers);
                if (api.Retcode == 0 && api.Data is { } data)
                {
                    var token = data.GetProp("ltoken");
                    if (!string.IsNullOrEmpty(token)) return token;
                }
            }
            catch { }
        }

        var query = $"uid={Uri.EscapeDataString(aid)}&stoken={Uri.EscapeDataString(stokenV1)}";
        foreach (var baseUrl in new[]
                 {
                     "https://passport-api.mihoyo.com/account/auth/api/getLTokenBySToken",
                     "https://api-takumi.mihoyo.com/auth/api/getLTokenBySToken",
                 })
        {
            try
            {
                var api = await MiHoYoHttp.SendWithHeadersAsync("GET",
                    $"{baseUrl}?{query}", null, BuildPlainStokenHeaders(mid, aid, stokenV1));
                if (api.Retcode == 0 && api.Data is { } data)
                {
                    var token = data.GetProp("ltoken");
                    if (!string.IsNullOrEmpty(token)) return token;
                }
            }
            catch { }
        }
        return "";
    }

    private static Dictionary<string, string> BuildPlainStokenHeaders(string mid, string aid, string stoken) => new()
    {
        ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
        ["x-rpc-app_version"] = BbsVersion,
        ["x-rpc-client_type"] = "5",
        ["x-rpc-app_id"] = AppId,
        ["x-requested-with"] = "com.mihoyo.hyperion",
        ["referer"] = "https://user.mihoyo.com/",
        ["cookie"] = $"stuid={aid}; ltuid={aid}; account_id={aid}; mid={mid}; stoken={stoken};",
    };

    private static Dictionary<string, string> BuildAppLoginHeaders(string aid, string mid, string stokenCookie, string body)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var r = Random.Next(100000, 200000).ToString();
        var hash = $"salt={AppLoginSalt}&t={t}&r={r}";
        return new Dictionary<string, string>
        {
            ["user-agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/{BbsVersion}",
            ["accept"] = "application/json",
            ["x-rpc-app_version"] = BbsVersion,
            ["x-rpc-client_type"] = "2",
            ["x-rpc-app_id"] = AppId,
            ["x-rpc-device_id"] = DeviceId,
            ["x-rpc-device_fp"] = "",
            ["x-rpc-device_name"] = DeviceName,
            ["x-rpc-device_model"] = DeviceModel,
            ["x-requested-with"] = "com.mihoyo.hyperion",
            ["referer"] = "https://user.mihoyo.com/",
            ["DS"] = $"{t},{r},{MiHoYoHttp.Md5(hash)}",
            ["cookie"] = $"stuid={aid}; ltuid={aid}; account_id={aid}; mid={mid}; {stokenCookie}; ltuid_v2={aid}; ltmid_v2={mid};",
        };
    }

    private static Dictionary<string, string> ParseSetCookies(IReadOnlyList<string>? setCookies)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (setCookies is null) return map;
        foreach (var value in setCookies)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var first = value.Split(';')[0];
            var eq = first.IndexOf('=');
            if (eq <= 0) continue;
            map[first[..eq].Trim()] = first[(eq + 1)..].Trim();
        }
        return map;
    }

    private static string MapGet(Dictionary<string, string> map, params string[] keys)
    {
        foreach (var key in keys)
            if (map.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)) return value;
        return "";
    }

    private static bool IsValidToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return !value.Any(ch => ch is '{' or '}' or '"' or '\n' or '\r');
    }

    private static string CookieValue(string cookie, string key)
    {
        if (string.IsNullOrEmpty(cookie)) return "";
        foreach (var segment in cookie.Split(';'))
        {
            var text = segment.Trim();
            var eq = text.IndexOf('=');
            if (eq > 0 && text[..eq].Trim() == key) return text[(eq + 1)..].Trim();
        }
        return "";
    }

    // ---------- Cookie 登录 ----------
    public static string NormalizeCookie(string raw)
    {
        var text = (raw ?? "").Trim();
        if (text.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
            text = text[7..].Trim();
        return string.Join("; ", text
            .Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Trim(';'))
            .Where(s => s.Contains('=')));
    }

    public static async Task<(bool ok, string message, string nickname, string uid, string avatar)> AddByCookieAsync(string rawCookie)
    {
        var cookie = NormalizeCookie(rawCookie);
        if (string.IsNullOrEmpty(cookie)) return (false, "请粘贴完整的米游社 Cookie", "", "", "");
        if (string.IsNullOrEmpty(CookieValue(cookie, "stoken")))
            return (false, "Cookie 缺少 stoken。请从米游社 App 的登录抓包中复制完整 Cookie，或改用扫码登录。", "", "", "");

        var identity = await ResolveIdentityAsync(cookie);
        if (string.IsNullOrEmpty(identity.uid))
            return (false, "登录态校验失败，Cookie 可能已过期或不完整。", "", "", "");

        AddOrUpdate(cookie, identity.nickname, identity.uid, identity.avatar);
        return (true, "账号已添加", identity.nickname, identity.uid, identity.avatar);
    }

    private static async Task<(string nickname, string uid, string avatar)> ResolveIdentityAsync(string cookie)
    {
        var aid = CookieValue(cookie, "account_id");
        if (string.IsNullOrEmpty(aid)) aid = CookieValue(cookie, "stuid");
        if (string.IsNullOrEmpty(aid)) aid = CookieValue(cookie, "ltuid");
        try
        {
            var headers = new Dictionary<string, string>
            {
                ["user-agent"] = MiHoYoHttp.UserAgent,
                ["referer"] = "https://www.miyoushe.com/",
                ["cookie"] = cookie,
            };
            var api = await MiHoYoHttp.SendWithHeadersAsync("GET",
                $"https://bbs-api.miyoushe.com/user/wapi/getUserFullInfo?uid={Uri.EscapeDataString(aid)}",
                null, headers);
            if (api.Retcode == 0 && api.Data is { } data &&
                data.TryGetProperty("user_info", out var userInfo) &&
                userInfo.ValueKind == JsonValueKind.Object)
            {
                var nickname = userInfo.GetProp("nickname");
                if (string.IsNullOrEmpty(nickname)) nickname = userInfo.GetProp("account_name");
                var uid = userInfo.GetProp("uid");
                if (string.IsNullOrEmpty(uid)) uid = aid;
                var avatar = userInfo.GetProp("avatar_url");
                if (string.IsNullOrEmpty(nickname)) nickname = CookieValue(cookie, "account_name");
                return (string.IsNullOrEmpty(nickname) ? "米游社用户" : nickname, uid, avatar);
            }
        }
        catch { }
        var fallbackName = CookieValue(cookie, "account_name");
        return (string.IsNullOrEmpty(fallbackName) ? "米游社用户" : fallbackName, aid, "");
    }
}
