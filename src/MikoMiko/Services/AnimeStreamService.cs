using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MikoMiko.Services;

/// <summary>一条可以直接交给播放器的视频流。</summary>
public sealed class AnimePlayback
{
    public string Title { get; init; } = string.Empty;
    public string Episode { get; init; } = "正片";
    public string StreamUrl { get; init; } = string.Empty;
    public string Referer { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;

    public bool IsHls => StreamUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 从追番站点的公开详情/播放页提取真实视频地址。
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
    private static readonly Regex HrefPattern = new(
        @"href=""(?<href>[^""]+)""",
        RegexOptions.IgnoreCase);
    private static readonly Regex EpisodePattern = new(
        @"(?:/(?:p|play)/[^/?#]+-|/mhyskan/[^/?#]+-)(?<sid>\d+)-(?<nid>\d+)",
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

            var playback = ParseHtml(pageHtml, pageUrl, item);
            if (playback is not null) return playback;

            // 详情页没有播放数据时，按“第一播放源、第一集”取播放页。
            var candidates = FindPlayPages(pageHtml, pageUrl);
            foreach (var candidate in candidates.Take(3))
            {
                var playHtml = await FetchAsync(candidate, pageUrl, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(playHtml)) continue;
                playback = ParseHtml(playHtml, candidate, item);
                if (playback is not null) return playback;
            }
        }
        catch
        {
            // 网络失败或页面结构变化时返回空，由界面提示用户。
        }

        return null;
    }

    private static AnimePlayback? ParseHtml(string html, string pageUrl, AnimeItem item)
    {
        var stream = ParsePlayerJson(html, pageUrl);
        if (string.IsNullOrWhiteSpace(stream))
            stream = ParseDirectMedia(html, pageUrl);
        if (string.IsNullOrWhiteSpace(stream)) return null;

        return new AnimePlayback
        {
            Title = item.Title,
            Episode = ReadEpisode(pageUrl),
            StreamUrl = stream,
            Referer = pageUrl,
            Source = item.Source,
        };
    }

    private static string ParsePlayerJson(string html, string pageUrl)
    {
        var match = PlayerPattern.Match(html);
        if (!match.Success) match = PlayerFallbackPattern.Match(html);
        if (!match.Success) return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(match.Groups["json"].Value);
            var root = document.RootElement;
            if (root.TryGetProperty("encrypt", out var encrypt) && encrypt.ValueKind == JsonValueKind.Number &&
                encrypt.GetInt32() != 0)
                return string.Empty;
            if (!root.TryGetProperty("url", out var urlNode)) return string.Empty;
            var url = urlNode.GetString() ?? string.Empty;
            return Normalize(url, pageUrl);
        }
        catch
        {
            return string.Empty;
        }
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
            if (!Regex.IsMatch(href, @"(?:/(?:p|play)/|/mhyskan/|/vodplay/)", RegexOptions.IgnoreCase)) continue;

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
        if (!episode.Success) return "正片";
        if (!int.TryParse(episode.Groups["nid"].Value, out var nid) || nid <= 0) return "正片";
        return $"第 {nid} 集";
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
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        return client;
    }
}
