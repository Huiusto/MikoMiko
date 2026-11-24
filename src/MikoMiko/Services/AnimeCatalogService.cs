using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MikoMiko.Services;

/// <summary>追番/影视里的一张海报卡片。</summary>
public sealed class AnimeItem
{
    public string Title { get; init; } = string.Empty;
    public string Poster { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string PlayUrl { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public string Kind { get; init; } = "动漫";
    public string Region { get; init; } = "其他";

    public string MetaText => string.IsNullOrWhiteSpace(Note) ? Source : $"{Source} · {Note}";

    /// <summary>海报请求的 Referer：多数追番站图床只允许本站页面外链。</summary>
    public string PosterReferer
    {
        get
        {
            try
            {
                var uri = new Uri(Url);
                return $"{uri.Scheme}://{uri.Host}/";
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

/// <summary>
/// 追番/影视的数据聚合：只解析结构稳定、可直接抓到海报的站点，
/// 其余站点保留为可点击的数据源入口，避免把壳页或客户端渲染页解析成空结果。
/// </summary>
public sealed class AnimeCatalogService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly Regex ModuleAnchor = new(
        @"<a\b(?<attrs>[^>]*\bclass=""[^""]*module-poster-item[^""]*""[^>]*)>(?<body>.*?)</a>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex StuiAnchor = new(
        @"<a\s+(?<tag>[^>]*\bstui-vodlist__thumb\b[^>]*)>(?<body>.*?)</a>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex AnyAnchor = new(
        @"<a\b(?<attrs>[^>]*)>(?<body>.*?)</a>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex TagPattern = new(@"<[^>]+>", RegexOptions.Singleline);
    private static readonly Regex SpacePattern = new(@"\s+");

    public async Task<List<AnimeItem>> LoadHomeAsync(IReadOnlyList<AnimeProvider> providers,
        CancellationToken cancellationToken = default)
    {
        var parseable = providers.Where(p => IsParseable(p) && !string.IsNullOrWhiteSpace(p.Home)).ToList();
        if (parseable.Count == 0) return new List<AnimeItem>();

        var tasks = parseable.Select(provider => FetchProviderAsync(provider.Home, provider, cancellationToken)).ToList();
        var all = Task.WhenAll(tasks);
        await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(9), cancellationToken)).ConfigureAwait(false);
        var lists = tasks.Select(task => task.IsCompletedSuccessfully ? task.Result : new List<AnimeItem>()).ToList();
        return Merge(parseable, lists);
    }

    public async Task<List<AnimeItem>> SearchAsync(IReadOnlyList<AnimeProvider> providers, string keyword,
        CancellationToken cancellationToken = default)
    {
        keyword = keyword.Trim();
        if (keyword.Length == 0) return new List<AnimeItem>();

        var searchable = providers
            .Where(p => IsParseable(p) && !string.IsNullOrWhiteSpace(p.Search))
            .ToList();
        var encoded = Uri.EscapeDataString(keyword);
        var tasks = searchable.Select(provider =>
            FetchProviderAsync(provider.Search.Replace("{q}", encoded), provider, cancellationToken)).ToList();
        var all = Task.WhenAll(tasks);
        await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(9), cancellationToken)).ConfigureAwait(false);
        var lists = tasks.Select(task => task.IsCompletedSuccessfully ? task.Result : new List<AnimeItem>()).ToList();
        return Merge(searchable, lists).Take(120).ToList();
    }

    private static List<AnimeItem> Merge(IReadOnlyList<AnimeProvider> providers, IReadOnlyList<List<AnimeItem>> lists)
    {
        var items = new List<AnimeItem>();
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < providers.Count; index++)
        {
            var provider = providers[index];
            var list = index < lists.Count ? lists[index] : new List<AnimeItem>();
            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Poster)) continue;
                var key = provider.Name + "|" + item.Url;
                if (!urls.Add(key)) continue;
                items.Add(item);
            }
        }
        return items;
    }

    private static async Task<List<AnimeItem>> FetchProviderAsync(string url, AnimeProvider provider,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;
                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var items = url.Contains("/om-search/api", StringComparison.OrdinalIgnoreCase)
                    ? ParseJsonSearch(html, provider)
                    : provider.Parser switch
                    {
                        "stui" => ParseStui(html, provider),
                        "manmankan" => ParseManmankan(html, provider),
                        "mhys" => ParseMhys(html, provider),
                        "generic" => ParseGeneric(html, provider),
                        _ => ParseModulePoster(html, provider),
                    };
                if (items.Count > 0 || attempt == 1) return items.Take(48).ToList();
            }
            catch
            {
                if (attempt == 1) return new List<AnimeItem>();
            }

            await Task.Delay(350, cancellationToken).ConfigureAwait(false);
        }

        return new List<AnimeItem>();
    }

    private static bool IsParseable(AnimeProvider provider)
        => provider.Parser is "module-poster" or "stui" or "manmankan" or "generic" or "mhys";

    private static List<AnimeItem> ParseModulePoster(string html, AnimeProvider provider)
    {
        var items = new List<AnimeItem>();
        foreach (Match match in ModuleAnchor.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var body = match.Groups["body"].Value;
            var href = Attribute(attrs, "href");
            var title = Text(Regex.Match(body, @"<div class=""module-poster-item-title"">(?<v>.*?)</div>",
                RegexOptions.Singleline).Groups["v"].Value);
            if (title.Length == 0)
                title = Attribute(attrs, "title");
            if (title.Length == 0)
                title = Text(Attribute(body, "alt"));

            var poster = Attribute(body, "data-original");
            if (poster.Length == 0) poster = Attribute(body, "data-src");
            if (poster.Length == 0) poster = Attribute(body, "src");

            var noteMatch = Regex.Match(body, @"<div class=""module-(?:item|poster-item)-note"">(?<v>.*?)</div>",
                RegexOptions.Singleline);
            var note = Text(noteMatch.Groups["v"].Value);

            Add(items, provider, href, poster, title, note);
        }
        return items;
    }

    /// <summary>漫漫看：详情页是剧情页，真正的在线观看入口在 *_p.shtml。</summary>
    private static List<AnimeItem> ParseManmankan(string html, AnimeProvider provider)
    {
        var items = new List<AnimeItem>();
        foreach (Match match in AnyAnchor.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var body = match.Groups["body"].Value;
            if (!body.Contains("<img", StringComparison.OrdinalIgnoreCase)) continue;
            var href = Attribute(attrs, "href");
            var title = Attribute(attrs, "title");
            if (title.Length == 0) title = Text(Attribute(body, "alt"));
            var poster = Attribute(body, "data-original");
            if (poster.Length == 0) poster = Attribute(body, "data-src");
            if (poster.Length == 0) poster = Attribute(body, "src");
            if (poster.Contains("loadimg.gif", StringComparison.OrdinalIgnoreCase) &&
                !body.Contains("data-original", StringComparison.OrdinalIgnoreCase)) continue;

            var play = Regex.Replace(href, @"\.shtml$", "_p.shtml", RegexOptions.IgnoreCase);
            Add(items, provider, href, poster, title, "在线观看", play);
            if (items.Count >= 48) break;
        }
        return items;
    }

    /// <summary>麻花影视：海报是懒加载 div 的 data-original，标题在 c1_wz 里。</summary>
    private static List<AnimeItem> ParseMhys(string html, AnimeProvider provider)
    {
        var items = new List<AnimeItem>();
        foreach (Match match in AnyAnchor.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var body = match.Groups["body"].Value;
            if (!body.Contains("data-original", StringComparison.OrdinalIgnoreCase)) continue;
            if (!body.Contains("img_wrapper", StringComparison.OrdinalIgnoreCase)) continue;

            var href = Attribute(attrs, "href");
            var title = Text(Regex.Match(body, @"<div class=""c1_wz"">(?<v>.*?)</div>",
                RegexOptions.Singleline).Groups["v"].Value);
            if (title.Length == 0) title = Attribute(attrs, "title");
            var poster = Attribute(body, "data-original");
            if (poster.Length == 0) poster = Attribute(body, "data-src");
            Add(items, provider, href, poster, title, "热播");
            if (items.Count >= 48) break;
        }
        return items;
    }

    /// <summary>结构不固定的站点：从带图片的详情链接里做保守提取。</summary>
    private static List<AnimeItem> ParseGeneric(string html, AnimeProvider provider)
    {
        var items = new List<AnimeItem>();
        foreach (Match match in AnyAnchor.Matches(html))
        {
            var attrs = match.Groups["attrs"].Value;
            var body = match.Groups["body"].Value;
            if (!body.Contains("<img", StringComparison.OrdinalIgnoreCase)) continue;
            var href = Attribute(attrs, "href");
            if (href.Length == 0 || href.StartsWith('#') || href.StartsWith("javascript", StringComparison.OrdinalIgnoreCase))
                continue;
            var title = Attribute(attrs, "title");
            if (title.Length == 0) title = Text(Attribute(body, "alt"));
            if (title.Length < 2 || title.Length > 60) continue;
            var poster = Attribute(body, "data-original");
            if (poster.Length == 0) poster = Attribute(body, "data-src");
            if (poster.Length == 0) poster = Attribute(body, "src");
            if (poster.Contains("load", StringComparison.OrdinalIgnoreCase) ||
                poster.Contains("logo", StringComparison.OrdinalIgnoreCase)) continue;
            Add(items, provider, href, poster, title, string.Empty);
            if (items.Count >= 48) break;
        }
        return items;
    }

    /// <summary>樱花TV 的搜索接口返回 JSON，列表页本身只有 JS 模板。</summary>
    private static List<AnimeItem> ParseJsonSearch(string json, AnimeProvider provider)
    {
        var items = new List<AnimeItem>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("hits", out var hits) ||
                hits.ValueKind != JsonValueKind.Array) return items;

            foreach (var hit in hits.EnumerateArray())
            {
                var id = hit.TryGetProperty("id", out var idNode) ? idNode.ToString() : string.Empty;
                var cat = hit.TryGetProperty("cat", out var catNode) ? catNode.ToString() : "1";
                var title = hit.TryGetProperty("title", out var titleNode) ? titleNode.GetString() ?? string.Empty : string.Empty;
                var poster = hit.TryGetProperty("pic", out var picNode) ? picNode.GetString() ?? string.Empty : string.Empty;
                var note = hit.TryGetProperty("note", out var noteNode) ? noteNode.GetString() ?? string.Empty : string.Empty;
                if (id.Length == 0) continue;
                Add(items, provider, $"/v/{id}/{cat}", poster, title, note);
            }
        }
        catch
        {
            // 返回体不是预期 JSON 时按空结果处理。
        }
        return items;
    }

    private static List<AnimeItem> ParseStui(string html, AnimeProvider provider)    {
        var items = new List<AnimeItem>();
        foreach (Match match in StuiAnchor.Matches(html))
        {
            var tag = match.Groups["tag"].Value;
            var body = match.Groups["body"].Value;
            var href = Attribute(tag, "href");
            var title = Text(Attribute(tag, "title"));
            var poster = Attribute(tag, "data-original");
            if (poster.Length == 0) poster = Attribute(body, "data-original");
            if (poster.Length == 0) poster = Attribute(body, "src");

            var note = Text(Regex.Match(body, @"<span class=""pic-text[^""]*"">(?<v>.*?)</span>",
                RegexOptions.Singleline).Groups["v"].Value);
            Add(items, provider, href, poster, title, note);
        }
        return items;
    }

    private static void Add(List<AnimeItem> items, AnimeProvider provider, string href, string poster,
        string title, string note, string playUrl = "")
    {
        if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(poster))
            return;

        string absoluteUrl;
        string absolutePoster;
        string absolutePlay;
        try
        {
            var baseUri = new Uri(provider.Home, UriKind.Absolute);
            absoluteUrl = new Uri(baseUri, href).ToString();
            absolutePoster = new Uri(baseUri, poster).ToString();
            absolutePlay = string.IsNullOrWhiteSpace(playUrl) ? absoluteUrl : new Uri(baseUri, playUrl).ToString();
        }
        catch
        {
            return;
        }

        if (!absoluteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;
        if (absolutePoster.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
        // 追番站的封面常混用 http 图床，升级到 https 可避开明文混合内容拦截。
        if (absolutePoster.StartsWith("http://lain.bgm.tv/", StringComparison.OrdinalIgnoreCase))
            absolutePoster = "https://" + absolutePoster[7..];
        items.Add(new AnimeItem
        {
            Title = title,
            Poster = absolutePoster,
            Url = absoluteUrl,
            PlayUrl = absolutePlay,
            Source = provider.Name,
            Note = note,
            Kind = provider.Kind,
            Region = provider.Region,
        });
    }

    private static string Attribute(string html, string name)
    {
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

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            // 部分第三方站点证书链不完整，聚合只读取公开页面，不提交任何账号信息。
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        return client;
    }
}
