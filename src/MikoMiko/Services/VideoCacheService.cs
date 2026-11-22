using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace MikoMiko.Services;

/// <summary>
/// 视频直链缓存：先下载到本地文件，再交给 MediaElement 播放。
/// 米游社的 CDN 直链带签名与长查询串，WPF Media Foundation 直接读 HTTPS 源经常打不开，
/// 落盘后本地播放最稳，也顺带解决重复观看的流量问题。
/// </summary>
public sealed class VideoCacheService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, Task<string?>> InFlight = new();

    public static string CacheDir
    {
        get
        {
            var dir = SettingsService.VideoCacheDirectory;
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Referrer = new Uri("https://www.miyoushe.com/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://www.miyoushe.com");
        return client;
    }

    private static string CachePath(string url)
        => Path.Combine(CacheDir, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24] + ".mp4");

    /// <summary>已缓存的本地文件；没有则返回 null。</summary>
    public static string? TryGetCached(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var target = CachePath(url);
        return File.Exists(target) && new FileInfo(target).Length > 1024 * 512 ? target : null;
    }

    /// <summary>把远程视频落到本地缓存，返回本地路径；失败返回 null。</summary>
    public static async Task<string?> GetLocalAsync(string url, IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24];
        var target = Path.Combine(CacheDir, key + ".mp4");
        if (File.Exists(target) && new FileInfo(target).Length > 1024 * 512)
        {
            progress?.Report(1);
            return target;
        }

        // 同一个地址并发请求时共用一次下载
        Task<string?>? running = null;
        lock (InFlight)
        {
            running = InFlight.TryGetValue(key, out var existing) ? existing : null;
        }

        if (running is not null) return await running;

        var task = DownloadAsync(url, target, progress, ct);
        lock (InFlight) InFlight[key] = task;
        try
        {
            return await task;
        }
        finally
        {
            lock (InFlight) InFlight.Remove(key);
        }
    }

    private static async Task<string?> DownloadAsync(string url, string target, IProgress<double>? progress,
        CancellationToken ct)
    {
        var temp = target + ".part";
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;

            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long read = 0;
                while (true)
                {
                    var count = await input.ReadAsync(buffer, ct);
                    if (count <= 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                    read += count;
                    if (total > 0) progress?.Report(Math.Clamp(read / (double)total, 0, 0.99));
                }
            }

            if (new FileInfo(temp).Length < 1024 * 512)
            {
                try { File.Delete(temp); } catch { }
                return null;
            }

            if (File.Exists(target))
            {
                try { File.Delete(target); } catch { }
            }

            File.Move(temp, target);
            progress?.Report(1);
            return target;
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            return null;
        }
    }

    /// <summary>当前缓存占用（字节）。</summary>
    public static long GetCacheSize()
    {
        try
        {
            return Directory.EnumerateFiles(CacheDir, "*.mp4")
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    public static void Clear()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(CacheDir))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch
        {
            // 忽略
        }
    }
}
