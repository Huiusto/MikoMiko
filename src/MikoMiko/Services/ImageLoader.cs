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

    public static Task<BitmapImage?> LoadAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Task.FromResult<BitmapImage?>(null);

        lock (Gate)
        {
            if (Cache.TryGetValue(url, out var cached)) return Task.FromResult<BitmapImage?>(cached);
            if (Pending.TryGetValue(url, out var running)) return running;
            var task = DownloadAsync(url);
            Pending[url] = task;
            return task;
        }
    }

    private static async Task<BitmapImage?> DownloadAsync(string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            var image = Create(bytes, 900);
            if (image is not null)
            {
                lock (Gate)
                {
                    Cache[url] = image;
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
                Pending.Remove(url);
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
