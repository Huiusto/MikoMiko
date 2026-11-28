using System.Net.Http;
using System.Text.Json;

namespace MikoMiko.Services;

/// <summary>
/// 官方启动器「游戏预约」页数据：直接读取 HoYoPlay 的
/// getGameReservationContent 接口，和官方启动器使用同一份 logo、简介、
/// 标签、视频与图片素材。
/// </summary>
public static class GameReservationService
{
    private const string LauncherId = "jGHBHlcOq1";
    private const string ApiRoot = "https://hyp-api.mihoyo.com/hyp/hyp-connect/api";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(18),
    };

    public sealed record Media(string Type, string Cover, string Resource);

    public sealed record Content(
        string Key,
        string Name,
        string Logo,
        string Introduction,
        IReadOnlyList<string> Tags,
        IReadOnlyList<Media> Medias,
        string OfficialUrl);

    private static readonly Dictionary<string, (string GameId, string Name, string OfficialUrl)> Games = new()
    {
        ["planet"] = ("ERYDdvf0w8", "星布谷地", "https://planet.mihoyo.com"),
        ["yinyuan"] = ("m8TGERqknS", "崩坏：因缘精灵", "https://yyjl.mihoyo.com"),
    };

    public static bool Supports(string key) => Games.ContainsKey(key);

    public static bool TryGetOfficialUrl(string key, out string url)
    {
        if (Games.TryGetValue(key, out var info))
        {
            url = info.OfficialUrl;
            return true;
        }
        url = string.Empty;
        return false;
    }

    public static async Task<Content> LoadAsync(string key)
    {
        (string GameId, string Name, string OfficialUrl) info = Games.TryGetValue(key, out var found)
            ? found
            : ("", key, "");

        try
        {
            var url = $"{ApiRoot}/getGameReservationContent?launcher_id={LauncherId}&language=zh-cn&game_id={info.GameId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("MikoMiko/0.1.67 HoYoPlay/1.0");
            using var response = await Http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.TryGetProperty("retcode", out var retcode) && retcode.GetInt32() != 0)
                return Fallback(key, info.Name, info.OfficialUrl);

            var data = root.GetProperty("data");
            var logo = data.TryGetProperty("logo", out var logoElement) ? logoElement.GetString() ?? "" : "";
            var intro = data.TryGetProperty("introduction", out var introElement) ? introElement.GetString() ?? "" : "";

            var tags = new List<string>();
            if (data.TryGetProperty("tags", out var tagArray) && tagArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagArray.EnumerateArray())
                {
                    if (tag.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) tags.Add(name);
                    }
                }
            }

            var mediaItems = new List<(string Type, string Cover, string Resource, int Order)>();
            if (data.TryGetProperty("media", out var mediaArray) && mediaArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var media in mediaArray.EnumerateArray())
                {
                    var type = media.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "" : "";
                    var cover = "";
                    if (media.TryGetProperty("cover", out var coverElement) &&
                        coverElement.ValueKind == JsonValueKind.Object &&
                        coverElement.TryGetProperty("url", out var coverUrl))
                    {
                        cover = coverUrl.GetString() ?? "";
                    }
                    var resource = media.TryGetProperty("resource_url", out var resourceElement) ? resourceElement.GetString() ?? "" : "";
                    var order = media.TryGetProperty("order", out var orderElement) && orderElement.TryGetInt32(out var o) ? o : 0;
                    if (string.IsNullOrWhiteSpace(cover) && string.IsNullOrWhiteSpace(resource)) continue;
                    mediaItems.Add((type, cover, resource, order));
                }
            }

            var medias = mediaItems
                .OrderBy(m => m.Order)
                .Select(m => new Media(m.Type, m.Cover, m.Resource))
                .ToList();

            return new Content(key, info.Name, logo, intro, tags, medias, info.OfficialUrl);
        }
        catch
        {
            return Fallback(key, info.Name, info.OfficialUrl);
        }
    }

    private static Content Fallback(string key, string name, string officialUrl)
    {
        return new Content(
            key,
            name,
            "",
            name == "星布谷地"
                ? "《星布谷地》是米哈游自主研发的生活模拟游戏。你将拥有一颗属于自己的星球，遇见生活的无限可能。"
                : "本作为米哈游「崩坏」IP下的全新精灵养成冒险策略游戏，您将与各种各样的因缘精灵结缘，携手伙伴一起进行冒险！",
            name == "星布谷地"
                ? new[] { "生活模拟", "多人联机", "种田", "休闲", "UGC" }
                : new[] { "角色扮演", "精灵养成", "自动战棋" },
            Array.Empty<Media>(),
            officialUrl);
    }
}
