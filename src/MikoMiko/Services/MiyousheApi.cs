using MikoMiko.Models;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MikoMiko.Services;

/// <summary>米游社社区接口封装（公开接口，参考 TeyvatGuide 的实现方式）。</summary>
public sealed class MiyousheApi
{
    private const string WebHomeUrl = "https://bbs-api-static.miyoushe.com/apihub/wapi/webHome";
    private const string PostApi = "https://bbs-api.miyoushe.com/post/wapi";

    private readonly HttpClient _http;
    private readonly string _cookie;

    public MiyousheApi(string cookie = "", string quality = "720P")
    {
        _cookie = cookie ?? string.Empty;
        Quality = string.IsNullOrWhiteSpace(quality) ? "720P" : quality;
        _http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://www.miyoushe.com/");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://www.miyoushe.com");
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        }
    }

    public string Quality { get; set; }

    // ---------------------------------------------------------------- 账号操作（需 Cookie）

    public bool HasCookie => !string.IsNullOrWhiteSpace(_cookie);

    /// <summary>点赞（同步到当前 Cookie 对应的米游社账号）。</summary>
    public Task<bool> LikePostAsync(string postId, CancellationToken ct = default)
        => PostActionAsync("likePost", postId, ct);

    /// <summary>收藏。</summary>
    public Task<bool> FavoritePostAsync(string postId, CancellationToken ct = default)
        => PostActionAsync("collectPost", postId, ct);

    /// <summary>
    /// 发表评论（同步到米游社）。官方网页端在 post/wapi/releaseReply，
    /// 需要 post_id、content、structured_content（Quill delta JSON）和 reply_id（一级评论为 0）。
    /// </summary>
    public async Task<(bool Ok, string Message)> ReleaseReplyAsync(string postId, string content, string replyId = "0",
        CancellationToken ct = default)
    {
        if (!HasCookie) return (false, "请先在「我的」扫码登录米游社");
        if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(content))
            return (false, "评论内容不能为空");

        try
        {
            var text = content.Trim();
            var delta = JsonSerializer.Serialize(new
            {
                ops = new object[] { new { insert = text + "\n" } },
            });

            var payload = JsonSerializer.Serialize(new
            {
                post_id = postId,
                content = text,
                structured_content = delta,
                reply_id = string.IsNullOrWhiteSpace(replyId) ? "0" : replyId,
            });

            var url = $"{PostApi}/releaseReply";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("x-rpc-client_type", "4");
            request.Headers.TryAddWithoutValidation("x-rpc-app_version", "2.11.1");

            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var code = root.TryGetProperty("retcode", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetInt32() : -1;
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() ?? string.Empty : string.Empty;

            return code == 0
                ? (true, "评论已发布并同步到米游社")
                : (false, string.IsNullOrWhiteSpace(message) ? $"发布失败（{code}）" : message);
        }
        catch (Exception ex)
        {
            return (false, "发布失败：" + ex.Message);
        }
    }

    private async Task<bool> PostActionAsync(string action, string postId, CancellationToken ct)
    {
        if (!HasCookie || string.IsNullOrWhiteSpace(postId)) return false;

        try
        {
            var url = $"{PostApi}/{action}?post_id={postId}";
            using var content = new StringContent(string.Empty);
            content.Headers.TryAddWithoutValidation("x-rpc-client_type", "4");
            content.Headers.TryAddWithoutValidation("x-rpc-app_version", "2.11.1");
            var response = await _http.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("retcode", out var code)
                   && code.ValueKind == JsonValueKind.Number
                   && code.GetInt32() == 0;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- 首页

    /// <summary>首页推荐（webHome → recommended_posts）。</summary>
    public async Task<IReadOnlyList<FeedItem>> GetRecommendedAsync(int gids, int size = 24, CancellationToken ct = default)
    {
        var url = $"{WebHomeUrl}?gids={gids}&page_size={size}";
        var root = await GetJsonAsync(url, ct);
        var list = Obj(Obj(root, "data"), "recommended_posts");

        var items = new List<FeedItem>();
        foreach (var element in Items(list))
        {
            var item = ParseHomeItem(element);
            if (item is not null) items.Add(item);
        }

        return items;
    }

    /// <summary>版块帖子列表（getForumPostList），支持按热度 / 最新排序与 last_id 翻页。</summary>
    public async Task<IReadOnlyList<FeedItem>> GetForumPostsAsync(int gids, int forumId, int size = 24,
        CancellationToken ct = default, int sortType = 2, string? lastId = null)
    {
        var url = $"{PostApi}/getForumPostList?forum_id={forumId}&gids={gids}&is_good=false&is_hot=false&page_size={size}&sort_type={sortType}";
        if (!string.IsNullOrWhiteSpace(lastId)) url += $"&last_id={lastId}";
        var root = await GetJsonAsync(url, ct);
        var list = Obj(Obj(root, "data"), "list");

        var items = new List<FeedItem>();
        foreach (var element in Items(list))
        {
            var item = ParseHomeItem(element);
            if (item is null) continue;
            if (string.IsNullOrEmpty(item.ForumName)) item.ForumName = Str(Obj(element, "forum"), "name");
            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// 全站搜索（post/wapi/searchPosts）。不传 gids 时覆盖米游社所有板块，
    /// 返回结果同样带 forum / user / stat / vod_list，可直接复用首页卡片。
    /// </summary>
    public async Task<(IReadOnlyList<FeedItem> Items, string LastId, bool IsLast)> SearchPostsAsync(
        string keyword, int size = 20, string? lastId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return (Array.Empty<FeedItem>(), string.Empty, true);

        var url = $"{PostApi}/searchPosts?keyword={Uri.EscapeDataString(keyword)}&page_size={size}";
        if (!string.IsNullOrWhiteSpace(lastId)) url += $"&last_id={Uri.EscapeDataString(lastId)}";

        var root = await GetJsonAsync(url, ct);
        var data = Obj(root, "data");
        if (data is null) return (Array.Empty<FeedItem>(), string.Empty, true);

        var items = new List<FeedItem>();
        foreach (var element in Items(Obj(data, "posts")))
        {
            var item = ParseHomeItem(element);
            if (item is null) continue;
            if (string.IsNullOrEmpty(item.ForumName)) item.ForumName = Str(Obj(element, "forum"), "name");
            if (string.IsNullOrEmpty(item.Author)) item.Author = Str(Obj(element, "user"), "nickname");
            items.Add(item);
        }

        return (items, Str(data, "last_id"), Obj(data, "is_last")?.ValueKind == JsonValueKind.True);
    }

    // ---------------------------------------------------------------- 视频

    /// <summary>聚合多个版块的视频内容。</summary>
    public async Task<IReadOnlyList<FeedItem>> GetVideosAsync(IEnumerable<int> gameIds, int perGame = 20, int take = 24, CancellationToken ct = default)
    {
        var all = new List<FeedItem>();
        foreach (var gids in gameIds)
        {
            try
            {
                var items = await GetRecommendedAsync(gids, perGame, ct);
                all.AddRange(items.Where(i => i.IsVideo && !string.IsNullOrEmpty(i.VideoUrl)));
            }
            catch
            {
                // 单个版块失败不影响其它版块
            }

            if (all.Count >= take) break;
        }

        return all
            .GroupBy(i => i.PostId)
            .Select(g => g.First())
            .Take(take)
            .ToList();
    }

    // ---------------------------------------------------------------- 详情

    public async Task<FeedItem?> GetPostFullAsync(string postId, CancellationToken ct = default)
    {
        var url = $"{PostApi}/getPostFull?post_id={postId}&read=1";
        var root = await GetJsonAsync(url, ct);
        var post = Obj(Obj(Obj(root, "data"), "post"), "post");
        if (post is null) return null;

        var body = HtmlToText(Str(post, "content"));
        var images = new List<string>();
        foreach (var image in Items(Obj(post, "images")))
        {
            var src = image.ValueKind == JsonValueKind.String ? image.GetString() : Str(image, "url");
            if (!string.IsNullOrWhiteSpace(src)) images.Add(src);
        }

        return new FeedItem
        {
            Id = postId,
            PostId = postId,
            Title = Str(post, "subject"),
            Summary = body,
            Body = body,
            Cover = images.FirstOrDefault() ?? string.Empty,
            TimeText = RelativeTime(Num(post, "created_at")),
            Images = images,
            LikeText = FormatCount(Num(post, "like_num")),
            ReplyText = FormatCount(Num(post, "reply_num")),
            ViewText = FormatCount(Num(post, "view_num")),
            WebUrl = WebUrl(post),
        };
    }

    /// <summary>帖子详情（正文、图片、统计、我的点赞 / 收藏状态、视频直链）。</summary>
    public async Task<PostDetail?> GetPostDetailAsync(string postId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(postId)) return null;

        var url = $"{PostApi}/getPostFull?post_id={postId}&read=1";
        var root = await GetJsonAsync(url, ct);
        var data = Obj(root, "data");
        var post = Obj(data, "post");
        var content = Obj(post, "post");
        if (content is null) return null;

        var detail = new PostDetail
        {
            PostId = postId,
            GameId = (int)Num(content, "game_id"),
            Title = Str(content, "subject"),
            Body = ParseBody(content),
            Author = Str(Obj(post, "user"), "nickname"),
            AuthorAvatar = Str(Obj(post, "user"), "avatar_url"),
            ForumName = Str(Obj(post, "forum"), "name"),
            TimeText = RelativeTime(Num(content, "created_at")),
            WebUrl = WebUrl(content),
            LikeCount = Num(Obj(post, "stat"), "like_num"),
            ReplyCount = Num(Obj(post, "stat"), "reply_num"),
            ViewCount = Num(Obj(post, "stat"), "view_num"),
            FavoriteCount = Num(Obj(post, "stat"), "bookmark_num"),
            IsLiked = Num(Obj(post, "self_operation"), "attitude") > 0,
            IsFavorited = Obj(Obj(post, "self_operation"), "is_collected")?.ValueKind == JsonValueKind.True,
        };

        var images = new List<string>();
        foreach (var image in Items(Obj(content, "images")))
        {
            var src = image.ValueKind == JsonValueKind.String ? image.GetString() : Str(image, "url");
            if (!string.IsNullOrWhiteSpace(src)) images.Add(src!);
        }

        foreach (var src in StructuredImages(content))
        {
            if (!string.IsNullOrWhiteSpace(src) && !images.Contains(src)) images.Add(src);
        }

        detail.Images.AddRange(images);

        var vodList = Obj(post, "vod_list");
        if (Items(vodList).Any())
        {
            var vod = Items(vodList).First();
            detail.IsVideo = true;
            detail.DurationText = DurationText(Num(vod, "duration"));
            detail.VideoUrl = PickResolution(Obj(vod, "resolutions"));
        }

        return detail;
    }

    /// <summary>热门评论（first page）。注意：必须显式带 is_hot=true，否则返回 retcode=-1。</summary>
    public async Task<IReadOnlyList<CommentItem>> GetRepliesAsync(string postId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(postId)) return Array.Empty<CommentItem>();

        var url = $"{PostApi}/getPostReplies?post_id={postId}&page_size=20&is_hot=true";
        var root = await GetJsonAsync(url, ct);
        var list = Obj(Obj(root, "data"), "list");
        var replies = new List<CommentItem>();
        foreach (var element in Items(list))
        {
            var item = ParseComment(element);
            if (item is not null) replies.Add(item);
        }

        return replies;
    }

    private static CommentItem? ParseComment(JsonElement element)
    {
        var reply = Obj(element, "reply");
        if (reply is null) return null;

        var user = Obj(element, "user");
        var item = new CommentItem
        {
            ReplyId = Str(reply, "reply_id"),
            Author = Str(user, "nickname"),
            Avatar = Str(user, "avatar_url"),
            Level = (int)Num(Obj(user, "level_exp"), "level"),
            IpRegion = Str(user, "ip_region"),
            Content = Str(reply, "content"),
            TimeText = RelativeTime(Num(reply, "created_at")),
            LikeText = FormatCount(Num(Obj(element, "stat"), "like_num")),
            IsLiked = Num(Obj(element, "self_operation"), "reply_vote_attitude") > 0,
            IsAuthor = Obj(element, "is_lz")?.ValueKind == JsonValueKind.True,
        };

        foreach (var sub in Items(Obj(element, "sub_replies")))
        {
            var child = ParseComment(sub);
            if (child is not null) item.Children.Add(child);
        }

        return item;
    }

    // ---------------------------------------------------------------- 账号

    public async Task<(bool Ok, string Nickname, int Level, string Avatar, string Uid)> GetUserInfoAsync(int gids, CancellationToken ct = default)
    {
        var url = $"https://bbs-api.miyoushe.com/user/wapi/getUserFullInfo?gids={gids}";
        var root = await GetJsonAsync(url, ct);
        var data = Obj(root, "data");
        var user = Obj(data, "user_info");
        if (user is null) return (false, string.Empty, 0, string.Empty, string.Empty);

        return (true, Str(user, "nickname"), (int)Num(Obj(user, "level_exp"), "level"),
            Str(user, "avatar_url"), Str(user, "uid"));
    }

    // ---------------------------------------------------------------- helpers

    private async Task<JsonElement?> GetJsonAsync(string url, CancellationToken ct)
    {
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("retcode", out var retcode) &&
            retcode.ValueKind == JsonValueKind.Number &&
            retcode.GetInt32() != 0)
        {
            return null;
        }

        return root.Clone();
    }

    private FeedItem? ParseHomeItem(JsonElement element)
    {
        var post = Obj(element, "post");
        if (post is null) return null;

        var stat = Obj(element, "stat");
        var user = Obj(element, "user");
        var vodList = Obj(element, "vod_list");

        var item = new FeedItem
        {
            Id = Str(post, "post_id"),
            PostId = Str(post, "post_id"),
            GameId = (int)Num(post, "game_id"),
            Title = Str(post, "subject"),
            Summary = Str(post, "content"),
            Cover = CoverOf(element) ?? Str(post, "cover"),
            Author = Str(user, "nickname"),
            AuthorAvatar = Str(user, "avatar_url"),
            TimeText = RelativeTime(Num(post, "created_at")),
            ViewText = FormatCount(Num(stat, "view_num")),
            ReplyText = FormatCount(Num(stat, "reply_num")),
            LikeText = FormatCount(Num(stat, "like_num")),
            TagText = TagOf(Num(stat, "like_num")),
            IsOfficial = Obj(Obj(post, "post_status"), "is_official")?.ValueKind == JsonValueKind.True,
            IsFollowing = Obj(user, "is_following")?.ValueKind == JsonValueKind.True,
            ForumName = Str(Obj(element, "forum"), "name"),
            IsVideo = Items(vodList).Any(),
            WebUrl = WebUrl(post),
        };

        if (item.IsVideo)
        {
            var vod = Items(vodList).First();
            item.DurationText = DurationText(Num(vod, "duration"));
            item.VideoUrl = PickResolution(Obj(vod, "resolutions"));
        }

        foreach (var image in Items(Obj(post, "images")))
        {
            if (image.ValueKind == JsonValueKind.String)
            {
                var src = image.GetString();
                if (!string.IsNullOrWhiteSpace(src)) item.Images.Add(src!);
            }
        }

        // 搜索结果把封面图放在顶层 image_list 里，帖子内 images 可能为空
        if (item.Images.Count == 0)
        {
            foreach (var image in Items(Obj(element, "image_list")))
            {
                var src = image.ValueKind == JsonValueKind.String
                    ? image.GetString()
                    : Str(image, "url");
                if (!string.IsNullOrWhiteSpace(src) && !item.Images.Contains(src!)) item.Images.Add(src!);
            }
        }

        if (string.IsNullOrEmpty(item.Cover) && item.Images.Count > 0) item.Cover = item.Images[0];

        return item;
    }

    private string PickResolution(JsonElement? resolutions)
    {
        var best = string.Empty;
        var bestHeight = 0;
        var exact = string.Empty;

        foreach (var resolution in Items(resolutions))
        {
            var url = Str(resolution, "url");
            var label = Str(resolution, "label");
            var height = (int)Num(resolution, "height");
            if (string.IsNullOrEmpty(url)) continue;
            if (string.Equals(label, Quality, StringComparison.OrdinalIgnoreCase)) exact = url;
            if (height > bestHeight)
            {
                bestHeight = height;
                best = url;
            }
        }

        return string.IsNullOrEmpty(exact) ? best : exact;
    }

    private static string? CoverOf(JsonElement element)
    {
        var cover = Obj(element, "cover");
        if (cover is null || cover.Value.ValueKind != JsonValueKind.Object) return null;
        var crop = Obj(cover, "crop");
        var cropUrl = Str(crop, "url");
        return string.IsNullOrEmpty(cropUrl) ? Str(cover, "url") : cropUrl;
    }

    private static string WebUrl(JsonElement? post)
    {
        var postId = Str(post, "post_id");
        var gameId = (int)Num(post, "game_id");
        var slug = gameId switch
        {
            1 => "bh3",
            2 => "ys",
            3 => "bh2",
            4 => "wd",
            5 => "dby",
            6 => "sr",
            8 => "zzz",
            _ => "dby",
        };
        return $"https://www.miyoushe.com/{slug}/article/{postId}";
    }

    private static JsonElement? Obj(JsonElement? element, string name)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return null;
        var value = element.Value;
        if (!value.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.Null ? null : prop;
    }

    private static IEnumerable<JsonElement> Items(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Array) return Array.Empty<JsonElement>();
        return element.Value.EnumerateArray();
    }

    private static string Str(JsonElement? element, string name)
    {
        var value = Obj(element, name);
        if (value is null) return string.Empty;
        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.Value.ToString(),
            _ => string.Empty,
        };
    }

    private static long Num(JsonElement? element, string name)
    {
        var value = Obj(element, name);
        if (value is null) return 0;
        return value.Value.ValueKind switch
        {
            JsonValueKind.Number => value.Value.TryGetInt64(out var parsed) ? parsed : 0,
            JsonValueKind.String => long.TryParse(value.Value.GetString(), out var parsed) ? parsed : 0,
            _ => 0,
        };
    }

    public static string FormatCount(long value)
    {
        if (value <= 0) return "0";
        if (value >= 100_000_000) return $"{value / 100_000_000.0:0.#}亿";
        if (value >= 10_000) return $"{value / 10_000.0:0.#}万";
        return value.ToString();
    }

    private static string TagOf(long likeCount)
        => likeCount >= 10_000 ? $"{FormatCount(likeCount)}点赞" : string.Empty;

    private static string DurationText(long milliseconds)
    {
        if (milliseconds <= 0) return string.Empty;
        var span = TimeSpan.FromMilliseconds(milliseconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }

    public static string RelativeTime(long unixSeconds)
    {
        if (unixSeconds <= 0) return string.Empty;
        var time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        var diff = DateTimeOffset.Now - time;
        if (diff.TotalMinutes < 1) return "刚刚";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} 分钟前";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} 小时前";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} 天前";
        return time.ToString("yyyy-MM-dd");
    }

    /// <summary>正文：图片帖的 content 是一段 JSON（{"imgs":[...],"text":"..."}），需要取 text 并去掉标记。</summary>
    private static string ParseBody(JsonElement? contentElement)
    {
        if (contentElement is null) return string.Empty;
        var content = contentElement.Value;
        var raw = Str(content, "content");
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    var text = Str(root, "text");
                    if (root.TryGetProperty("desc", out var desc) && string.IsNullOrWhiteSpace(text))
                        text = desc.ValueKind == JsonValueKind.String ? desc.GetString() ?? string.Empty : string.Empty;
                    return HtmlToText(text);
                }
            }
            catch (JsonException)
            {
                // 不是标准 JSON 就按普通 HTML/纯文本处理
            }
        }

        return HtmlToText(raw);
    }

    /// <summary>结构化正文里的图片地址。</summary>
    private static List<string> StructuredImages(JsonElement? contentElement)
    {
        var result = new List<string>();
        if (contentElement is null) return result;
        var raw = Str(contentElement.Value, "content");
        if (string.IsNullOrWhiteSpace(raw)) return result;

        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}")) return result;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            if (!doc.RootElement.TryGetProperty("imgs", out var imgs)) return result;

            if (imgs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in imgs.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    var url = item.GetString();
                    if (!string.IsNullOrWhiteSpace(url)) result.Add(url!);
                }
            }
            else if (imgs.ValueKind == JsonValueKind.String)
            {
                var value = imgs.GetString();
                if (string.IsNullOrWhiteSpace(value)) return result;
                foreach (var part in value.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    if (part.Trim().StartsWith("http")) result.Add(part.Trim());
            }
        }
        catch (JsonException)
        {
            // 忽略解析失败
        }

        return result;
    }

    private static string HtmlToText(string html)    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</p>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", string.Empty);
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
        text = Regex.Replace(text, "\n{3,}", "\n\n");
        return text.Trim();
    }
}
