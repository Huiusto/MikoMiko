using MikoMiko.Models;
using System.IO;
using System.Net.Http;

namespace MikoMiko.Services;

/// <summary>图片 / 视频下载：统一保存到设置里的「下载位置」。</summary>
public static class DownloadService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }

    public static string ResolveDirectory(AppSettings settings)
    {
        var path = string.IsNullOrWhiteSpace(settings.DownloadPath)
            ? AppInfo.DefaultDownloadDirectory
            : settings.DownloadPath;
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>下载到指定目录；文件名自动清理非法字符并避免覆盖已有文件。</summary>
    public static async Task<string> DownloadAsync(string url, string directory, string baseName,
        string extension, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("下载地址为空", nameof(url));
        Directory.CreateDirectory(directory);

        var safe = Sanitize(baseName);
        if (safe.Length > 80) safe = safe[..80];
        if (string.IsNullOrWhiteSpace(safe)) safe = "MikoMiko";
        var ext = extension.StartsWith(".") ? extension : "." + extension;
        var path = Path.Combine(directory, safe + ext);
        var index = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{safe} ({index++}){ext}");
        }

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = File.Create(path);
        await input.CopyToAsync(output, ct);
        return path;
    }

    public static string GuessImageExtension(string url)
    {
        var clean = (url ?? string.Empty).Split('?')[0].ToLowerInvariant();
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" })
        {
            if (clean.EndsWith(ext)) return ext == ".jpeg" ? ".jpg" : ext;
        }
        return ".jpg";
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = (value ?? string.Empty).Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim().Trim('.');
    }
}
