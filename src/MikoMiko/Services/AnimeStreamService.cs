using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MikoMiko.Services;

/// <summary>一条可以直接交给播放器的视频流。</summary>
public sealed class AnimePlayback
{
    public string Title { get; set; } = string.Empty;
    public string Episode { get; set; } = "正片";
    public string StreamUrl { get; set; } = string.Empty;
    public string Referer { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Poster { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public AnimeDetails? Details { get; set; }
    public AnimeSource? SelectedSource { get; set; }
    public AnimeEpisode? SelectedEpisode { get; set; }
    public AnimeItem? CatalogItem { get; set; }

    public bool IsHls => StreamUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);
}

/// <summary>详情页里的一集。</summary>
public sealed class AnimeEpisode
{
    public string Name { get; init; } = "正片";
    public string PageUrl { get; init; } = string.Empty;
    public int Order { get; init; }
}

/// <summary>一个播放线路及其剧集。</summary>
public sealed class AnimeSource
{
    public string Name { get; init; } = "默认线路";
    public List<AnimeEpisode> Episodes { get; init; } = new();
    public string EpisodeCountText => $"{Episodes.Count} 集";
}

/// <summary>详情页上可直接展示的剧集信息。</summary>
public sealed class AnimeDetails
{
    public string Title { get; init; } = string.Empty;
    public string Poster { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Meta { get; init; } = string.Empty;
    public List<AnimeSource> Sources { get; init; } = new();
}

/// <summary>
/// 从追番站点的公开详情/播放页提取真实视频地址和选集列表。
/// 支持 MacCMS 的 player_aaaa、页面内直链 m3u8/mp4，以及常见播放页跳转；
/// 只读取公开页面，不提交账号信息，也不再把站点网页展示给用户。
/// </summary>
public sealed class AnimeStreamService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly Regex PlayerPattern = new(
        @"player_aaaa\s*=\s*(?<json>\{.*?\})\s*</script>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex PlayerFallbackPattern = new(
        @"player_aaaa\s*=\s*(?<json>\{[^\r\n]*\})",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex MediaPattern = new(
        @"(?:https?:)?//[^""'<>\s]+?\.(?:m3u8|mp4)(?:\?[^""'<>\s]*)?",
        RegexOptions.IgnoreCase);
    private static readonly Regex AnyAnchor = new(
        @"<a\b(?<attrs>[^>]*)>(?<body>.*?)</a>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex TagPattern = new(@"<[^>]+>", RegexOptions.Singleline);
    private static readonly Regex SpacePattern = new(@"\s+");
    private static readonly Regex HrefPattern = new(
        @"href=""(?<href>[^""]+)""",
        RegexOptions.IgnoreCase);
    private static readonly Regex EpisodePattern = new(
        @"(?:/(?:p|play)/[^/?#]+-|/mhyskan/[^/?#]+-)(?<sid>\d+)-(?<nid>\d+)",
        RegexOptions.IgnoreCase);
    private static readonly Regex PlayHrefPattern = new(
        @"(?:/(?:p|play|mhyskan|vodplay)/)",
        RegexOptions.IgnoreCase);
    private static readonly Regex StatusPattern = new(
        @"(?:已完结|完结|全\s*\d+\s*集|更新(?:至|到)?\s*第?\s*\d+\s*[集话期]?|连载中|正片)",
        RegexOptions.IgnoreCase);

    public async Task<AnimePlayback?> ResolveAsync(AnimeItem item, CancellationToken cancellationToken = default)
    {
        if (item is null) return null;

        var pageUrl = string.IsNullOrWhiteSpace(item.PlayUrl) ? item.Url : item.PlayUrl;
        if (string.IsNullOrWhiteSpace(pageUrl)) return null;

        try
        {
            var pageHtml = await FetchAsync(pageUrl, item.Url, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(pageHtml)) return null;

            var details = BuildDetails(pageHtml, pageUrl, item);

            // 详情页本身就是播放页时，直接取流。
            var direct = ParseHtml(pageHtml, pageUrl, item, details, null, null);
            if (direct is not null)
            {
                if (details.Sources.Count == 0)
                    details.Sources.Add(new AnimeSource
                    {
                        Name = "默认线路",
                        Episodes = new List<AnimeEpisode>
                        {
                            new() { Name = direct.Episode, PageUrl = pageUrl, Order = 1 },
                        },
                    });
                direct.Details = details;
                direct.SelectedEpisode = details.Sources.FirstOrDefault()?.Episodes.FirstOrDefault();
                direct.SelectedSource = details.Sources.FirstOrDefault();
                return direct;
            }

            if (details.Sources.Count == 0)
            {
                // 详情页没有播放数据时，按“第一播放源、第一集”取播放页。
                var candidates = FindPlayPages(pageHtml, pageUrl);
                foreach (var candidate in candidates.Take(3))
                {
                    var playHtml = await FetchAsync(candidate, pageUrl, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(playHtml)) continue;
                    var fallback = ParseHtml(playHtml, candidate, item, details, null, null);
                    if (fallback is null) continue;
                    fallback.Details = details;
                    fallback.SelectedEpisode = new AnimeEpisode { Name = fallback.Episode, PageUrl = candidate, Order = 1 };
                    fallback.SelectedSource = new AnimeSource
                    {
                        Name = "默认线路",
                        Episodes = new List<AnimeEpisode> { fallback.SelectedEpisode },
                    };
                    details.Sources.Add(fallback.SelectedSource);
                    return fallback;
                }
                return null;
            }

            // 逐个线路尝试第一集，遇到源站限制时继续往后找。
            foreach (var source in details.Sources.Take(4))
            {
                foreach (var episode in source.Episodes.Take(2))
                {
                    var playback = await ResolveEpisodeAsync(details, source, episode, item, cancellationToken)
                        .ConfigureAwait(false);
                    if (playback is not null) return playback;
                }
            }
        }
        catch
        {
            // 网络失败或页面结构变化时返回空，由界面提示用户。
        }

        return null;
    }

    /// <summary>切换线路或选集时按对应播放页重新取流。</summary>
    public async Task<AnimePlayback?> ResolveEpisodeAsync(AnimeDetails details, AnimeSource source,
        AnimeEpisode episode, AnimeItem item, CancellationToken cancellationToken = default)
    {
        if (detailUrlInvalid(episode.PageUrl)) return null;
        try
        {
            var html = await FetchAsync(episode.PageUrl, item.Url, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(html)) return null;
            return ParseHtml(html, episode.PageUrl, item, details, source, episode);
        }
        catch
        {
            return null;
        }
    }

    private static bool detailUrlInvalid(string url)
        => string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase);

    private static AnimePlayback? ParseHtml(string html, string pageUrl, AnimeItem item, AnimeDetails? details,
        AnimeSource? source, AnimeEpisode? episode)
    {
        var stream = ParsePlayerJson(html, pageUrl);
        if (string.IsNullOrWhiteSpace(stream))
            stream = ParseDirectMedia(html, pageUrl);
        if (string.IsNullOrWhiteSpace(stream)) return null;

        return new AnimePlayback
        {
            Title = details?.Title ?? item.Title,
            Episode = episode?.Name ?? ReadEpisode(pageUrl),
            StreamUrl = stream,
            Referer = pageUrl,
            Source = source?.Name ?? item.Source,
            Poster = item.Poster,
            Description = details?.Description ?? string.Empty,
            Meta = details?.Meta ?? item.Note,
            Details = details,
            SelectedSource = source,
            SelectedEpisode = episode,
            CatalogItem = item,
        };
    }

    private static AnimeDetails BuildDetails(string html, string pageUrl, AnimeItem item)
    {
        var sources = ParseSources(html, pageUrl);
        return new AnimeDetails
        {
            Title = string.IsNullOrWhiteSpace(item.Title) ? ReadTitle(html) : item.Title,
            Poster = item.Poster,
            Description = ReadDescription(html, item.Note),
            Meta = BuildMeta(html, item),
            Sources = sources,
        };
    }

    private static List<AnimeSource> ParseSources(string html, string pageUrl)
    {
        var host = SafeHost(pageUrl);
        List<AnimeSource> sources;

        if (host.Contains("superpix", StringComparison.OrdinalIgnoreCase))
            sources = ParseSuperpixSources(html, pageUrl);
        else if (host.Contains("mh-ys", StringComparison.OrdinalIgnoreCase))
            sources = ParseMhysSources(html, pageUrl);
        else if (host.Contains("yhdmtv", StringComparison.OrdinalIgnoreCase))
            sources = ParseYhdmtvSources(html, pageUrl);
        else
            sources = ParseGenericSources(html, pageUrl);

        if (sources.Count == 0)
            sources = ParseGenericSources(html, pageUrl);
        return sources;
    }

    private static List<AnimeSource> ParseSuperpixSources(string html, string pageUrl)
    {
        var sources = new List<AnimeSource>();
        var sections = Regex.Matches(html,
            @"<h3[^>]*class=""title""[^>]*>(?<name>.*?)</h3>(?<body>.*?)(?=<h3[^>]*class=""title""|$)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match section in sections)
        {
            var episodes = ParseAnchors(section.Groups["body"].Value, pageUrl);
            if (episodes.Count == 0) continue;
            var name = Text(section.Groups["name"].Value);
            if (name.Length == 0 || name is "剧情介绍" or "猜你喜欢") continue;
            sources.Add(new AnimeSource
            {
                Name = name.Contains("m3u8", StringComparison.OrdinalIgnoreCase) ? "高清线路" : name,
                Episodes = episodes,
            });
        }
        return sources;
    }

    private static List<AnimeSource> ParseMhysSources(string html, string pageUrl)
    {
        var tabNames = new Dictionary<int, string>();
        foreach (Match match in Regex.Matches(html,
                     @"switch=""tab_con_playlist_(?<n>\d+)""[^>]*>(?<body>.*?)</li>",
                     RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            var index = int.TryParse(match.Groups["n"].Value, out var n) ? n : 0;
            var name = Text(match.Groups["body"].Value);
            if (name.Length > 0) tabNames[index] = name;
        }

        var groups = new Dictionary<int, List<AnimeEpisode>>();
        foreach (Match anchor in AnyAnchor.Matches(html))
        {
            var href = Attribute(anchor.Groups["attrs"].Value, "href");
            if (!IsPlayHref(href)) continue;
            var route = PlayRoute(href);
            if (route.Nid < 0) continue;
            var label = Text(anchor.Groups["body"].Value);
            if (label.Length == 0) label = Attribute(anchor.Groups["attrs"].Value, "title");
            var episode = CreateEpisode(label, href, route.Nid, pageUrl);
            if (episode is null) continue;
            if (!groups.TryGetValue(route.Sid, out var list))
                groups[route.Sid] = list = new List<AnimeEpisode>();
            if (list.All(e => !string.Equals(e.PageUrl, episode.PageUrl, StringComparison.OrdinalIgnoreCase)))
                list.Add(episode);
        }

        var sources = new List<AnimeSource>();
        var sortIndex = 0;
        foreach (var pair in groups.OrderBy(g => g.Key))
        {
            var sorted = SortEpisodes(pair.Value);
            if (sorted.Count == 0) continue;
            sortIndex++;
            sources.Add(new AnimeSource
            {
                Name = tabNames.TryGetValue(pair.Key, out var name) ? name : $"线路 {sortIndex}",
                Episodes = sorted,
            });
        }
        return sources;
    }

    private static List<AnimeSource> ParseYhdmtvSources(string html, string pageUrl)
    {
        var episodes = new List<AnimeEpisode>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match anchor in AnyAnchor.Matches(html))
        {
            var href = Attribute(anchor.Groups["attrs"].Value, "href");
            if (!Regex.IsMatch(href, @"/p/\d+/\d+/\d+", RegexOptions.IgnoreCase)) continue;
            var absolute = Normalize(href, pageUrl);
            if (string.IsNullOrWhiteSpace(absolute) || !seen.Add(absolute)) continue;
            var route = PlayRoute(href);
            if (route.Nid == 0) continue;
            var label = Text(anchor.Groups["body"].Value);
            if (label.Length == 0) label = Attribute(anchor.Groups["attrs"].Value, "title");
            var episode = CreateEpisode(label, href, route.Nid, pageUrl);
            if (episode is not null) episodes.Add(episode);
        }

        var sorted = SortEpisodes(episodes);
        if (sorted.Count == 0) return new List<AnimeSource>();
        // 樱花TV 的链接 ID 是内部长编号，按真实顺序生成用户可读的集数。
        var ordered = new List<AnimeEpisode>();
        for (var i = 0; i < sorted.Count; i++)
        {
            ordered.Add(new AnimeEpisode
            {
                Name = $"第 {i + 1} 集",
                PageUrl = sorted[i].PageUrl,
                Order = i + 1,
            });
        }
        return new List<AnimeSource>
        {
            new() { Name = "优选云", Episodes = ordered },
        };
    }

    private static List<AnimeSource> ParseGenericSources(string html, string pageUrl)
    {
        var groups = new Dictionary<int, List<AnimeEpisode>>();
        foreach (Match anchor in AnyAnchor.Matches(html))
        {
            var href = Attribute(anchor.Groups["attrs"].Value, "href");
            if (!IsPlayHref(href)) continue;
            var route = PlayRoute(href);
            if (route.Nid < 0) continue;
            var label = Text(anchor.Groups["body"].Value);
            if (label.Length == 0) label = Attribute(anchor.Groups["attrs"].Value, "title");
            var episode = CreateEpisode(label, href, route.Nid, pageUrl);
            if (episode is null) continue;
            if (!groups.TryGetValue(route.Sid, out var list))
                groups[route.Sid] = list = new List<AnimeEpisode>();
            if (list.All(e => !string.Equals(e.PageUrl, episode.PageUrl, StringComparison.OrdinalIgnoreCase)))
                list.Add(episode);
        }

        if (groups.Count == 0) return new List<AnimeSource>();

        // 只有一条线路时用站点常见的默认名称，避免用户看到没有意义的“线路 9”。
        if (groups.Count == 1)
        {
            var host = SafeHost(pageUrl);
            var name = host.Contains("zhuidm", StringComparison.OrdinalIgnoreCase) ? "极速线路" : "默认线路";
            var episodes = SortEpisodes(groups.Values.First());
            return episodes.Count == 0
                ? new List<AnimeSource>()
                : new List<AnimeSource> { new() { Name = name, Episodes = episodes } };
        }

        var sources = new List<AnimeSource>();
        var index = 0;
        foreach (var pair in groups.OrderBy(g => g.Key))
        {
            var episodes = SortEpisodes(pair.Value);
            if (episodes.Count == 0) continue;
            index++;
            sources.Add(new AnimeSource { Name = $"线路 {index}", Episodes = episodes });
        }
        return sources;
    }

    private static List<AnimeEpisode> ParseAnchors(string html, string pageUrl)
    {
        var episodes = new List<AnimeEpisode>();
        foreach (Match anchor in AnyAnchor.Matches(html))
        {
            var href = Attribute(anchor.Groups["attrs"].Value, "href");
            if (!IsPlayHref(href)) continue;
            var route = PlayRoute(href);
            if (route.Nid < 0) continue;
            var label = Text(anchor.Groups["body"].Value);
            if (label.Length == 0) label = Attribute(anchor.Groups["attrs"].Value, "title");
            var episode = CreateEpisode(label, href, route.Nid, pageUrl);
            if (episode is null) continue;
            if (episodes.All(e => !string.Equals(e.PageUrl, episode.PageUrl, StringComparison.OrdinalIgnoreCase)))
                episodes.Add(episode);
        }
        return SortEpisodes(episodes);
    }

    private static AnimeEpisode? CreateEpisode(string label, string href, int nid, string pageUrl)
    {
        var absolute = Normalize(href, pageUrl);
        if (string.IsNullOrWhiteSpace(absolute)) return null;
        label = CleanLabel(label);
        if (label.Length == 0 || label is "立即播放" or "播放")
        {
            label = nid > 0 && nid < 10000 ? $"第 {nid} 集" : (nid == 0 ? "正片" : "正片");
            if (label.Length > 28) label = "正片";
        }
        if (label.Length > 32) label = label[..32];
        return new AnimeEpisode { Name = label, PageUrl = absolute, Order = nid };
    }

    private static List<AnimeEpisode> SortEpisodes(IEnumerable<AnimeEpisode> episodes)
    {
        return episodes
            .GroupBy(e => e.PageUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Order <= 0 ? 999999 : e.Order)
            .ThenBy(e => NumericLabel(e.Name))
            .ToList();
    }

    private static int NumericLabel(string label)
    {
        var match = Regex.Match(label ?? string.Empty, @"\d+");
        return match.Success && int.TryParse(match.Value, out var value) ? value : 999999;
    }

    private static string CleanLabel(string value)
    {
        value = Text(value);
        value = value.Replace("立即播放", string.Empty, StringComparison.Ordinal).Trim();
        return value;
    }

    private static (int Sid, int Nid) PlayRoute(string href)
    {
        if (string.IsNullOrWhiteSpace(href)) return (-1, -1);
        var path = href;
        try { path = new Uri(href, UriKind.RelativeOrAbsolute).IsAbsoluteUri ? new Uri(href).AbsolutePath : href; }
        catch { }
        path = Regex.Replace(path, @"\.html?$", string.Empty, RegexOptions.IgnoreCase);
        var numbers = Regex.Matches(path, @"\d+");
        if (numbers.Count == 0) return (-1, -1);
        var nid = int.TryParse(numbers[^1].Value, out var n) ? n : -1;
        var sid = numbers.Count >= 2 && int.TryParse(numbers[^2].Value, out var s) ? s : 0;
        return (sid, nid);
    }

    private static bool IsPlayHref(string href)
        => !string.IsNullOrWhiteSpace(href) && PlayHrefPattern.IsMatch(href);

    private static string ParsePlayerJson(string html, string pageUrl)
    {
        var match = PlayerPattern.Match(html);
        if (!match.Success) match = PlayerFallbackPattern.Match(html);
        if (!match.Success) return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(match.Groups["json"].Value);
            var root = document.RootElement;
            if (root.TryGetProperty("encrypt", out var encrypt))
            {
                var encrypted = encrypt.ValueKind switch
                {
                    JsonValueKind.Number => encrypt.GetInt32() != 0,
                    JsonValueKind.String => encrypt.GetString() is not (null or "" or "0"),
                    _ => true,
                };
                if (encrypted) return string.Empty;
            }

            foreach (var key in new[] { "url", "url_next" })
            {
                if (!root.TryGetProperty(key, out var urlNode)) continue;
                var url = urlNode.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(url)) continue;
                return Normalize(url, pageUrl);
            }
        }
        catch
        {
            // 页面数据不是完整 JSON 时继续尝试直链。
        }
        return string.Empty;
    }

    private static string ParseDirectMedia(string html, string pageUrl)
    {
        var normalized = html.Replace("\\/", "/", StringComparison.Ordinal);
        foreach (Match match in MediaPattern.Matches(normalized))
        {
            var value = Normalize(WebUtility.HtmlDecode(match.Value), pageUrl);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return string.Empty;
    }

    private static List<string> FindPlayPages(string html, string pageUrl)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<(string Url, int Sid, int Nid)>();
        foreach (Match match in HrefPattern.Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            if (href.Length == 0 || href.StartsWith('#') ||
                href.StartsWith("javascript", StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsPlayHref(href)) continue;

            var absolute = Normalize(href, pageUrl);
            if (string.IsNullOrWhiteSpace(absolute) || !seen.Add(absolute)) continue;

            var episode = EpisodePattern.Match(href);
            var sid = episode.Success ? int.TryParse(episode.Groups["sid"].Value, out var s) ? s : 999 : 999;
            var nid = episode.Success ? int.TryParse(episode.Groups["nid"].Value, out var n) ? n : 999 : 999;
            // 0-0 / 1-0 通常是预告或播放器壳页，放在真正剧集之后。
            if (nid == 0) sid += 1000;
            candidates.Add((absolute, sid, nid));
        }

        return candidates
            .OrderBy(c => c.Sid)
            .ThenBy(c => c.Nid)
            .Select(c => c.Url)
            .ToList();
    }

    private static string Normalize(string raw, string baseUrl)
    {
        raw = WebUtility.HtmlDecode(raw ?? string.Empty).Trim();
        if (raw.Length == 0) return string.Empty;
        raw = raw.Replace("\\/", "/", StringComparison.Ordinal);
        if (raw.StartsWith("//", StringComparison.Ordinal)) raw = "https:" + raw;
        try
        {
            var absolute = new Uri(new Uri(baseUrl, UriKind.Absolute), raw).ToString();
            return absolute.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? absolute : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadEpisode(string pageUrl)
    {
        var episode = EpisodePattern.Match(pageUrl);
        if (!episode.Success) return "第 1 集";
        if (!int.TryParse(episode.Groups["nid"].Value, out var nid) || nid <= 0) return "第 1 集";
        return $"第 {nid} 集";
    }

    private static string ReadTitle(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>(?<t>.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var title = Text(match.Groups["t"].Value);
        title = Regex.Split(title, @"(?:详情介绍|在线观看|免费在线| - )")[0].Trim();
        return title.Length > 0 ? title : "追番 / 影视";
    }

    private static string ReadDescription(string html, string fallback)
    {
        var description = WebUtility.HtmlDecode(Attribute(html, "content", "og:description")).Trim();

        // 有些站点把 og:description 写成 webkit 之类的前端占位，不能直接展示。
        if (!LooksLikeDescription(description))
        {
            var meta = Regex.Match(html,
                @"<meta[^>]+name=""(?:description|Description)""[^>]+content=""(?<v>[^""]*)""",
                RegexOptions.IgnoreCase);
            if (!meta.Success)
            {
                meta = Regex.Match(html,
                    @"<meta[^>]+content=""(?<v>[^""]*)""[^>]+name=""(?:description|Description)""",
                    RegexOptions.IgnoreCase);
            }
            description = meta.Success ? WebUtility.HtmlDecode(meta.Groups["v"].Value).Trim() : string.Empty;
        }

        if (!LooksLikeDescription(description))
        {
            var intro = Regex.Match(html,
                @"<(?:div|p)[^>]*(?:introduction-content|yplx_c3|desc)[^>]*>(?<body>.*?)</(?:div|p)>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            description = intro.Success ? Text(intro.Groups["body"].Value) : string.Empty;
        }

        if (!LooksLikeDescription(description)) return fallback;
        for (var i = 0; i < 2; i++)
        {
            description = WebUtility.HtmlDecode(description).Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        }
        description = Regex.Replace(description, @"\s+", " ").Trim();
        description = Regex.Replace(description, @"^\s*(?:剧情)?[:：]\s*", string.Empty);
        return description.Length > 420 ? description[..420] + "…" : description;
    }

    private static bool LooksLikeDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim();
        if (text.Length < 12) return false;
        var lower = text.ToLowerInvariant();
        return !lower.Contains("webkit") && !lower.Contains("width=device") &&
               !lower.Contains("initial-scale") && !lower.Contains("text/html") &&
               !lower.Contains("<!doctype");
    }

    private static string BuildMeta(string html, AnimeItem item)
    {
        var status = StatusPattern.Match(html);
        var parts = new List<string>();
        if (status.Success)
        {
            var value = Text(status.Value);
            if (value.Length > 0) parts.Add(value);
        }
        if (!string.IsNullOrWhiteSpace(item.Note) && !parts.Contains(item.Note)) parts.Add(item.Note);
        if (!string.IsNullOrWhiteSpace(item.Kind)) parts.Add(item.Kind);
        if (!string.IsNullOrWhiteSpace(item.Region) && item.Region != "其他") parts.Add(item.Region);
        if (!string.IsNullOrWhiteSpace(item.Source)) parts.Add(item.Source);
        return string.Join(" · ", parts);
    }

    private static string Attribute(string html, string name, string? nearToken = null)
    {
        if (!string.IsNullOrWhiteSpace(nearToken))
        {
            var tokenIndex = html.IndexOf(nearToken, StringComparison.OrdinalIgnoreCase);
            if (tokenIndex >= 0)
            {
                var tagStart = html.LastIndexOf('<', tokenIndex);
                var tagEnd = html.IndexOf('>', tokenIndex);
                if (tagStart >= 0 && tagEnd > tagStart)
                {
                    var snippet = html[tagStart..(tagEnd + 1)];
                    var nested = Regex.Match(snippet, name + @"=""(?<v>[^""]*)""", RegexOptions.IgnoreCase);
                    if (nested.Success) return WebUtility.HtmlDecode(nested.Groups["v"].Value).Trim();
                }
            }
        }
        var match = Regex.Match(html, name + @"=""(?<v>[^""]*)""", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value).Trim() : string.Empty;
    }

    private static string Text(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var plain = TagPattern.Replace(html, " ");
        plain = WebUtility.HtmlDecode(plain);
        return SpacePattern.Replace(plain, " ").Trim();
    }

    private static string SafeHost(string url)
    {
        try { return new Uri(url).Host; } catch { return string.Empty; }
    }

    private static async Task<string> FetchAsync(string url, string referer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
        if (!string.IsNullOrWhiteSpace(referer))
            request.Headers.Referrer = new Uri(referer, UriKind.Absolute);
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return string.Empty;
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(18) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        return client;
    }
}
