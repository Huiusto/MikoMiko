using MikoMiko.Models;

namespace MikoMiko.Services;

/// <summary>内容聚合：优先请求米游社接口，失败时回落到离线演示数据。</summary>
public sealed class ContentService
{
    private readonly AppSettings _settings;
    private List<ForumSection>? _forumSections;

    /// <summary>随机流用的翻页游标池（各版块 last_id），让「刷更多」能跳到不同时间段。</summary>
    private readonly List<string> _cursorPool = new();

    private static readonly int[] Games = { 2, 1, 6, 4, 5, 3, 8 };

    /// <summary>首页随机流使用全站搜索取样，覆盖米游社所有板块，而不是只抓某几个版块。</summary>
    private static readonly string[] FeedKeywords =
    {
        "原神", "崩坏3", "星穹铁道", "绝区零", "未定事件簿", "大别野", "同人", "攻略",
        "剧情", "绘画", "摄影", "手书", "萌新", "深渊", "抽卡", "角色", "活动", "考据",
    };

    /// <summary>视频流取样关键词（跨游戏、跨版块）。</summary>
    private static readonly string[] VideoKeywords =
    {
        "视频", "短片", "实况", "剪辑", "动画", "混剪", "MMD", "手书", "整活", "二创",
    };

    public ContentService(AppSettings settings)
    {
        _settings = settings;
        Api = new MiyousheApi(settings.Cookie, settings.Quality);
    }

    public MiyousheApi Api { get; private set; }

    /// <summary>本次数据是否来自演示数据。</summary>
    public bool IsDemo { get; private set; }

    public void RebuildApi() => Api = new MiyousheApi(_settings.Cookie, _settings.Quality);

    public async Task<IReadOnlyList<FeedItem>> GetHomeAsync(bool latest, CancellationToken ct = default)
    {
        try
        {
            var gids = _settings.CommunityId;
            if (latest)
            {
                var posts = await Api.GetForumPostsAsync(gids, ForumIdFor(gids), 24, ct);
                if (posts.Count > 0)
                {
                    IsDemo = false;
                    return posts;
                }
            }

            var recommended = await Api.GetRecommendedAsync(gids, 24, ct);
            if (recommended.Count > 0)
            {
                IsDemo = false;
                return recommended;
            }
        }
        catch
        {
            // 网络异常 → 演示数据
        }

        IsDemo = true;
        return DemoData.Home();
    }

    /// <summary>官方页：只显示米游社官方公告 / 活动 / 资讯。</summary>
    public async Task<IReadOnlyList<FeedItem>> GetOfficialAsync(CancellationToken ct = default)
    {
        try
        {
            var items = await Api.GetOfficialPostsAsync(Games.Take(4), 16, ct);
            if (items.Count > 0)
            {
                IsDemo = false;
                return items;
            }
        }
        catch
        {
            // 回落演示数据
        }

        IsDemo = true;
        return DemoData.Home().Where(i => i.IsOfficial).ToList() is { Count: > 0 } official
            ? official
            : DemoData.Home();
    }

    /// <summary>分区 / 版块目录（结果缓存，避免每次打开帖子页都请求）。</summary>
    public async Task<List<ForumSection>> GetForumSectionsAsync(CancellationToken ct = default)
    {
        _forumSections ??= await Api.GetForumSectionsAsync(ct);
        return _forumSections;
    }

    /// <summary>按分区 / 版块 / 排序读取帖子流。</summary>
    public async Task<(IReadOnlyList<FeedItem> Items, string LastId, bool IsLast)> GetForumFeedAsync(
        int gameId, int forumId, int sortType, int pageSize = 20, string lastId = "",
        CancellationToken ct = default)
    {
        var feed = await Api.GetForumFeedAsync(gameId, forumId, sortType, pageSize, lastId, ct);
        if (feed.Items.Count > 0) IsDemo = false;
        return feed;
    }

    /// <summary>角色管理里打开的游戏详情：该游戏官方帖流。</summary>
    public async Task<IReadOnlyList<FeedItem>> GetGamePostsAsync(int gameId, int size = 12,
        CancellationToken ct = default)
    {
        try
        {
            var items = await Api.GetGamePostsAsync(gameId, size, ct);
            if (items.Count > 0) IsDemo = false;
            return items;
        }
        catch
        {
            return Array.Empty<FeedItem>();
        }
    }

    public async Task<IReadOnlyList<FeedItem>> GetVideosAsync(CancellationToken ct = default)
    {
        var gameIds = new[] { 6, 5, 3, 2 };
        try
        {
            var videos = await Api.GetVideosAsync(gameIds, 20, 24, ct);
            if (videos.Count > 0)
            {
                IsDemo = false;
                return videos;
            }
        }
        catch
        {
            // 忽略
        }

        IsDemo = true;
        return DemoData.Videos();
    }

    /// <summary>
    /// 随机帖子流：用随机关键词走全站搜索，能把所有米游社板块的帖子混在一起；
    /// 搜索偶尔失败时再回落到版块列表。
    /// </summary>
    public async Task<IReadOnlyList<FeedItem>> GetRandomPostsAsync(int take, IReadOnlyCollection<string> exclude,
        CancellationToken ct = default)
    {
        var collected = new List<FeedItem>();

        for (var attempt = 0; attempt < 4 && collected.Count < take; attempt++)
        {
            var keyword = FeedKeywords[Random.Shared.Next(FeedKeywords.Length)];
            var lastId = _cursorPool.Count > 0 && Random.Shared.Next(0, 2) == 0
                ? _cursorPool[Random.Shared.Next(_cursorPool.Count)]
                : null;

            try
            {
                var search = await Api.SearchPostsAsync(keyword, take + 10, lastId, ct);
                if (!string.IsNullOrEmpty(search.LastId) && !_cursorPool.Contains(search.LastId))
                {
                    _cursorPool.Add(search.LastId);
                    if (_cursorPool.Count > 64) _cursorPool.RemoveAt(0);
                }

                collected.AddRange(search.Items.Where(p => !exclude.Contains(p.PostId)));
            }
            catch
            {
                // 单个关键词失败继续换一个
            }
        }

        // 搜索整体不可用时用版块列表兜底
        if (collected.Count == 0)
        {
            for (var attempt = 0; attempt < 3 && collected.Count == 0; attempt++)
            {
                var gids = Games[Random.Shared.Next(Games.Length)];
                var forumId = ForumIdFor(gids);
                try
                {
                    var posts = await Api.GetForumPostsAsync(gids, forumId, take + 10, ct,
                        Random.Shared.Next(1, 3));
                    collected.AddRange(posts.Where(p => !exclude.Contains(p.PostId)));
                }
                catch
                {
                    // 忽略
                }
            }
        }

        if (collected.Count > 0)
        {
            IsDemo = false;
            return collected
                .GroupBy(i => i.PostId)
                .Select(g => g.First())
                .OrderBy(_ => Random.Shared.Next())
                .Take(take)
                .ToList();
        }

        IsDemo = true;
        return DemoData.Home().OrderBy(_ => Random.Shared.Next()).Take(take).ToList();
    }

    /// <summary>官方全站搜索（首页搜索框用）。</summary>
    public async Task<IReadOnlyList<FeedItem>> SearchPostsAsync(string keyword, int take = 30,
        CancellationToken ct = default)
    {
        var collected = new List<FeedItem>();
        var lastId = string.Empty;
        for (var page = 0; page < 2 && collected.Count < take; page++)
        {
            var result = await Api.SearchPostsAsync(keyword, take, lastId, ct);
            collected.AddRange(result.Items);
            lastId = result.LastId;
            if (result.IsLast || string.IsNullOrEmpty(lastId)) break;
        }

        IsDemo = false;
        return collected.GroupBy(i => i.PostId).Select(g => g.First()).Take(take).ToList();
    }

    /// <summary>随机视频流：用视频类关键词在全站搜索，覆盖所有用户 / 所有板块发布的视频。</summary>
    public async Task<IReadOnlyList<FeedItem>> GetRandomVideosAsync(int take, IReadOnlyCollection<string> exclude,
        CancellationToken ct = default)
    {
        var collected = new List<FeedItem>();

        for (var attempt = 0; attempt < 6 && collected.Count < take; attempt++)
        {
            var keyword = VideoKeywords[Random.Shared.Next(VideoKeywords.Length)];
            var lastId = _cursorPool.Count > 0 && Random.Shared.Next(0, 2) == 0
                ? _cursorPool[Random.Shared.Next(_cursorPool.Count)]
                : null;

            try
            {
                var search = await Api.SearchPostsAsync(keyword, take + 12, lastId, ct);
                if (!string.IsNullOrEmpty(search.LastId) && !_cursorPool.Contains(search.LastId))
                {
                    _cursorPool.Add(search.LastId);
                    if (_cursorPool.Count > 64) _cursorPool.RemoveAt(0);
                }

                collected.AddRange(search.Items.Where(p => p.IsVideo && !string.IsNullOrEmpty(p.VideoUrl)
                                                             && !exclude.Contains(p.PostId)));
            }
            catch
            {
                // 忽略
            }
        }

        // 全站搜索里暂时没有视频时，退回版块列表筛选
        if (collected.Count == 0)
        {
            for (var attempt = 0; attempt < 5 && collected.Count < take; attempt++)
            {
                var gids = Games[Random.Shared.Next(Games.Length)];
                try
                {
                    var posts = await Api.GetForumPostsAsync(gids, ForumIdFor(gids), take + 12, ct,
                        Random.Shared.Next(1, 3));
                    collected.AddRange(posts.Where(p => p.IsVideo && !string.IsNullOrEmpty(p.VideoUrl)
                                                         && !exclude.Contains(p.PostId)));
                }
                catch
                {
                    // 忽略
                }
            }
        }

        if (collected.Count > 0)
        {
            IsDemo = false;
            return collected
                .GroupBy(i => i.PostId)
                .Select(g => g.First())
                .OrderBy(_ => Random.Shared.Next())
                .Take(take)
                .ToList();
        }

        IsDemo = true;
        return DemoData.Videos().OrderBy(_ => Random.Shared.Next()).Take(take).ToList();
    }

    /// <summary>
    /// 动态页：只保留「我关注的人」发布的内容。
    /// 米游社没有公开的关注流接口，这里用带 Cookie 的推荐流（webHome）里 user.is_following=true 的帖子，
    /// 未登录时由调用方展示登录引导，不再回落到演示数据。
    /// </summary>
    public async Task<IReadOnlyList<MomentItem>> GetMomentsAsync(CancellationToken ct = default)
    {
        if (!Api.HasCookie) return Array.Empty<MomentItem>();

        // 先拿到完整关注列表，再逐个取作者发布过的帖子（不限新旧），按发布时间倒序聚合。
        var users = await Api.GetFollowingAsync(ct);
        var posts = new List<FeedItem>();
        if (users.Count > 0)
        {
            // 并发拉取关注作者的帖子，最多 40 人、每人 24 条；不再限制“仅新动态”。
            using var gate = new SemaphoreSlim(6);
            var tasks = users.Take(40).Select(async user =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    // 不指定 gids，取该用户在所有版块（原神、星铁、绝区零、大别野等）的最新发布。
                    var items = await Api.GetUserPostsAsync(user.Uid, 0, 24, ct);
                    return items;
                }
                catch
                {
                    return new List<FeedItem>();
                }
                finally
                {
                    gate.Release();
                }
            }).ToArray();

            var lists = await Task.WhenAll(tasks);
            foreach (var list in lists)
            {
                if (posts.Count >= 400) break;
                posts.AddRange(list.Take(400 - posts.Count));
            }
        }

        // 关注接口不可用时，退回从推荐流中挑选已关注作者的帖子
        if (posts.Count == 0)
        {
            var tasks = Games.Select(async gids =>
            {
                try
                {
                    var items = await Api.GetRecommendedAsync(gids, 20, ct);
                    return items.Where(i => i.IsFollowing).ToArray();
                }
                catch
                {
                    return Array.Empty<FeedItem>();
                }
            }).ToArray();

            foreach (var list in await Task.WhenAll(tasks))
            {
                if (posts.Count >= 50) break;
                posts.AddRange(list.Take(50 - posts.Count));
            }
        }

        IsDemo = false;
        return posts
            .GroupBy(i => i.PostId)
            .Select(g => g.OrderByDescending(i => i.CreatedAt).First())
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new MomentItem
            {
                Id = i.PostId,
                PostId = i.PostId,
                Author = i.Author,
                Avatar = i.AuthorAvatar,
                TimeText = i.TimeText,
                Text = string.IsNullOrWhiteSpace(i.Summary) ? i.Title : i.Summary,
                Topic = string.IsNullOrEmpty(i.ForumName) ? string.Empty : $"#{i.ForumName}#",
                LikeText = i.LikeText,
                ReplyText = i.ReplyText,
                IsOfficial = i.IsOfficial,
                IsFollowing = true,
                IsVideo = i.IsVideo,
                VideoCover = i.Cover,
                DurationText = i.DurationText,
                Images = i.Images.Take(3).ToList(),
            })
            .ToList();
    }

    public async Task<FeedItem> GetDetailAsync(FeedItem item, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(item.PostId) || item.PostId.StartsWith("demo", StringComparison.OrdinalIgnoreCase))
        {
            return item;
        }

        try
        {
            var detail = await Api.GetPostFullAsync(item.PostId, ct);
            if (detail is not null)
            {
                detail.Author = item.Author;
                detail.AuthorAvatar = item.AuthorAvatar;
                detail.IsVideo = item.IsVideo;
                if (string.IsNullOrWhiteSpace(detail.VideoUrl)) detail.VideoUrl = item.VideoUrl;
                detail.DurationText = item.DurationText;
                detail.Images = detail.Images.Count > 0 ? detail.Images : item.Images;
                detail.Title = string.IsNullOrWhiteSpace(detail.Title) ? item.Title : detail.Title;
                if (string.IsNullOrWhiteSpace(detail.WebUrl)) detail.WebUrl = item.WebUrl;
                return detail;
            }
        }
        catch
        {
            // 失败时显示列表页已缓存的信息
        }

        return item;
    }

    private static int ForumIdFor(int gameId) => gameId switch
    {
        2 => 28,
        5 => 47,
        6 => 53,
        3 => 40,
        8 => 58,
        _ => 47,
    };
}
