using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MikoMiko.Services;

/// <summary>米游社消息中心（回复 / @ / 点赞 / 系统通知）接口，使用当前登录 Cookie。</summary>
public sealed class MiyousheMessageService
{
    private const string BaseApi = "https://bbs-api.miyoushe.com/notification/wapi";
    private const string X4Salt = "xV8v4Qu54lUKrEYFZkJhB8cuOh9Asafs";
    private static readonly string DeviceId = Guid.NewGuid().ToString("N");

    private readonly HttpClient _http;
    private readonly string _cookie;

    public MiyousheMessageService(string cookie)
    {
        _cookie = cookie ?? string.Empty;
        _http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(18)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://www.miyoushe.com/");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://www.miyoushe.com");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        if (!string.IsNullOrWhiteSpace(_cookie))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", _cookie);
    }

    public bool HasCookie => !string.IsNullOrWhiteSpace(_cookie);

    public async Task<Dictionary<string, long>> GetUnreadCountsAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, long>();
        if (!HasCookie) return result;
        try
        {
            using var request = NewGet("getUserGameUnreadCount", string.Empty);
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return result;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!TryData(doc.RootElement, out var data)) return result;
            if (!data.TryGetProperty("count", out var count) || count.ValueKind != JsonValueKind.Object) return result;
            foreach (var name in new[] { "reply", "active_mention", "comment", "system" })
            {
                if (count.TryGetProperty(name, out var value))
                    result[name] = value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;
            }
        }
        catch
        {
            // 网络异常时保持 0，交给页面提示。
        }
        return result;
    }

    public async Task<(IReadOnlyList<MiyousheNotification> Items, string Error)> GetListAsync(
        string category, int pageSize = 20, CancellationToken ct = default)
    {
        if (!HasCookie) return (Array.Empty<MiyousheNotification>(), "未登录米游社");
        try
        {
            var query = $"category={Uri.EscapeDataString(category)}&page_size={pageSize}&current_page=1";
            using var request = NewGet("getUserGameNotifications", query);
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return (Array.Empty<MiyousheNotification>(), $"接口返回 {(int)response.StatusCode}");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!TryData(doc.RootElement, out var data))
                return (Array.Empty<MiyousheNotification>(), "米游社返回异常");
            if (!data.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
                return (Array.Empty<MiyousheNotification>(), string.Empty);

            var items = new List<MiyousheNotification>();
            foreach (var item in list.EnumerateArray())
                items.Add(Parse(item, category));
            return (items, string.Empty);
        }
        catch (Exception ex)
        {
            return (Array.Empty<MiyousheNotification>(), "加载失败：" + ex.Message);
        }
    }

    public async Task<bool> ClearUnreadAsync(string category, CancellationToken ct = default)
    {
        if (!HasCookie) return false;
        try
        {
            var payload = JsonSerializer.Serialize(new { category });
            var query = string.Empty;
            using var request = NewPost("clearUserGameUnread", query, payload);
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("retcode", out var code) && code.GetInt32() == 0;
        }
        catch
        {
            return false;
        }
    }

    private static MiyousheNotification Parse(JsonElement item, string category)
    {
        var op = item.TryGetProperty("op_user", out var user) && user.ValueKind == JsonValueKind.Object
            ? user : default;
        var nickname = Str(op, "nickname");
        var avatar = Str(op, "avatar_url");
        var content = Str(item, "content");
        if (string.IsNullOrWhiteSpace(content)) content = Str(item, "site_content");
        if (string.IsNullOrWhiteSpace(content)) content = Str(item, "detail");
        if (string.IsNullOrWhiteSpace(content)) content = Str(item, "subject");

        var origin = string.Empty;
        if (item.TryGetProperty("origin", out var originElement) && originElement.ValueKind == JsonValueKind.Object)
            origin = Str(originElement, "text");

        var postId = string.Empty;
        if (item.TryGetProperty("ext", out var ext) && ext.ValueKind == JsonValueKind.Object)
        {
            postId = Str(ext, "post_id");
            if (string.IsNullOrWhiteSpace(content)) content = Str(ext, "reply_content");
        }

        var web = Str(item, "web_path");
        if (string.IsNullOrWhiteSpace(web) && !string.IsNullOrWhiteSpace(postId))
            web = $"https://www.miyoushe.com/ys/article/{postId}";
        if (string.IsNullOrWhiteSpace(web))
            web = "https://www.miyoushe.com/ys/notifications/" + (category == "active_mention" ? "mention" : category);

        var created = item.TryGetProperty("created_at", out var createdAt) && createdAt.ValueKind == JsonValueKind.Number
            ? createdAt.GetInt64() : 0;
        var unread = item.TryGetProperty("is_read", out var read) && read.ValueKind == JsonValueKind.False;

        return new MiyousheNotification
        {
            Id = Str(item, "notification_id"),
            Category = category,
            Nickname = string.IsNullOrWhiteSpace(nickname) ? "米游社" : nickname,
            AvatarUrl = avatar,
            Content = content.Trim(),
            Origin = origin.Trim(),
            ActionUrl = web,
            CreatedAt = created,
            Unread = unread,
        };
    }

    private HttpRequestMessage NewGet(string action, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseApi}/{action}{(query.Length > 0 ? "?" + query : string.Empty)}");
        AddSignedHeaders(request, query, string.Empty);
        return request;
    }

    private HttpRequestMessage NewPost(string action, string query, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseApi}/{action}{(query.Length > 0 ? "?" + query : string.Empty)}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        AddSignedHeaders(request, query, body);
        return request;
    }

    private static void AddSignedHeaders(HttpRequestMessage request, string query, string body)
    {
        var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var random = Random.Shared.Next(100000, 200000).ToString();
        var hash = $"salt={X4Salt}&t={time}&r={random}&b={body}&q={query}";
        var ds = $"{time},{random},{Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(hash))).ToLowerInvariant()}";

        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/2.119.0");
        request.Headers.TryAddWithoutValidation("x-rpc-app_version", "2.119.0");
        request.Headers.TryAddWithoutValidation("x-rpc-client_type", "5");
        request.Headers.TryAddWithoutValidation("x-rpc-device_id", DeviceId);
        request.Headers.TryAddWithoutValidation("x-requested-with", "com.mihoyo.hyperion");
        request.Headers.TryAddWithoutValidation("Referer", "https://www.miyoushe.com/");
        request.Headers.TryAddWithoutValidation("ds", ds);
    }

    private static bool TryData(JsonElement root, out JsonElement data)
    {
        data = default;
        if (!root.TryGetProperty("retcode", out var code) || code.ValueKind != JsonValueKind.Number || code.GetInt32() != 0)
            return false;
        return root.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Object;
    }

    private static string Str(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return string.Empty;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            _ => string.Empty,
        };
    }
}

public sealed class MiyousheNotification
{
    public string Id { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string ActionUrl { get; init; } = string.Empty;
    public long CreatedAt { get; init; }
    public bool Unread { get; init; }
}
