using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MikoMiko.Services;

/// <summary>官方游戏包/分支/清单接口，专供软件内安装使用，与启动页壁纸服务解耦。</summary>
public static class GameInstallApi
{
    private const string LauncherId = "jGHBHlcOq1";
    private const string PackageApi = "https://hyp-api.mihoyo.com/hyp/hyp-connect/api/getGamePackages";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly Dictionary<string, string> BizMap = new()
    {
        ["genshin"] = "hk4e_cn",
        ["starrail"] = "hkrpg_cn",
        ["zzz"] = "nap_cn",
        ["bh3"] = "bh3_cn",
    };

    public sealed class ChunkManifestInfo
    {
        public string MatchingField = "";
        public string ManifestId = "";
        public string ManifestChecksum = "";
        public bool ManifestCompressed = true;
        public string ManifestUrlPrefix = "";
        public string ManifestUrlSuffix = "";
        public bool ChunkCompressed = true;
        public string ChunkUrlPrefix = "";
        public string ChunkUrlSuffix = "";
        public long CompressedSize;
        public long UncompressedSize;
        public long FileCount;
    }

    public sealed class ChunkBuildInfo
    {
        public string Version = "";
        public string GameBiz = "";
        public string BuildId = "";
        public string Tag = "";
        public List<ChunkManifestInfo> Manifests = new();
        public long TotalUncompressedBytes => Manifests.Sum(x => x.UncompressedSize);
    }

    public sealed record InstallPart(string Url, long Size, long DecompressedSize);

    public sealed class InstallPackage
    {
        public string Version { get; init; } = "";
        public string GameBiz { get; init; } = "";
        public string ResListUrl { get; init; } = "";
        public List<InstallPart> Parts { get; init; } = new();
        public List<InstallPart> AudioParts { get; init; } = new();
        public long TotalDownloadBytes => Parts.Sum(x => x.Size) + AudioParts.Sum(x => x.Size);
        public long TotalDiskBytes => Parts.Sum(x => x.DecompressedSize) + AudioParts.Sum(x => x.DecompressedSize);
    }

    /// <summary>官方 Sophon chunk 构建信息：当前正式版本 + 需要下载的清单。</summary>
    public static async Task<ChunkBuildInfo?> GetChunkBuildAsync(string gameKey, string language)
    {
        var biz = BizMap.TryGetValue(gameKey, out var b) ? b : null;
        if (string.IsNullOrEmpty(biz)) return null;
        var gameId = GameIdOf(biz);
        if (string.IsNullOrEmpty(gameId)) return null;

        try
        {
            var branchUrl = "https://hyp-api.mihoyo.com/hyp/hyp-connect/api/getGameBranches" +
                            $"?launcher_id={LauncherId}&language={language}&game_ids[]={gameId}";
            using var branchDoc = await GetJsonAsync(branchUrl).ConfigureAwait(false);
            if (branchDoc == null) return null;
            if (!branchDoc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("game_branches", out var branches) ||
                branches.ValueKind != JsonValueKind.Array || branches.GetArrayLength() == 0)
                return null;

            var package = branches[0].TryGetProperty("main", out var main) && main.ValueKind == JsonValueKind.Object
                ? main : default;
            if (package.ValueKind != JsonValueKind.Object) return null;
            var packageId = Str(package, "package_id");
            var password = Str(package, "password");
            var branch = Str(package, "branch");
            var tag = Str(package, "tag");
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(branch))
                return null;

            var buildUrl = "https://downloader-api.mihoyo.com/downloader/sophon_chunk/api/getBuild" +
                           $"?branch={Uri.EscapeDataString(branch)}&package_id={Uri.EscapeDataString(packageId)}" +
                           $"&password={Uri.EscapeDataString(password)}";
            using var buildDoc = await GetJsonAsync(buildUrl).ConfigureAwait(false);
            if (buildDoc == null) return null;
            if (!buildDoc.RootElement.TryGetProperty("data", out var build) ||
                !build.TryGetProperty("manifests", out var manifests) ||
                manifests.ValueKind != JsonValueKind.Array)
                return null;

            var result = new ChunkBuildInfo
            {
                Version = Str(build, "tag") is { Length: > 0 } t ? t : tag,
                Tag = tag,
                GameBiz = biz,
                BuildId = Str(build, "build_id"),
            };

            foreach (var m in manifests.EnumerateArray())
            {
                var field = Str(m, "matching_field");
                if (string.IsNullOrEmpty(field)) continue;
                if (field.Contains('-') && !field.Equals(language, StringComparison.OrdinalIgnoreCase))
                    continue;

                var manifest = m.TryGetProperty("manifest", out var mf) && mf.ValueKind == JsonValueKind.Object
                    ? mf : default;
                var manifestDownload = m.TryGetProperty("manifest_download", out var md) && md.ValueKind == JsonValueKind.Object
                    ? md : default;
                var chunkDownload = m.TryGetProperty("chunk_download", out var cd) && cd.ValueKind == JsonValueKind.Object
                    ? cd : default;
                var stats = m.TryGetProperty("stats", out var st) && st.ValueKind == JsonValueKind.Object
                    ? st : default;
                var id = Str(manifest, "id");
                if (string.IsNullOrEmpty(id)) continue;

                result.Manifests.Add(new ChunkManifestInfo
                {
                    MatchingField = field,
                    ManifestId = id,
                    ManifestChecksum = Str(manifest, "checksum"),
                    ManifestCompressed = ToLong(manifestDownload, "compression") != 0,
                    ManifestUrlPrefix = Str(manifestDownload, "url_prefix"),
                    ManifestUrlSuffix = Str(manifestDownload, "url_suffix"),
                    ChunkCompressed = ToLong(chunkDownload, "compression") != 0,
                    ChunkUrlPrefix = Str(chunkDownload, "url_prefix"),
                    ChunkUrlSuffix = Str(chunkDownload, "url_suffix"),
                    CompressedSize = ToLong(stats, "compressed_size"),
                    UncompressedSize = ToLong(stats, "uncompressed_size"),
                    FileCount = ToLong(stats, "file_count"),
                });
            }

            return result.Manifests.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>安装包信息（用于「获取游戏」弹窗显示所需空间）</summary>
    public static async Task<(string Version, long SizeBytes)> GetLatestPackageAsync(string gameKey)
    {
        if (!BizMap.TryGetValue(gameKey, out var biz)) return ("", 0);
        try
        {
            using var doc = await GetJsonAsync(
                $"{PackageApi}?launcher_id={LauncherId}&language=zh-cn&game_ids[]={GameIdOf(biz)}")
                .ConfigureAwait(false);
            if (doc == null) return ("", 0);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data)) return ("", 0);
            if (!data.TryGetProperty("game_packages", out var pkgs)) return ("", 0);
            foreach (var pkg in pkgs.EnumerateArray())
            {
                if (!pkg.TryGetProperty("game", out var g)) continue;
                if (!g.TryGetProperty("biz", out var b) || b.GetString() != biz) continue;
                if (!pkg.TryGetProperty("main", out var main)) continue;
                if (!main.TryGetProperty("major", out var major)) continue;
                var ver = major.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                var p = await GetInstallPackageAsync(gameKey, "zh-cn").ConfigureAwait(false);
                return (ver, p?.TotalDiskBytes ?? ReadMajorSize(major, main));
            }
        }
        catch
        {
            // 接口异常时返回空信息
        }
        return ("", 0);
    }

    public static async Task<InstallPackage?> GetInstallPackageAsync(string gameKey, string language)
    {
        if (!BizMap.TryGetValue(gameKey, out var biz)) return null;
        using var doc = await GetJsonAsync(
            $"{PackageApi}?launcher_id={LauncherId}&language=zh-cn&game_ids[]={GameIdOf(biz)}")
            .ConfigureAwait(false);
        if (doc == null) return null;
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("game_packages", out var pkgs))
            return null;
        foreach (var pkg in pkgs.EnumerateArray())
        {
            if (!pkg.TryGetProperty("game", out var g) || !g.TryGetProperty("biz", out var b) || b.GetString() != biz)
                continue;
            if (!pkg.TryGetProperty("main", out var main) || !main.TryGetProperty("major", out var major))
                continue;

            var result = new InstallPackage
            {
                Version = major.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "",
                GameBiz = biz,
                ResListUrl = Str(major, "res_list_url").TrimEnd('/'),
            };

            if (major.TryGetProperty("game_pkgs", out var parts))
            {
                foreach (var p in parts.EnumerateArray())
                {
                    var url = Str(p, "url");
                    if (string.IsNullOrEmpty(url)) continue;
                    result.Parts.Add(new InstallPart(url, ToLong(p, "size"), ToLong(p, "decompressed_size")));
                }
            }

            if (major.TryGetProperty("audio_pkgs", out var audios))
            {
                foreach (var a in audios.EnumerateArray())
                {
                    var lang = Str(a, "language");
                    if (!string.IsNullOrEmpty(lang) && lang != language) continue;
                    var url = Str(a, "url");
                    if (string.IsNullOrEmpty(url)) continue;
                    result.AudioParts.Add(new InstallPart(url, ToLong(a, "size"), ToLong(a, "decompressed_size")));
                    break;
                }
            }

            return result.Parts.Count > 0 ? result : null;
        }
        return null;
    }

    private static long ReadMajorSize(JsonElement major, JsonElement main)
    {
        if (major.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number)
            return sz.GetInt64();
        long total = 0;
        if (major.TryGetProperty("game_pkgs", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in arr.EnumerateArray())
            {
                foreach (var key in new[] { "decompressed_size", "size" })
                {
                    if (!part.TryGetProperty(key, out var s)) continue;
                    if (s.ValueKind == JsonValueKind.Number) { total += s.GetInt64(); break; }
                    if (s.ValueKind == JsonValueKind.String && long.TryParse(s.GetString(), out var n))
                    {
                        total += n;
                        break;
                    }
                }
            }
        }
        return total;
    }

    private static long ToLong(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt64();
        return v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var n) ? n : 0;
    }

    private static string Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string GameIdOf(string biz) => biz switch
    {
        "hk4e_cn" => "1Z8W5NHUQb",
        "hkrpg_cn" => "64kMb5iAWu",
        "nap_cn" => "x6znKlJ0xK",
        "bh3_cn" => "osvnlOc0S8",
        _ => "",
    };

    private static async Task<JsonDocument?> GetJsonAsync(string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "MikoMiko/0.1.47");
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonDocument.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
        catch
        {
            return null;
        }
    }
}
