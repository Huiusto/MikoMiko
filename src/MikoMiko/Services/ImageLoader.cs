using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace MikoMiko.Services;

/// <summary>图片下载与内存缓存，避免在 UI 线程同步加载网络图。</summary>
public static class ImageLoader
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, BitmapImage> Cache = new();
    private static readonly Dictionary<string, Task<BitmapImage?>> Pending = new();
    private static readonly object Gate = new();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
        // 不携带固定 Referer：追番站的图床会拒绝来自米游社域名的外链请求。
        return client;
    }

    public static Task<BitmapImage?> LoadAsync(string url, string? referer = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return Task.FromResult<BitmapImage?>(null);

        var key = string.IsNullOrWhiteSpace(referer) ? url : $"{url}|{referer}";
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached)) return Task.FromResult<BitmapImage?>(cached);
            if (Pending.TryGetValue(key, out var running)) return running;
            var task = DownloadAsync(url, key, referer);
            Pending[key] = task;
            return task;
        }
    }

    private static async Task<BitmapImage?> DownloadAsync(string url, string key, string? referer)
    {
        try
        {
            byte[]? bytes = null;
            if (!string.IsNullOrWhiteSpace(referer))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Referrer = new Uri(referer);
                using var response = await Http.SendAsync(request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }

            if (bytes is null)
                bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);

            var image = Create(bytes, 900);
            if (image is not null)
            {
                lock (Gate)
                {
                    Cache[key] = image;
                }
            }

            return image;
        }
        catch
        {
            return null;
        }
        finally
        {
            lock (Gate)
            {
                Pending.Remove(key);
            }
        }
    }

    private static BitmapImage? Create(byte[] bytes, int decodeWidth)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.StreamSource = stream;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
