using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikoMiko.Services;

/// <summary>追番/影视模块里的一个数据源。</summary>
public sealed class AnimeProvider
{
    public string Name { get; set; } = string.Empty;
    public string Home { get; set; } = string.Empty;
    public string Kind { get; set; } = "动漫";
    public string Region { get; set; } = "其他";
    public string Parser { get; set; } = "none";
    public string Search { get; set; } = string.Empty;
}

/// <summary>模块 manifest.json 的结构。</summary>
public sealed class AnimeModuleManifest
{
    public string Name { get; set; } = "追番/影视";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public List<AnimeProvider> Providers { get; set; } = new();
}

/// <summary>
/// 追番/影视模块的安装、更新和卸载。模块本体是独立 zip，
/// 安装后在应用数据目录 modules\anime 下解压，卸载时整目录删除。
/// </summary>
public sealed class AnimeModuleService
{
    public const string AssetName = "MikoMiko-AnimeModule.zip";

    private static readonly HttpClient Http = CreateHttpClient();
    private readonly string _root;
    private readonly string _manifestPath;

    public AnimeModuleService(string? root = null)
    {
        _root = root ?? Path.Combine(SettingsService.DataDirectory, "modules", "anime");
        _manifestPath = Path.Combine(_root, "manifest.json");
    }

    public string Root => _root;
    public bool IsInstalled => File.Exists(_manifestPath);
    public AnimeModuleManifest? Manifest { get; private set; }
    public IReadOnlyList<AnimeProvider> Providers
        => Manifest?.Providers ?? (IReadOnlyList<AnimeProvider>)Array.Empty<AnimeProvider>();

    /// <summary>读取模块配置；模块未安装或配置损坏时返回 false。</summary>
    public bool TryLoad()
    {
        if (!File.Exists(_manifestPath))
        {
            Manifest = null;
            return false;
        }

        try
        {
            var json = File.ReadAllText(_manifestPath);
            var manifest = JsonSerializer.Deserialize<AnimeModuleManifest>(json, JsonOptions);
            if (manifest is null || manifest.Providers.Count == 0)
            {
                Manifest = null;
                return false;
            }
            Manifest = manifest;
            return true;
        }
        catch
        {
            Manifest = null;
            return false;
        }
    }

    /// <summary>
    /// 下载并安装模块。优先从当前版本的 GitHub Release 获取独立模块包，
    /// 失败时回退到安装目录里随包附带的 anime-module.zip，保证离线也能安装。
    /// </summary>
    public async Task<bool> InstallAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(SettingsService.DataDirectory);
        var tempZip = Path.Combine(Path.GetTempPath(), "MikoMiko-AnimeModule-" + Guid.NewGuid().ToString("N") + ".zip");
        var staging = _root + ".new";
        var fromCache = false;

        try
        {
            progress?.Report(0.02);
            var downloaded = await TryDownloadReleaseAssetAsync(tempZip, progress, cancellationToken)
                .ConfigureAwait(false);

            if (!downloaded)
            {
                var bundled = Path.Combine(AppContext.BaseDirectory, "anime-module.zip");
                if (!File.Exists(bundled))
                    bundled = Path.Combine(AppContext.BaseDirectory, "Assets", "Anime", "anime-module.zip");
                if (!File.Exists(bundled))
                    throw new FileNotFoundException("没有找到可用的追番/影视模块包。", bundled);

                fromCache = true;
                progress?.Report(0.72);
                await CopyFileAsync(bundled, tempZip, cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(0.82);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);
            ExtractSafely(tempZip, staging);

            var stagedManifest = Path.Combine(staging, "manifest.json");
            if (!File.Exists(stagedManifest))
                throw new InvalidDataException("模块包缺少 manifest.json。");

            // 先校验 manifest，再替换现有目录，避免装上一份坏模块。
            var probe = JsonSerializer.Deserialize<AnimeModuleManifest>(File.ReadAllText(stagedManifest), JsonOptions);
            if (probe is null || probe.Providers.Count == 0)
                throw new InvalidDataException("模块包里的数据源配置无效。");

            progress?.Report(0.92);
            if (Directory.Exists(_root))
            {
                try
                {
                    Directory.Delete(_root, recursive: true);
                }
                catch
                {
                    // 旧模块文件被占用时，至少先移除 manifest，让界面回到未安装状态。
                    TryDeleteFile(_manifestPath);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_root)!);
            Directory.Move(staging, _root);
            Manifest = probe;
            progress?.Report(1.0);
            _ = fromCache;
            return true;
        }
        finally
        {
            if (File.Exists(tempZip)) TryDeleteFile(tempZip);
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: true); } catch { }
            }
        }
    }

    /// <summary>卸载模块：删除独立模块目录，不影响主程序数据。</summary>
    public void Uninstall()
    {
        Manifest = null;
        if (!Directory.Exists(_root)) return;

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // 被占用时先删 manifest，让页面立即回到下载引导状态。
            TryDeleteFile(_manifestPath);
        }
    }

    private async Task<bool> TryDownloadReleaseAssetAsync(string target, IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var releaseApi = $"https://api.github.com/repos/Huiusto/MikoMiko/releases/tags/v{AppInfo.Version}";
            using var releaseRequest = new HttpRequestMessage(HttpMethod.Get, releaseApi);
            releaseRequest.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var releaseResponse = await Http.SendAsync(releaseRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!releaseResponse.IsSuccessStatusCode) return false;

            await using var releaseStream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(releaseStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var assetUrl = document.RootElement.GetProperty("assets").EnumerateArray()
                .Where(a => string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                .Select(a => a.GetProperty("url").GetString())
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(assetUrl)) return false;

            using var assetRequest = new HttpRequestMessage(HttpMethod.Get, assetUrl);
            assetRequest.Headers.Accept.ParseAdd("application/octet-stream");
            using var assetResponse = await Http.SendAsync(assetRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!assetResponse.IsSuccessStatusCode) return false;

            var total = assetResponse.Content.Headers.ContentLength ?? 0;
            await using var source = await assetResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = File.Create(target);
            var buffer = new byte[81920];
            long read = 0;
            while (true)
            {
                var count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (count <= 0) break;
                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                read += count;
                if (total > 0)
                    progress?.Report(0.05 + Math.Clamp(read * 1.0 / total, 0, 0.72));
            }

            return new FileInfo(target).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ExtractSafely(string zipPath, string destinationRoot)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var rootFull = Path.GetFullPath(destinationRoot);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) && string.IsNullOrWhiteSpace(entry.FullName)) continue;
            var target = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("模块包包含非法路径。");
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static async Task CopyFileAsync(string source, string target, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(source);
        await using var output = File.Create(target);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 卸载失败会在下次启动时重试。
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"MikoMiko/{AppInfo.Version} (Windows)");
        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
