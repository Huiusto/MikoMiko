using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MikoMiko.Services;

/// <summary>追剧页卡片所需的轻量媒体信息（不解析视频流）。</summary>
public sealed class BilibiliMediaInfo
{
    public string Title { get; init; } = string.Empty;
    public string Cover { get; init; } = string.Empty;
    public string Meta { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int EpisodeCount { get; init; }
}

/// <summary>B 站评论（只在软件内展示，不跳官方页面）。</summary>
public sealed class BilibiliComment
{    public string Nickname { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public long LikeCount { get; init; }
    public string Initial => string.IsNullOrEmpty(Nickname) ? "哔" : Nickname[..1].ToUpperInvariant();
    public string LikeText => LikeCount > 0 ? $"{LikeCount:N0} 赞" : string.Empty;
}

/// <summary>
/// 哔哩哔哩番剧 / 视频的软件内取流。把官方视频流、选集、弹幕与热门评论解析到本地，
/// 交给 AnimePlayerView 原生播放，不再打开官方网页。
/// </summary>
public sealed class BilibiliAnimeService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly Regex PagePattern = new(@"(?:^|&)p=(?<p>\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex BangumiPattern = new(@"/bangumi/play/(?<id>ss|ep)(?<n>\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex MediaPattern = new(@"/bangumi/media/md(?<n>\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex VideoPattern = new(@"/video/(?<bv>BV[0-9A-Za-z]+)", RegexOptions.IgnoreCase);
    private static readonly Regex DmPattern = new(
        @"<d\s+p=""(?<p>[^""]+)""[^>]*>(?<text>.*?)</d>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.bilibili.com/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
        return client;
    }

    public async Task<AnimePlayback?> ResolveAsync(string pageUrl, string title, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pageUrl)) return null;
        try
        {
            var bangumi = BangumiPattern.Match(pageUrl);
            if (bangumi.Success)
            {
                var id = bangumi.Groups["id"].Value.ToLowerInvariant();
                var number = long.Parse(bangumi.Groups["n"].Value);
                return id == "ss"
                    ? await ResolveSeasonAsync(number, title, null, ct)
                    : await ResolveEpisodeAsync(number, title, ct);
            }

            var media = MediaPattern.Match(pageUrl);
            if (media.Success)
            {
                var seasonId = await ResolveSeasonIdFromMediaAsync(long.Parse(media.Groups["n"].Value), ct);
                return seasonId > 0
                    ? await ResolveSeasonAsync(seasonId, title, null, ct)
                    : null;
            }

            var video = VideoPattern.Match(pageUrl);
            if (video.Success)
            {
                var page = 1;
                var p = PagePattern.Match(pageUrl);
                if (p.Success) page = Math.Max(1, int.Parse(p.Groups["p"].Value));
                return await ResolveVideoAsync(video.Groups["bv"].Value, page, title, ct);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>读取番剧/视频的标题、封面与简介，供追剧页原生卡片使用。</summary>
    public async Task<BilibiliMediaInfo?> GetMediaInfoAsync(string pageUrl, string fallbackTitle = "",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pageUrl)) return null;
        try
        {
            var media = MediaPattern.Match(pageUrl);
            if (media.Success)
            {
                var seasonId = await ResolveSeasonIdFromMediaAsync(long.Parse(media.Groups["n"].Value), ct);
                return seasonId > 0 ? await GetSeasonInfoAsync(seasonId, fallbackTitle, ct) : null;
            }

            var bangumi = BangumiPattern.Match(pageUrl);
            if (bangumi.Success && bangumi.Groups["id"].Value.Equals("ss", StringComparison.OrdinalIgnoreCase))
                return await GetSeasonInfoAsync(long.Parse(bangumi.Groups["n"].Value), fallbackTitle, ct);

            var video = VideoPattern.Match(pageUrl);
            if (video.Success) return await GetVideoInfoAsync(video.Groups["bv"].Value, fallbackTitle, ct);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<BilibiliMediaInfo?> GetSeasonInfoAsync(long seasonId, string fallbackTitle,
        CancellationToken ct)
    {
        using var season = await GetJsonAsync($"https://api.bilibili.com/pgc/view/web/season?season_id={seasonId}", ct);
        if (season is null) return null;
        var root = season.RootElement;
        if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0) return null;
        if (!root.TryGetProperty("result", out var result)) return null;
        var episodes = result.TryGetProperty("episodes", out var epNode) && epNode.ValueKind == JsonValueKind.Array
            ? epNode.GetArrayLength()
            : 0;
        var typeName = ReadString(result, "type_name", "番剧");
        return new BilibiliMediaInfo
        {
            Title = ReadString(result, "title", fallbackTitle),
            Cover = ReadString(result, "cover"),
            Description = ReadString(result, "evaluate"),
            EpisodeCount = episodes,
            Meta = $"哔哩哔哩 · {typeName}" + (episodes > 0 ? $" · 全 {episodes} 话" : ""),
        };
    }

    private static async Task<BilibiliMediaInfo?> GetVideoInfoAsync(string bvid, string fallbackTitle,
        CancellationToken ct)
    {
        using var view = await GetJsonAsync($"https://api.bilibili.com/x/web-interface/view?bvid={bvid}", ct);
        if (view is null) return null;
        var root = view.RootElement;
        if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0) return null;
        if (!root.TryGetProperty("data", out var data)) return null;
        var owner = data.TryGetProperty("owner", out var ownerNode) ? ReadString(ownerNode, "name") : string.Empty;
        var duration = FormatDuration(ReadLong(data, "duration"));
        var pages = data.TryGetProperty("pages", out var pagesNode) && pagesNode.ValueKind == JsonValueKind.Array
            ? pagesNode.GetArrayLength()
            : 1;
        return new BilibiliMediaInfo
        {
            Title = ReadString(data, "title", fallbackTitle),
            Cover = ReadString(data, "pic"),
            Description = ReadString(data, "desc"),
            EpisodeCount = pages,
            Meta = string.Join(" · ", new[] { "哔哩哔哩", owner, duration }.Where(x => !string.IsNullOrWhiteSpace(x))),
        };
    }

    private static async Task<long> ResolveSeasonIdFromMediaAsync(long mediaId, CancellationToken ct)
    {
        try
        {
            using var response = await Http.GetAsync($"https://www.bilibili.com/bangumi/media/md{mediaId}", ct);
            if (!response.IsSuccessStatusCode) return 0;
            var html = await response.Content.ReadAsStringAsync(ct);
            var match = Regex.Match(html, @"""season_id""\s*:\s*(?<id>\d+)", RegexOptions.IgnoreCase);
            return match.Success && long.TryParse(match.Groups["id"].Value, out var id) ? id : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<AnimePlayback?> ResolveVideoAsync(string bvid, int pageIndex, string fallbackTitle,
        CancellationToken ct)
    {
        using var view = await GetJsonAsync($"https://api.bilibili.com/x/web-interface/view?bvid={bvid}", ct);
        if (view is null) return null;
        var root = view.RootElement;
        if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0) return null;
        if (!root.TryGetProperty("data", out var data)) return null;

        var title = ReadString(data, "title", fallbackTitle);
        var poster = ReadString(data, "pic");
        var description = ReadString(data, "desc");
        var aid = ReadLong(data, "aid");
        var owner = data.TryGetProperty("owner", out var ownerNode) ? ReadString(ownerNode, "name") : string.Empty;
        var duration = FormatDuration(ReadLong(data, "duration"));

        var episodes = new List<AnimeEpisode>();
        var pages = data.TryGetProperty("pages", out var pagesNode) && pagesNode.ValueKind == JsonValueKind.Array
            ? pagesNode.EnumerateArray().ToList()
            : new List<JsonElement>();
        for (var i = 0; i < pages.Count; i++)
        {
            var item = pages[i];
            var part = ReadString(item, "part", $"P{i + 1}");
            episodes.Add(new AnimeEpisode
            {
                Name = pages.Count > 1 ? $"P{i + 1} {part}".Trim() : "正片",
                Order = i + 1,
                PageUrl = i == 0
                    ? $"https://www.bilibili.com/video/{bvid}"
                    : $"https://www.bilibili.com/video/{bvid}?p={i + 1}",
            });
        }
        if (episodes.Count == 0) return null;

        var index = Math.Clamp(pageIndex - 1, 0, pages.Count - 1);
        var page = pages[index];
        var cid = ReadLong(page, "cid");
        var selected = episodes[index];

        var streamUrl = await GetPlayUrlAsync(
            $"https://api.bilibili.com/x/player/playurl?bvid={bvid}&cid={cid}&qn=80&fnval=1&fourk=1&platform=html5&high_quality=1",
            false, ct);
        if (string.IsNullOrWhiteSpace(streamUrl)) return null;

        var dm = await GetDanmakuAsync(cid, ct);
        var comments = await GetCommentsAsync(aid, ct);
        var details = new AnimeDetails
        {
            Title = title,
            Poster = poster,
            Description = description,
            Meta = $"哔哩哔哩 · {owner} · {duration}",
            Sources = new List<AnimeSource>
            {
                new() { Name = "哔哩哔哩", Episodes = episodes },
            },
        };
        var catalog = new AnimeItem
        {
            Title = title,
            Poster = poster,
            Url = $"https://www.bilibili.com/video/{bvid}",
            PlayUrl = selected.PageUrl,
            Source = "哔哩哔哩",
            Note = selected.Name,
            Kind = "视频",
        };
        return new AnimePlayback
        {
            Title = title,
            Episode = pages.Count > 1 ? selected.Name : "正片",
            StreamUrl = streamUrl,
            Referer = "https://www.bilibili.com/",
            Source = "哔哩哔哩",
            Poster = poster,
            Description = description,
            Meta = details.Meta,
            Details = details,
            SelectedSource = details.Sources[0],
            SelectedEpisode = new AnimeEpisode
            {
                Name = selected.Name,
                Order = selected.Order,
                PageUrl = selected.PageUrl,
            },
            CatalogItem = catalog,
            IsBilibili = true,
            Danmaku = dm,
            Comments = comments,
        };
    }

    private async Task<AnimePlayback?> ResolveSeasonAsync(long seasonId, string fallbackTitle, long? selectedEpId,
        CancellationToken ct)
    {
        using var season = await GetJsonAsync($"https://api.bilibili.com/pgc/view/web/season?season_id={seasonId}", ct);
        if (season is null) return null;
        var root = season.RootElement;
        if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0) return null;
        if (!root.TryGetProperty("result", out var result)) return null;

        var episodesNode = result.TryGetProperty("episodes", out var epNode) && epNode.ValueKind == JsonValueKind.Array
            ? epNode.EnumerateArray().ToList()
            : new List<JsonElement>();
        if (episodesNode.Count == 0) return null;

        var title = ReadString(result, "title", fallbackTitle);
        var poster = ReadString(result, "cover");
        var description = ReadString(result, "evaluate");
        var typeName = ReadString(result, "type_name", "番剧");
        var meta = $"哔哩哔哩 · {typeName}";

        var episodes = new List<AnimeEpisode>();
        var cids = new List<long>();
        var aids = new List<long>();
        var ids = new List<long>();
        for (var i = 0; i < episodesNode.Count; i++)
        {
            var item = episodesNode[i];
            var epId = ReadLong(item, "id");
            var longTitle = ReadString(item, "long_title");
            var showTitle = ReadString(item, "show_title");
            var name = !string.IsNullOrWhiteSpace(longTitle)
                ? longTitle
                : !string.IsNullOrWhiteSpace(showTitle) ? showTitle : $"第 {i + 1} 话";
            ids.Add(epId);
            cids.Add(ReadLong(item, "cid"));
            aids.Add(ReadLong(item, "aid"));
            episodes.Add(new AnimeEpisode
            {
                Name = name,
                Order = i + 1,
                PageUrl = $"https://www.bilibili.com/bangumi/play/ep{epId}",
            });
        }

        var index = selectedEpId is null ? 0 : ids.FindIndex(x => x == selectedEpId.Value);
        if (index < 0) index = 0;

        var streamUrl = await GetPlayUrlAsync(
            $"https://api.bilibili.com/pgc/player/web/playurl?ep_id={ids[index]}&cid={cids[index]}&qn=80&fnval=1&fourk=1&platform=html5&high_quality=1",
            true, ct);
        if (string.IsNullOrWhiteSpace(streamUrl)) return null;

        var dm = await GetDanmakuAsync(cids[index], ct);
        var comments = await GetCommentsAsync(aids[index], ct);
        var details = new AnimeDetails
        {
            Title = title,
            Poster = poster,
            Description = description,
            Meta = meta,
            Sources = new List<AnimeSource>
            {
                new() { Name = "哔哩哔哩", Episodes = episodes },
            },
        };
        var selected = episodes[index];
        var catalog = new AnimeItem
        {
            Title = title,
            Poster = poster,
            Url = $"https://www.bilibili.com/bangumi/play/ss{seasonId}",
            PlayUrl = selected.PageUrl,
            Source = "哔哩哔哩",
            Note = selected.Name,
            Kind = typeName,
        };
        return new AnimePlayback
        {
            Title = title,
            Episode = selected.Name,
            StreamUrl = streamUrl,
            Referer = "https://www.bilibili.com/",
            Source = "哔哩哔哩",
            Poster = poster,
            Description = description,
            Meta = meta,
            Details = details,
            SelectedSource = details.Sources[0],
            SelectedEpisode = selected,
            CatalogItem = catalog,
            IsBilibili = true,
            Danmaku = dm,
            Comments = comments,
        };
    }

    private async Task<AnimePlayback?> ResolveEpisodeAsync(long epId, string fallbackTitle, CancellationToken ct)
    {
        using var season = await GetJsonAsync($"https://api.bilibili.com/pgc/view/web/season?ep_id={epId}", ct);
        if (season is null) return null;
        var root = season.RootElement;
        if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0) return null;
        if (!root.TryGetProperty("result", out var result)) return null;
        var seasonId = ReadLong(result, "season_id");
        return await ResolveSeasonAsync(seasonId > 0 ? seasonId : 0, fallbackTitle, epId, ct);
    }

    public async Task<AnimePlayback?> ResolveEpisodeAsync(AnimePlayback current, AnimeEpisode episode,
        CancellationToken ct = default)
    {
        var next = await ResolveAsync(episode.PageUrl, current.Title, ct);
        if (next is null) return null;
        // 保留当前作品的完整选集，避免每次切集重新构建出不同标题。
        if (current.Details is not null && next.Details is not null)
        {
            var source = current.Details.Sources.FirstOrDefault();
            if (source is not null && source.Episodes.Count > 0)
            {
                next.Details = new AnimeDetails
                {
                    Title = current.Details.Title,
                    Poster = current.Details.Poster,
                    Description = current.Details.Description,
                    Meta = current.Details.Meta,
                    Sources = current.Details.Sources,
                };
                next.SelectedSource = source;
                var target = source.Episodes.FirstOrDefault(e =>
                    string.Equals(e.PageUrl, episode.PageUrl, StringComparison.OrdinalIgnoreCase));
                if (target is not null) next.SelectedEpisode = target;
            }
        }
        return next;
    }

    private static async Task<string> GetPlayUrlAsync(string api, bool isPgc, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(api, ct);
        if (doc is null) return string.Empty;
        var root = doc.RootElement;
        if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0) return string.Empty;
        var data = isPgc
            ? (root.TryGetProperty("result", out var result) ? result : default)
            : (root.TryGetProperty("data", out var dataNode) ? dataNode : default);
        if (data.ValueKind != JsonValueKind.Object) return string.Empty;

        if (data.TryGetProperty("durl", out var durl) && durl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in durl.EnumerateArray())
            {
                var url = ReadString(item, "url");
                if (!string.IsNullOrWhiteSpace(url)) return url;
            }
        }

        return string.Empty;
    }

    private async Task<List<(long TimeMs, string Text)>> GetDanmakuAsync(long cid, CancellationToken ct)
    {
        var result = new List<(long, string)>();
        if (cid <= 0) return result;
        try
        {
            using var response = await Http.GetAsync($"https://api.bilibili.com/x/v1/dm/list.so?oid={cid}", ct);
            if (!response.IsSuccessStatusCode) return result;
            var xml = await response.Content.ReadAsStringAsync(ct);
            foreach (Match match in DmPattern.Matches(xml))
            {
                var p = match.Groups["p"].Value.Split(',');
                if (p.Length == 0) continue;
                var seconds = double.TryParse(p[0], out var value) ? value : 0;
                var text = WebUtility.HtmlDecode(Regex.Replace(match.Groups["text"].Value, "<[^>]+>", string.Empty)).Trim();
                if (text.Length == 0) continue;
                result.Add(((long)Math.Round(seconds * 1000), text));
                if (result.Count >= 2000) break;
            }
        }
        catch
        {
            // 弹幕失败不影响播放。
        }
        return result;
    }

    private async Task<List<BilibiliComment>> GetCommentsAsync(long aid, CancellationToken ct)
    {
        var result = new List<BilibiliComment>();
        if (aid <= 0) return result;
        try
        {
            using var doc = await GetJsonAsync(
                $"https://api.bilibili.com/x/v2/reply?type=1&oid={aid}&pn=1&ps=20&sort=1", ct);
            if (doc is null) return result;
            var root = doc.RootElement;
            if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0) return result;
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("replies", out var replies) ||
                replies.ValueKind != JsonValueKind.Array) return result;
            foreach (var item in replies.EnumerateArray())
            {
                var member = item.TryGetProperty("member", out var m) ? m : default;
                var content = item.TryGetProperty("content", out var c) ? c : default;
                var message = ReadString(content, "message").Replace("\n", " ").Trim();
                if (message.Length == 0) continue;
                var ctime = ReadLong(item, "ctime");
                result.Add(new BilibiliComment
                {
                    Nickname = ReadString(member, "uname", "哔哩哔哩用户"),
                    AvatarUrl = ReadString(member, "avatar"),
                    Message = message,
                    LikeCount = ReadLong(item, "like"),
                    TimeText = ctime > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(ctime).ToLocalTime().ToString("MM-dd HH:mm")
                        : string.Empty,
                });
                if (result.Count >= 20) break;
            }
        }
        catch
        {
            // 评论区失败不影响播放。
        }
        return result;
    }

    private static async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;
        var raw = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(raw);
    }

    private static string ReadString(JsonElement node, string name, string fallback = "")
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind != JsonValueKind.String) return fallback;
        var text = value.GetString() ?? fallback;
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static long ReadLong(JsonElement node, string name)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetInt64(out var number) ? number : 0;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0;
    }

    private static string FormatDuration(long seconds)
    {
        if (seconds <= 0) return string.Empty;
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{span.Hours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes}:{span.Seconds:00}";
    }
}
