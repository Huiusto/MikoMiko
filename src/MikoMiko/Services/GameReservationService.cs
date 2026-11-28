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
        string OfficialUrl,
        string Slogan = "",
        string ActionLabel = "预约");

    private static readonly Dictionary<string, (string GameId, string Name, string OfficialUrl)> Games = new()
    {
        ["planet"] = ("ERYDdvf0w8", "星布谷地", "https://planet.mihoyo.com"),
        ["yinyuan"] = ("m8TGERqknS", "崩坏：因缘精灵", "https://yyjl.mihoyo.com"),
        ["nodusfall"] = ("", "源初之结", "https://nodusfall.mihoyo.com/home"),
        ["varsapura"] = ("", "Varsapura", "https://varsapura.city/"),
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

        // 0.1.70：源初之结与 Varsapura 官方启动器接口尚未开放，使用随包素材构建同款页面。
        if (key == "nodusfall")
            return LoadNodusfall(info.Name, info.OfficialUrl);
        if (key == "varsapura")
            return LoadVarsapura(info.Name, info.OfficialUrl);

        try
        {
            var url = $"{ApiRoot}/getGameReservationContent?launcher_id={LauncherId}&language=zh-cn&game_id={info.GameId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("MikoMiko/0.1.71 HoYoPlay/1.0");
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

    private static Content LoadNodusfall(string name, string officialUrl)
    {
        const string baseUrl = "https://miko.local/reservation-nodusfall/";
        var medias = Enumerable.Range(1, 8)
            .Select(i =>
            {
                var url = $"{baseUrl}{i:00}.jpg";
                return new Media("IMAGE", url, url);
            })
            .ToList();
        return new Content(
            "nodusfall",
            name,
            baseUrl + "logo.png",
            "《源初之结》是由米哈游研发的奇幻写实题材动作游戏。玩家将以「织者」的身份踏入神权陨落、秩序崩塌的世界，结识强大的伙伴，共同迎战撼天动地的强敌，不断夺取存于世间的神格之力，并最终重建世界秩序。",
            new[] { "奇幻写实", "动作", "开放世界", "米哈游" },
            medias,
            officialUrl);
    }

    private static Content LoadVarsapura(string name, string officialUrl)
    {
        const string baseUrl = "https://miko.local/reservation-varsapura/";
        var medias = Enumerable.Range(1, 9)
            .Select(i =>
            {
                var url = $"{baseUrl}{i:00}.jpg";
                return new Media("IMAGE", url, url);
            })
            .ToList();
        return new Content(
            "varsapura",
            name,
            baseUrl + "logo.png",
            "《Varsapura》是米哈游 HoYoverse 用虚幻引擎 5 打造的一款新怪谈风格都市开放世界动作 RPG，玩家扮演「空心人」加入「阴影控制局」，在雨城现实中对抗超自然沼泽化灾厄，预计 2027 年后发售，可能登陆 PC、主机和移动端多平台。",
            new[] { "新怪谈", "都市开放世界", "动作 RPG", "虚幻引擎 5" },
            medias,
            officialUrl,
            "",
            "第三方介绍");
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
