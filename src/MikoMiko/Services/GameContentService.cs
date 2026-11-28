using System.Net.Http;
using System.Text.Json;

namespace MikoMiko.Services;

/// <summary>
/// 官方启动器左下角「相关内容」：直接读取 HoYoPlay 的 getGameContent，
/// 与官方启动器同一份活动、公告、资讯条目（标题、日期、链接、banner 图）。
/// </summary>
public static class GameContentService
{
    private const string LauncherId = "jGHBHlcOq1";
    private const string ApiRoot = "https://hyp-api.mihoyo.com/hyp/hyp-connect/api";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(18) };

    public sealed record Post(string Kind, string Title, string Date, string Url, string Cover);

    public sealed record Content(string GameName, IReadOnlyList<Post> Posts);

    private static readonly Dictionary<string, (string GameId, string Name)> Games = new()
    {
        ["genshin"] = ("1Z8W5NHUQb", "原神"),
        ["starrail"] = ("64kMb5iAWu", "崩坏：星穹铁道"),
        ["zzz"] = ("x6znKlJ0xK", "绝区零"),
        ["bh3"] = ("osvnlOc0S8", "崩坏3"),
    };

    public static async Task<Content> LoadAsync(string key)
    {
        if (!Games.TryGetValue(key, out var info)) return new Content("", Array.Empty<Post>());

        var url = $"{ApiRoot}/getGameContent?launcher_id={LauncherId}&language=zh-cn&game_id={info.GameId}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("MikoMiko/0.1.71 HoYoPlay/1.0");
            using var response = await Http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new Content(info.Name, Array.Empty<Post>());

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var root = doc.RootElement;
            if (root.TryGetProperty("retcode", out var code) && code.GetInt32() != 0)
                return new Content(info.Name, Array.Empty<Post>());

            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("content", out var content))
                return new Content(info.Name, Array.Empty<Post>());

            // 官方启动器给部分资讯配了 banner 大图，按去掉查询参数后的链接与 posts 对齐。
            var covers = new Dictionary<string, string>();
            if (content.TryGetProperty("banners", out var banners) && banners.ValueKind == JsonValueKind.Array)
            {
                foreach (var banner in banners.EnumerateArray())
                {
                    if (!banner.TryGetProperty("image", out var image)) continue;
                    var imageUrl = Str(image, "url");
                    var link = Normalize(Str(image, "link"));
                    if (link.Length > 0 && imageUrl.Length > 0 && !covers.ContainsKey(link))
                        covers[link] = imageUrl;
                }
            }

            var posts = new List<Post>();
            if (content.TryGetProperty("posts", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var post in items.EnumerateArray())
                {
                    var title = Str(post, "title").Trim();
                    var link = Str(post, "link").Trim();
                    if (title.Length == 0 || link.Length == 0) continue;
                    var kind = KindOf(Str(post, "type"));
                    posts.Add(new Post(
                        kind,
                        title,
                        Str(post, "date"),
                        link,
                        covers.TryGetValue(Normalize(link), out var cover) ? cover : ""));
                }
            }

            return new Content(info.Name, posts);
        }
        catch
        {
            return new Content(info.Name, Array.Empty<Post>());
        }
    }

    private static string KindOf(string type) => type switch
    {
        "POST_TYPE_ACTIVITY" => "activity",
        "POST_TYPE_ANNOUNCE" => "announce",
        _ => "info",
    };

    private static string Normalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        try
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath.TrimEnd('/')}".ToLowerInvariant();
        }
        catch
        {
            return url.Trim();
        }
    }

    private static string Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
