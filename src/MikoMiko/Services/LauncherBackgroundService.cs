using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikoMiko.Services;

/// <summary>
/// 官方启动器（HoYoPlay）壁纸：按官方接口同步每款游戏当前版本的背景，
/// 图片缓存到本地，动态壁纸（.webm）按需下载后交给启动页的 WebView2 播放。
/// 接口与 MikoNest / Starward 使用的 launcher_id 一致，因此换版本时自动跟随官方。
/// </summary>
public static class LauncherBackgroundService
{
    private const string LauncherId = "jGHBHlcOq1";
    private const string ApiBase = "https://hyp-api.mihoyo.com/hyp/hyp-connect/api/getAllGameBasicInfo";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private static readonly Dictionary<string, string> BizMap = new()
    {
        ["genshin"] = "hk4e_cn",
        ["starrail"] = "hkrpg_cn",
        ["zzz"] = "nap_cn",
        ["bh3"] = "bh3_cn",
    };

    private static readonly Dictionary<string, string> FallbackMap = new()
    {
        ["genshin"] = "fallback_genshin.jpg",
        ["starrail"] = "fallback_starrail.jpg",
        ["zzz"] = "fallback_zzz.jpg",
        ["bh3"] = "fallback_bh3.jpg",
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly Dictionary<string, SemaphoreSlim> RefreshGates = new();
    private static readonly Dictionary<string, Task<string?>> VideoJobs = new();
    private static int _prefetchStarted;

    public static string CacheDir => Path.Combine(SettingsService.CacheDirectory, "launch-bg");

    public static string AssetDir => Path.Combine(SettingsService.DataDirectory, "launch", "assets");

    public sealed class BackgroundItem
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("image")] public string? ImageFile { get; set; }
        [JsonPropertyName("video")] public string? VideoFile { get; set; }
        [JsonPropertyName("videoUrl")] public string? VideoUrl { get; set; }

        [JsonIgnore]
        public string? ImagePath => Existing(ImageFile);

        [JsonIgnore]
        public string? VideoPath => Existing(VideoFile);

        private static string? Existing(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var path = Path.Combine(CacheDir, name);
            return File.Exists(path) ? path : null;
        }
    }

    public sealed class BackgroundSet
    {
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("fetched")] public DateTime FetchedAtUtc { get; set; }
        [JsonPropertyName("items")] public List<BackgroundItem> Items { get; set; } = new();

        [JsonIgnore]
        public bool HasAny => Items.Any(i => i.ImagePath != null);
    }

    private static string MetaFile(string key) => Path.Combine(CacheDir, $"official_{key}.json");
    private static readonly string UiStateFile = Path.Combine(SettingsService.CacheDirectory, "launch-ui.json");

    /// <summary>某款游戏的动态壁纸下载完成，启动页据此刷新。</summary>
    public static event Action<string>? BackgroundReady;

    public static BackgroundSet? GetCached(string gameKey)
    {
        try
        {
            var file = MetaFile(gameKey);
            if (!File.Exists(file)) return null;
            var meta = JsonSerializer.Deserialize<BackgroundSet>(File.ReadAllText(file));
            return meta is { HasAny: true } ? meta : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsStale(string gameKey)
    {
        var meta = GetCached(gameKey);
        return meta == null || DateTime.UtcNow - meta.FetchedAtUtc > CacheTtl;
    }

    /// <summary>拉取官方背景列表：图片立即下载，视频切到该图时再下载。</summary>
    public static async Task<BackgroundSet?> RefreshAsync(string gameKey, bool force = false)
    {
        SemaphoreSlim gate;
        lock (RefreshGates)
        {
            if (!RefreshGates.TryGetValue(gameKey, out var found))
            {
                gate = new SemaphoreSlim(1, 1);
                RefreshGates[gameKey] = gate;
            }
            else
            {
                gate = found;
            }
        }

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await RefreshCoreAsync(gameKey, force).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<BackgroundSet?> RefreshCoreAsync(string gameKey, bool force)
    {
        if (!BizMap.TryGetValue(gameKey, out var biz)) return GetCached(gameKey);
        if (!force && !IsStale(gameKey)) return GetCached(gameKey);

        try
        {
            using var doc = await GetJsonAsync($"{ApiBase}?launcher_id={LauncherId}&language=zh-cn").ConfigureAwait(false);
            if (doc == null) return GetCached(gameKey);
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("game_info_list", out var list))
                return GetCached(gameKey);

            JsonElement? hit = null;
            foreach (var item in list.EnumerateArray())
            {
                if (item.TryGetProperty("game", out var game) &&
                    game.TryGetProperty("biz", out var bizNode) &&
                    bizNode.GetString() == biz)
                {
                    hit = item;
                    break;
                }
            }
            if (hit == null) return GetCached(gameKey);

            var info = hit.Value;
            if (!info.TryGetProperty("backgrounds", out var bgs) || bgs.GetArrayLength() == 0)
                return GetCached(gameKey);

            var old = GetCached(gameKey);
            var result = new BackgroundSet { Key = gameKey, FetchedAtUtc = DateTime.UtcNow };

            foreach (var bg in bgs.EnumerateArray())
            {
                var id = bg.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? "" : "";
                var imageUrl = NestedUrl(bg, "background");
                var videoUrl = NestedUrl(bg, "video");
                var type = bg.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

                var item = new BackgroundItem { Id = id, Type = type, VideoUrl = videoUrl };
                if (imageUrl != null) item.ImageFile = await DownloadAsync(imageUrl).ConfigureAwait(false);

                var oldItem = old?.Items.FirstOrDefault(x => x.Id == id);
                if (oldItem?.VideoFile != null && File.Exists(Path.Combine(CacheDir, oldItem.VideoFile)))
                    item.VideoFile = oldItem.VideoFile;

                if (item.ImageFile != null || item.VideoFile != null) result.Items.Add(item);
            }

            if (result.Items.Count == 0) return GetCached(gameKey);

            await SaveMetaAsync(result).ConfigureAwait(false);
            CleanupUnused();
            return result;
        }
        catch
        {
            return GetCached(gameKey);
        }
    }

    /// <summary>选中动态壁纸时按需下载 .webm。</summary>
    public static async Task<string?> EnsureVideoAsync(string gameKey, string backgroundId)
    {
        var jobKey = gameKey + "|" + backgroundId;
        Task<string?> job;
        lock (VideoJobs)
        {
            if (!VideoJobs.TryGetValue(jobKey, out var found) || found == null)
            {
                job = EnsureVideoCoreAsync(gameKey, backgroundId);
                VideoJobs[jobKey] = job;
            }
            else
            {
                job = found;
            }
        }

        try
        {
            return await job.ConfigureAwait(false);
        }
        finally
        {
            lock (VideoJobs)
            {
                if (VideoJobs.TryGetValue(jobKey, out var current) && ReferenceEquals(current, job))
                    VideoJobs.Remove(jobKey);
            }
        }
    }

    private static async Task<string?> EnsureVideoCoreAsync(string gameKey, string backgroundId)
    {
        var meta = GetCached(gameKey);
        var item = meta?.Items.FirstOrDefault(x => x.Id == backgroundId);
        if (item == null) return null;
        if (item.VideoPath != null) return item.VideoPath;
        if (string.IsNullOrWhiteSpace(item.VideoUrl)) return null;

        var name = await DownloadAsync(item.VideoUrl).ConfigureAwait(false);
        if (name == null) return null;
        item.VideoFile = name;
        await SaveMetaAsync(meta!).ConfigureAwait(false);
        BackgroundReady?.Invoke(gameKey);
        return item.VideoPath;
    }

    /// <summary>启动页打开时后台把四款游戏的壁纸与动态壁纸全部拉好，切换游戏不再等待。</summary>
    public static async Task PrefetchAllAsync()
    {
        if (Interlocked.Exchange(ref _prefetchStarted, 1) == 1) return;
        try
        {
            foreach (var key in BizMap.Keys.ToList())
            {
                var meta = await RefreshAsync(key).ConfigureAwait(false);
                if (meta == null || meta.Items.Count == 0) continue;
                BackgroundReady?.Invoke(key);

                using var gate = new SemaphoreSlim(2);
                var jobs = new List<Task>();
                foreach (var item in meta.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.VideoUrl) || item.VideoPath != null) continue;
                    var id = item.Id;
                    jobs.Add(Task.Run(async () =>
                    {
                        await gate.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            await EnsureVideoAsync(key, id).ConfigureAwait(false);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }));
                }
                if (jobs.Count > 0) await Task.WhenAll(jobs).ConfigureAwait(false);
            }
            CleanupUnused();
        }
        catch
        {
            // 后台预加载失败不阻断启动页
        }
        finally
        {
            Interlocked.Exchange(ref _prefetchStarted, 0);
        }
    }

    public static string? FallbackImage(string gameKey)
    {
        if (!FallbackMap.TryGetValue(gameKey, out var name)) return null;
        var path = Path.Combine(AssetDir, name);
        return File.Exists(path) ? path : null;
    }

    // ---------- 启动页记录：每款游戏上次看到第几张 ----------

    private sealed class LaunchUiState
    {
        public Dictionary<string, int> Index { get; set; } = new();
        public Dictionary<string, bool> Flags { get; set; } = new();
    }

    public static int LoadIndex(string gameKey)
    {
        try
        {
            if (!File.Exists(UiStateFile)) return 0;
            var state = JsonSerializer.Deserialize<LaunchUiState>(File.ReadAllText(UiStateFile));
            return state != null && state.Index.TryGetValue(gameKey, out var index) ? Math.Max(0, index) : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static void SaveIndex(string gameKey, int index)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.CacheDirectory);
            LaunchUiState state;
            if (File.Exists(UiStateFile))
                state = JsonSerializer.Deserialize<LaunchUiState>(File.ReadAllText(UiStateFile)) ?? new();
            else
                state = new();
            state.Index[gameKey] = Math.Max(0, index);
            File.WriteAllText(UiStateFile, JsonSerializer.Serialize(state));
        }
        catch
        {
            // 只影响下次默认选中
        }
    }

    /// <summary>启动页显示开关（预约卡片 / 米哈云游按钮）。默认开启。</summary>
    public static bool LoadUiFlag(string name, bool fallback)
    {
        try
        {
            if (!File.Exists(UiStateFile)) return fallback;
            var state = JsonSerializer.Deserialize<LaunchUiState>(File.ReadAllText(UiStateFile));
            if (state?.Flags != null && state.Flags.TryGetValue(name, out var value)) return value;
        }
        catch
        {
            // 读取失败时使用默认值
        }
        return fallback;
    }

    public static void SaveUiFlag(string name, bool value)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.CacheDirectory);
            LaunchUiState state;
            if (File.Exists(UiStateFile))
                state = JsonSerializer.Deserialize<LaunchUiState>(File.ReadAllText(UiStateFile)) ?? new();
            else
                state = new();
            state.Flags ??= new Dictionary<string, bool>();
            state.Flags[name] = value;
            File.WriteAllText(UiStateFile, JsonSerializer.Serialize(state));
        }
        catch
        {
            // 仅影响下次启动页的显示状态
        }
    }

    // ---------- 网络与缓存 ----------

    private static async Task<JsonDocument?> GetJsonAsync(string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonDocument.Parse(raw);
        }
        catch
        {
            return null;
        }
    }

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private static string? NestedUrl(JsonElement bg, string node)
    {
        if (!bg.TryGetProperty(node, out var n) || n.ValueKind != JsonValueKind.Object) return null;
        if (!n.TryGetProperty("url", out var u)) return null;
        var s = u.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static async Task<string?> DownloadAsync(string url)
    {
        try
        {
            var name = Path.GetFileName(new Uri(url).AbsolutePath);
            if (string.IsNullOrWhiteSpace(name)) return null;
            Directory.CreateDirectory(CacheDir);
            var path = Path.Combine(CacheDir, name);
            if (File.Exists(path) && new FileInfo(path).Length > 0) return name;

            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            if (bytes.Length == 0) return null;

            var temp = path + "." + Guid.NewGuid().ToString("N") + ".part";
            await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
            try
            {
                File.Move(temp, path, true);
            }
            catch
            {
                try { File.Delete(temp); } catch { }
                if (!File.Exists(path)) return null;
            }
            return name;
        }
        catch
        {
            return null;
        }
    }

    private static async Task SaveMetaAsync(BackgroundSet meta)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(MetaFile(meta.Key), json).ConfigureAwait(false);
        }
        catch
        {
            // 缓存写失败不阻断显示
        }
    }

    private static void CleanupUnused()
    {
        try
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in BizMap.Keys)
            {
                var meta = GetCached(key);
                if (meta == null) continue;
                foreach (var item in meta.Items)
                {
                    if (item.ImageFile != null) keep.Add(item.ImageFile);
                    if (item.VideoFile != null) keep.Add(item.VideoFile);
                }
            }

            foreach (var file in Directory.GetFiles(CacheDir))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("official_", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (keep.Contains(name)) continue;
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromMinutes(10)) continue;
                File.Delete(file);
            }
        }
        catch
        {
            // 忽略
        }
    }
}
