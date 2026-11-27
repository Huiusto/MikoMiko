using MikoMiko.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace MikoMiko.Services;

/// <summary>GitHub Releases 应用内更新检查与安装包下载。</summary>
public static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    public sealed record UpdateInfo(
        Version Version,
        string Tag,
        string Title,
        string Notes,
        string PageUrl,
        string AssetName,
        string AssetUrl);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"MikoMiko/{AppInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await Http.GetStringAsync("https://api.github.com/repos/Huiusto/MikoMiko/releases/latest", ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;

            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty;
            var parsed = Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out var version);
            if (!parsed || version is null) return null;
            if (version <= Version.Parse(AppInfo.Version)) return null;

            var page = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? string.Empty : string.Empty;
            var title = root.TryGetProperty("name", out var name) ? name.GetString() ?? tag : tag;
            var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty;

            string assetName = string.Empty;
            string assetUrl = string.Empty;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var candidate = asset.TryGetProperty("name", out var assetNameElement)
                        ? assetNameElement.GetString() ?? string.Empty
                        : string.Empty;
                    if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!candidate.Contains("Setup", StringComparison.OrdinalIgnoreCase)) continue;
                    assetName = candidate;
                    assetUrl = asset.TryGetProperty("browser_download_url", out var url)
                        ? url.GetString() ?? string.Empty
                        : string.Empty;
                    break;
                }
            }

            return new UpdateInfo(version, tag, title, notes, page, assetName, assetUrl);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>下载安装包到临时目录，返回本地路径。</summary>
    public static async Task<string?> DownloadAsync(UpdateInfo info, IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(info.AssetUrl)) return null;
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "MikoMikoUpdate");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, info.AssetName);
            using var response = await Http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(file);
            var buffer = new byte[81920];
            long readTotal = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer, ct);
                if (read <= 0) break;
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                readTotal += read;
                if (total > 0) progress?.Report(Math.Clamp(readTotal / (double)total, 0, 1));
            }

            progress?.Report(1);
            return file;
        }
        catch
        {
            return null;
        }
    }

    public static bool RunInstaller(string path)
    {
        try
        {
            // 静默安装到当前运行目录，安装完成后由 NSIS 重新启动新版本。
            // /D= 必须是最后一个参数，且不能加引号（NSIS 会读取原始命令行）。
            var target = AppInfo.InstallDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Arguments = $"/S /D={target}",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>手动或启动时检查；发现新版本时弹出应用内更新窗口。</summary>
    public static async Task<bool> PromptIfAvailableAsync(Window? owner, AppSettings settings, bool manual,
        CancellationToken ct = default)
    {
        var info = await CheckAsync(ct);
        if (info is null)
        {
            return false;
        }

        // 用户点过「取消」则忽略该版本；手动检查时不忽略，方便重新查看
        if (!manual && string.Equals(settings.IgnoredUpdateVersion, info.Tag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var window = new Views.UpdateWindow(info, settings) { Owner = owner };
        window.ShowDialog();
        return true;
    }
}
