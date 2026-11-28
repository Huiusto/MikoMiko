using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MikoMiko.Localization;
using MikoMiko.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace MikoMiko.Views;

/// <summary>
/// 启动页（仿 MikoNest）：整页由内嵌 HTML 渲染，官方启动器的壁纸与 .webm 动态壁纸
/// 交给 WebView2 / Chromium 播放；左侧靠近边缘浮现官方壁纸切换条。
/// </summary>
public partial class LaunchView : UserControl
{
    private const string VirtualHost = "mikomiko.local";

    private static readonly Dictionary<string, string> IconFiles = new()
    {
        ["genshin"] = "icon_genshin.png",
        ["starrail"] = "icon_starrail.png",
        ["zzz"] = "icon_zzz.png",
        ["bh3"] = "icon_bh3.png",
    };

    private static readonly Dictionary<string, (string Source, string Target)> FallbackFiles = new()
    {
        ["genshin"] = ("Assets/Launch/bg_genshin.jpg", "fallback_genshin.jpg"),
        ["starrail"] = ("Assets/Launch/bg_StarRail.jpg", "fallback_starrail.jpg"),
        ["zzz"] = ("Assets/Launch/bg_zzz.jpg", "fallback_zzz.jpg"),
        ["bh3"] = ("Assets/Launch/bg_bh3.jpg", "fallback_bh3.jpg"),
    };

    private static readonly Dictionary<string, string> Websites = new()
    {
        ["genshin"] = "https://ys.mihoyo.com/",
        ["starrail"] = "https://sr.mihoyo.com/",
        ["zzz"] = "https://zzz.mihoyo.com/",
        ["bh3"] = "https://bh3.mihoyo.com/",
    };

    private string _selectedKey = "genshin";
    private List<LauncherBackgroundService.BackgroundItem> _bgItems = new();
    private readonly Dictionary<string, GameContentService.Content> _feedCache = new();
    private readonly HashSet<string> _feedLoading = new();
    private int _bgIndex;
    private bool _paused;
    private bool _preparing;
    private bool _showReserve = true;
    private bool _showMhyy = true;
    private bool _showFeed = true;
    private bool _webReady;
    private readonly HashSet<string> _runningGames = new();
    private List<string> _gameOrder = new();
    private Dictionary<string, GameUpdateService.UpdateState> _updateStates = new();
    private readonly Dictionary<string, InstallJob> _installJobs = new();

    /// <summary>启动页安装任务：配置页关闭后继续在后台下载，按钮原位显示进度。</summary>
    private sealed class InstallJob
    {
        public string GameKey { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public CancellationTokenSource Cts { get; } = new();
        public GameInstallService.PauseController Pause { get; } = new();
        public string Stage { get; set; } = "正在准备安装…";
        public long Done { get; set; }
        public long Total { get; set; }
        public long DownloadDone { get; set; }
        public long DownloadTotal { get; set; }
        public long InstallDone { get; set; }
        public long InstallTotal { get; set; }
        public double Speed { get; set; }
        public double DownloadSpeed { get; set; }
        public double InstallSpeed { get; set; }
        public long LastSpeedTick { get; set; }
        public long LastDownloadBytes { get; set; }
        public long LastInstallBytes { get; set; }
        public bool Cancelling { get; set; }
        public bool OwnsTarget { get; set; }

        public double Percent => Total > 0 ? Math.Clamp(Done * 100.0 / Total, 0, 100) : 0;
    }

    /// <summary>启动页需要在内置浏览器打开的网址（米哈云游等），由 Shell 接管。</summary>
    public event Action<string>? OpenUrlRequested;

    /// <summary>WebView2 是独立子窗口，会被上层页面自带的 WebView2 抢 z 序；覆盖层打开时先隐藏。</summary>
    public void SetWebHidden(bool hidden)
    {
        Web.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        // 隐藏 WebView2 后，上传弹层等覆盖页下面会露出这个底色；深色会像一块黑屏。
        Root.Background = new System.Windows.Media.SolidColorBrush(
            hidden ? System.Windows.Media.Color.FromRgb(0xF3, 0xF7, 0xFB) : System.Windows.Media.Color.FromRgb(0x0B, 0x0D, 0x12));
    }

    /// <summary>预约卡片：参数为游戏键（planet / yinyuan 等）和官网兜底链接。</summary>
    public event Action<string, string?>? ReservationRequested;

    private static readonly Dictionary<string, string> UninstallBiz = new()
    {
        ["genshin"] = "hk4e_cn",
        ["starrail"] = "hkrpg_cn",
        ["zzz"] = "nap_cn",
        ["bh3"] = "bh3_cn",
    };

    public LaunchView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibleChanged;
        LauncherBackgroundService.BackgroundReady += OnBackgroundReady;
    }

    /// <summary>页面被切回来后 WebView2 / 动态壁纸可能停在黑帧，重新推送并恢复播放。</summary>
    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue || !_webReady) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            PushState();
            _ = Web.CoreWebView2?.ExecuteScriptAsync(
                "try { if (window.mikoRepaintBackground) window.mikoRepaintBackground(); } catch(e) {}");
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        MikoNestBridge.Reload();
        _gameOrder = LauncherBackgroundService.LoadGameOrder();
        var settings = App.SettingsService.Current;
        _showReserve = settings.ShowReserve;
        _showMhyy = settings.ShowMhyy;
        _showFeed = settings.ShowFeed;
        if (_webReady)
        {
            PushState();
            return;
        }
        await InitWebAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_webReady) return;
        _ = Web.CoreWebView2?.ExecuteScriptAsync(
            "try { const v=document.getElementById('bgVideo'); if(v) v.pause(); } catch(e) {}");
    }

    private async Task InitWebAsync()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.DataDirectory);
            PrepareFiles();

            var env = await CoreWebView2Environment.CreateAsync(
                null, Path.Combine(SettingsService.CacheDirectory, "webview2-launch"));
            await Web.EnsureCoreWebView2Async(env);

            var settings = Web.CoreWebView2.Settings;
            settings.AreDevToolsEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.IsZoomControlEnabled = false;
            Web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 11, 13, 18);

            Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHost, SettingsService.DataDirectory, CoreWebView2HostResourceAccessKind.Allow);
            Web.CoreWebView2.WebMessageReceived += OnWebMessage;
            Web.CoreWebView2.NavigationCompleted += async (_, __) =>
            {
                _webReady = true;
                SelectFirstGame();
                LoadBackgrounds(_selectedKey);
                PushState();
                await EnsureCurrentVideoAsync();
                _ = LauncherBackgroundService.PrefetchAllAsync();
                _ = RefreshUpdateStatesAsync();
            };
            Web.CoreWebView2.Navigate($"https://{VirtualHost}/launch/launch.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show("启动页初始化失败：" + ex.Message, "MikoMiko",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>安装程序结束时调用：把启动页 HTML、壁纸与图标提前写入用户数据目录。</summary>
    internal static void PrepareLaunchAssetsForInstall()
    {
        Directory.CreateDirectory(SettingsService.DataDirectory);
        PrepareFiles();
    }

    /// <summary>把 HTML、兜底壁纸与游戏图标复制到 WebView2 虚拟目录。</summary>
    private static void PrepareFiles()
    {
        var launchDir = Path.Combine(SettingsService.DataDirectory, "launch");
        var assetDir = Path.Combine(launchDir, "assets");
        var iconDir = Path.Combine(assetDir, "icons");
        Directory.CreateDirectory(iconDir);

        CopyResource("Assets/Launch/launch.html", Path.Combine(launchDir, "launch.html"));

        foreach (var (source, target) in FallbackFiles.Values)
            CopyResource(source, Path.Combine(assetDir, target));

        foreach (var file in IconFiles.Values)
            CopyResource("Assets/Launch/icons/" + file, Path.Combine(iconDir, file));
    }

    private static void CopyResource(string resourcePath, string targetPath)
    {
        try
        {
            using var stream = Application.GetResourceStream(
                new Uri("pack://application:,,,/" + resourcePath))?.Stream;
            if (stream == null) return;
            using var fs = File.Create(targetPath);
            stream.CopyTo(fs);
        }
        catch
        {
            // 资源缺失时页面仍有兜底背景
        }
    }

    // ---------- 状态推送 ----------

    private List<NestGame> OrderedGames()
    {
        var result = new List<NestGame>();
        var remaining = MikoNestBridge.Games.ToList();
        var preferred = _gameOrder.Count > 0
            ? _gameOrder
            : MikoNestBridge.Settings.LaunchGameOrder ?? new List<string>();
        foreach (var key in preferred)
        {
            var game = remaining.FirstOrDefault(g => g.Key == key);
            if (game == null) continue;
            result.Add(game);
            remaining.Remove(game);
        }
        result.AddRange(remaining);
        return result;
    }

    private void SelectFirstGame()
    {
        var first = OrderedGames().FirstOrDefault();
        if (first != null) _selectedKey = first.Key;
    }

    private void LoadBackgrounds(string key)
    {
        var cached = LauncherBackgroundService.GetCached(key);
        _bgItems = cached?.Items ?? new List<LauncherBackgroundService.BackgroundItem>();
        _bgIndex = Math.Clamp(LauncherBackgroundService.LoadIndex(key), 0,
            Math.Max(0, _bgItems.Count - 1));
        _paused = false;
        _preparing = false;
        _ = LoadFeedAsync(key);
        _ = RefreshOfficialAsync(key);
    }

    /// <summary>官方启动器「相关内容」：活动 / 公告 / 资讯，直接取 HoYoPlay 数据。</summary>
    private async Task LoadFeedAsync(string key)
    {
        if (_feedCache.ContainsKey(key) || !_feedLoading.Add(key)) return;
        PushState();

        var content = await GameContentService.LoadAsync(key).ConfigureAwait(false);
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            _feedLoading.Remove(key);
            if (content.Posts.Count > 0) _feedCache[key] = content;
            if (key == _selectedKey) PushState();
        }));
    }

    private async Task RefreshOfficialAsync(string key)
    {
        var meta = await LauncherBackgroundService.RefreshAsync(key);
        if (meta == null || key != _selectedKey) return;

        var oldIndex = _bgIndex;
        _bgItems = meta.Items;
        _bgIndex = Math.Clamp(LauncherBackgroundService.LoadIndex(key), 0,
            Math.Max(0, _bgItems.Count - 1));
        if (_bgItems.Count == 0) _bgIndex = oldIndex;
        PushState();
    }

    private void OnBackgroundReady(string key)
    {
        if (!_webReady || key != _selectedKey) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var cached = LauncherBackgroundService.GetCached(key);
            if (cached == null) return;
            _bgItems = cached.Items;
            _bgIndex = Math.Clamp(_bgIndex, 0, Math.Max(0, _bgItems.Count - 1));
            PushState();
        }));
    }

    private void PushState()
    {
        if (!_webReady) return;

        // 以持久化设置为准，避免页面初始化先拿到旧字段导致开关失效。
        var saved = App.SettingsService.Current;
        _showReserve = saved.ShowReserve;
        _showMhyy = saved.ShowMhyy;
        _showFeed = saved.ShowFeed;

        var games = OrderedGames().Select(game => new
        {
            key = game.Key,
            name = game.Name,
            located = IsLocated(game.Key),
            running = _runningGames.Contains(game.Key),
            preDownload = _updateStates.TryGetValue(game.Key, out var update) && update.HasPreDownload,
            preDownloadVersion = _updateStates.TryGetValue(game.Key, out var version) ? version.PreVersion : "",
            icon = IconFiles.TryGetValue(game.Key, out var iconFile)
                ? ToUrl(Path.Combine(SettingsService.DataDirectory, "launch", "assets", "icons", iconFile))
                : null,
        }).ToList();

        var officialItems = new List<object>();
        foreach (var item in _bgItems)
        {
            if (item.ImagePath == null) continue;
            officialItems.Add(new
            {
                id = item.Id,
                image = ToUrl(item.ImagePath),
                video = ToUrl(item.VideoPath),
            });
        }

        // 官方壁纸同步完成前先用内置同款壁纸占位，避免启动页整块变黑；
        // 一旦官方列表到达，会立即替换为官方壁纸。
        var items = new List<object>(officialItems);
        var usingFallback = false;
        if (officialItems.Count == 0 && FallbackFiles.TryGetValue(_selectedKey, out var fallback))
        {
            var path = Path.Combine(SettingsService.DataDirectory, "launch", "assets", fallback.Target);
            var url = ToUrl(path);
            if (url != null)
            {
                items.Add(new { id = "fallback", image = url, video = (string?)null });
                usingFallback = true;
            }
        }

        _bgIndex = Math.Clamp(_bgIndex, 0, Math.Max(0, items.Count - 1));
        var shownIndex = usingFallback ? 0 : _bgIndex;

        var selectedGame = MikoNestBridge.Games.FirstOrDefault(g => g.Key == _selectedKey);
        var selectedDir = MikoNestBridge.GetGameDir(_selectedKey);
        var selectedExe = MikoNestBridge.FindGameExe(_selectedKey);
        var gameInfo = new
        {
            name = selectedGame?.Name ?? _selectedKey,
            exe = selectedGame is null ? "" : string.Join(" / ", selectedGame.ExeNames),
            dir = selectedDir ?? "",
            free = FormatFreeSpace(selectedDir),
            located = selectedExe != null,
        };

        var preloadImages = new List<string>();
        foreach (var gameKey in new[] { "genshin", "starrail", "zzz", "bh3" })
        {
            var cached = LauncherBackgroundService.GetCached(gameKey);
            if (cached == null) continue;
            foreach (var item in cached.Items)
            {
                if (item.ImagePath == null) continue;
                var url = ToUrl(item.ImagePath);
                if (url != null && !preloadImages.Contains(url)) preloadImages.Add(url);
            }
        }

        var state = new
        {
            games,
            selected = _selectedKey,
            feed = BuildFeed(_selectedKey),
            backgrounds = items,
            officialBackgrounds = officialItems,
            preloadImages,
            index = shownIndex,
            officialIndex = _bgIndex,
            paused = _paused,
            preparing = _preparing,
            showReserve = _showReserve,
            showMhyy = _showMhyy,
            showFeed = _showFeed,
            accent = "#4EA4DD",
            hint = BuildHint(),
            customBackground = false,
            gameInfo,
            install = InstallStateFor(_selectedKey),
            lang = Loc.Instance.Language,
            texts = Services.LaunchWebTexts.BuildMap(),
        };

        _ = Web.CoreWebView2.ExecuteScriptAsync(
            $"window.mikoInit({JsonSerializer.Serialize(state)});");
    }

    private object BuildFeed(string key)
    {
        var game = MikoNestBridge.Games.FirstOrDefault(g => g.Key == key);
        var cached = _feedCache.TryGetValue(key, out var content) ? content : null;
        var items = cached?.Posts.Select(post => new
        {
            kind = post.Kind,
            title = post.Title,
            date = post.Date,
            url = post.Url,
            cover = post.Cover,
        }).ToArray() ?? Array.Empty<object>();

        return new
        {
            title = $"{game?.Name ?? cached?.GameName ?? "当前游戏"} · 相关内容",
            loading = _feedLoading.Contains(key),
            failed = cached == null && !_feedLoading.Contains(key),
            icon = IconFiles.TryGetValue(key, out var iconFile)
                ? $"https://{VirtualHost}/launch/assets/icons/{iconFile}"
                : "",
            items,
            tabs = new[]
            {
                new { key = "activity", label = "活动" },
                new { key = "announce", label = "公告" },
                new { key = "info", label = "资讯" },
            },
        };
    }

    private void PushPatch(object patch)
    {
        if (!_webReady) return;
        _ = Web.CoreWebView2.ExecuteScriptAsync(
            $"window.mikoUpdate({JsonSerializer.Serialize(patch)});");
    }

    private object? InstallStateFor(string key)
    {
        if (!_installJobs.TryGetValue(key, out var job)) return null;
        return new
        {
            active = true,
            paused = job.Pause.IsPaused,
            cancelling = job.Cancelling,
            percent = Math.Round(job.Percent, 1),
            stage = job.Stage,
            done = FormatSize(job.Done),
            total = FormatSize(job.Total),
            downloadDone = FormatSize(job.DownloadDone),
            downloadTotal = FormatSize(job.DownloadTotal),
            downloadDoneBytes = job.DownloadDone,
            downloadTotalBytes = job.DownloadTotal,
            installDone = FormatSize(job.InstallDone),
            installTotal = FormatSize(job.InstallTotal),
            installDoneBytes = job.InstallDone,
            installTotalBytes = job.InstallTotal,
            speed = job.Speed > 0 ? $"{job.Speed / 1024.0 / 1024.0:0.0} MB/s" : string.Empty,
            speedBytes = job.Speed,
            downloadSpeed = job.DownloadSpeed > 0 ? $"{job.DownloadSpeed / 1024.0 / 1024.0:0.00} MB/s" : string.Empty,
            installSpeed = job.InstallSpeed > 0 ? $"{job.InstallSpeed / 1024.0 / 1024.0:0.00} MB/s" : string.Empty,
        };
    }

    private void PushInstallState(string key)
    {
        if (key != _selectedKey) return;
        PushPatch(new
        {
            install = InstallStateFor(key),
            hint = BuildHint(),
        });
    }

    private async Task RunInstallAsync(string key, string name, string target, string language)
    {
        if (_installJobs.ContainsKey(key)) return;

        var job = new InstallJob { GameKey = key, Target = target };
        try
        {
            // 安装前目标目录为空时，整个目录都是这次安装新建的，取消可以直接清掉。
            job.OwnsTarget = !Directory.Exists(target) || !Directory.EnumerateFileSystemEntries(target).Any();
        }
        catch
        {
            job.OwnsTarget = false;
        }
        _installJobs[key] = job;
        PushInstallState(key);
        Toast($"已开始安装「{name}」，可在按钮上暂停");

        var progress = new Progress<GameInstallService.Progress>(p =>
        {
            var total = p.Total > 0 ? p.Total : p.InstallTotal;
            var done = p.Done > 0 ? p.Done : p.InstallDone;
            var stage = string.IsNullOrWhiteSpace(p.Stage) ? job.Stage : p.Stage;
            if (job.Pause.IsPaused) stage = "已暂停，正在完成当前分块";
            else if (job.Cancelling) stage = "正在取消安装并清理…";

            job.Stage = stage;
            job.Done = done;
            job.Total = total;
            job.DownloadDone = p.Done;
            job.DownloadTotal = p.Total;
            job.InstallDone = p.InstallDone;
            job.InstallTotal = p.InstallTotal;
            job.Speed = p.BytesPerSecond;
            var nowTick = Stopwatch.GetTimestamp();
            if (job.LastSpeedTick == 0)
            {
                job.LastSpeedTick = nowTick;
                job.LastDownloadBytes = p.Done;
                job.LastInstallBytes = p.InstallDone;
            }
            else
            {
                var seconds = (nowTick - job.LastSpeedTick) / (double)Stopwatch.Frequency;
                if (seconds >= 0.45)
                {
                    var downloadDelta = Math.Max(0, p.Done - job.LastDownloadBytes);
                    var installDelta = Math.Max(0, p.InstallDone - job.LastInstallBytes);
                    if (downloadDelta > 0) job.DownloadSpeed = downloadDelta / seconds;
                    if (installDelta > 0) job.InstallSpeed = installDelta / seconds;
                    job.LastSpeedTick = nowTick;
                    job.LastDownloadBytes = p.Done;
                    job.LastInstallBytes = p.InstallDone;
                }
            }
            PushInstallState(key);
        });

        try
        {
            var installed = await GameInstallService.InstallAsync(
                key, target, language, progress, job.Cts.Token, job.Pause);
            MikoNestBridge.SetLocalGamePath(key, installed);
            SaveInstalledByApp(key, installed);
            _updateStates.Remove(key);
            Toast($"「{name}」安装完成，可以开始游戏了");
        }
        catch (OperationCanceledException)
        {
            CleanupCancelledInstall(job);
            Toast("已取消安装，本次写入的文件已清理");
        }
        catch (Exception ex)
        {
            Toast($"「{name}」安装失败：{ex.Message}");
        }
        finally
        {
            _installJobs.Remove(key);
            if (key == _selectedKey) PushState();
        }
    }

    private void ToggleInstallPause(string key)
    {
        if (!_installJobs.TryGetValue(key, out var job) || job.Cancelling) return;
        if (job.Pause.IsPaused)
        {
            job.Pause.Resume();
            job.Stage = "继续安装…";
            Toast("已继续安装");
        }
        else
        {
            job.Pause.Pause();
            job.Stage = "已暂停，正在完成当前分块";
            Toast("安装已暂停");
        }
        PushInstallState(key);
    }

    private void CancelInstall(string key)
    {
        if (!_installJobs.TryGetValue(key, out var job) || job.Cancelling) return;
        job.Cancelling = true;
        job.Stage = "正在取消安装并清理…";
        job.Cts.Cancel();
        job.Pause.Resume();
        PushInstallState(key);
        Toast("正在取消安装并清理本次写入的文件");
    }

    /// <summary>取消安装后收拾目录：本次新建的目录整体删除，原来就有内容的只清空目录壳。</summary>
    private static void CleanupCancelledInstall(InstallJob job)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job.Target) || !Directory.Exists(job.Target)) return;
            if (job.OwnsTarget)
            {
                Directory.Delete(job.Target, recursive: true);
                return;
            }

            foreach (var dir in Directory.GetDirectories(job.Target, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir, recursive: false);
                }
                catch
                {
                    // 被占用的目录留给后续手动清理
                }
            }
        }
        catch
        {
            // 清理失败不覆盖取消结果
        }
    }

    private static string InstallRegistryFile =>
        Path.Combine(SettingsService.DataDirectory, "launch-installed-games.json");

    private static Dictionary<string, string> LoadInstalledByApp()
    {
        try
        {
            if (!File.Exists(InstallRegistryFile)) return new();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                       File.ReadAllText(InstallRegistryFile)) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static void SaveInstalledByApp(string key, string path)
    {
        try
        {
            var map = LoadInstalledByApp();
            map[key] = path;
            Directory.CreateDirectory(SettingsService.DataDirectory);
            File.WriteAllText(InstallRegistryFile, JsonSerializer.Serialize(map));
        }
        catch
        {
            // 记录失败不影响游戏启动
        }
    }

    private static void RemoveInstalledByApp(string key)
    {
        try
        {
            var map = LoadInstalledByApp();
            if (!map.Remove(key)) return;
            File.WriteAllText(InstallRegistryFile, JsonSerializer.Serialize(map));
        }
        catch
        {
            // 清理失败不影响卸载流程
        }
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void Toast(string text)
    {
        if (!_webReady) return;
        _ = Web.CoreWebView2.ExecuteScriptAsync($"window.mikoToast({JsonSerializer.Serialize(text)});");
    }

    private string BuildHint()
    {
        if (_installJobs.TryGetValue(_selectedKey, out var job))
        {
            // 安装中只保留主按钮悬停卡里的进度，页面提示区不再显示“正在写入 xxx”。
            return job.Cancelling ? "正在取消安装并清理已写入的文件…" : string.Empty;
        }
        if (_runningGames.Contains(_selectedKey)) return "游戏运行中，退出游戏后按钮会自动恢复。";
        var dir = MikoNestBridge.GetGameDir(_selectedKey);
        if (dir == null) return "未找到本地游戏，点「获取游戏」即可立即安装。";
        if (MikoNestBridge.FindGameExe(_selectedKey) == null)
            return "已找到游戏目录，但没有找到主程序：" + dir + "；点「定位游戏」重新选择包含游戏程序的文件夹。";
        return "已定位：" + dir;
    }

    private bool IsLocated(string key) => MikoNestBridge.FindGameExe(key) != null;

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var i = 0;
        while (value >= 1024 && i < units.Length - 1)
        {
            value /= 1024;
            i++;
        }
        return $"{value:0.##} {units[i]}";
    }

    private static string FormatFreeSpace(string? dir)
    {
        try
        {
            var root = dir is null ? null : Path.GetPathRoot(dir);
            if (string.IsNullOrWhiteSpace(root)) return "—";
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return "—";
            return (drive.AvailableFreeSpace / 1024d / 1024d / 1024d).ToString("0.0") + " GB";
        }
        catch
        {
            return "—";
        }
    }

    private string? ToUrl(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath)) return null;
        var full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        var root = Path.GetFullPath(SettingsService.DataDirectory).Replace('\\', '/').TrimEnd('/');
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return $"https://{VirtualHost}" + full[root.Length..];
        return null;
    }

    // ---------- 页面消息 ----------

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string type;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            type = doc.RootElement.GetProperty("type").GetString() ?? "";
        }
        catch
        {
            return;
        }

        try
        {
            switch (type)
            {
                case "ready":
                    PushState();
                    break;

                case "selectGame":
                {
                    var key = ReadString(e.WebMessageAsJson, "key");
                    if (string.IsNullOrEmpty(key) || key == _selectedKey) break;
                    if (MikoNestBridge.Games.All(g => g.Key != key)) break;
                    _selectedKey = key;
                    LoadBackgrounds(key);
                    PushState();
                    await EnsureCurrentVideoAsync();
                    break;
                }

                case "bg":
                    await SelectBackgroundAsync((int)ReadNumber(e.WebMessageAsJson, "index"));
                    break;

                case "officialBg":
                    await SelectBackgroundAsync((int)ReadNumber(e.WebMessageAsJson, "index"));
                    break;

                case "pauseToggle":
                    _paused = !_paused;
                    PushPatch(new { paused = _paused });
                    break;

                case "uiFlag":
                {
                    var name = ReadString(e.WebMessageAsJson, "name");
                    var value = ReadNumber(e.WebMessageAsJson, "value") != 0;
                    if (name == "showReserve") _showReserve = value;
                    else if (name == "showMhyy") _showMhyy = value;
                    else if (name == "showFeed") _showFeed = value;
                    else break;

                    var settings = App.SettingsService.Current;
                    settings.ShowReserve = _showReserve;
                    settings.ShowMhyy = _showMhyy;
                    settings.ShowFeed = _showFeed;
                    PushPatch(new { showReserve = _showReserve, showMhyy = _showMhyy, showFeed = _showFeed });
                    break;
                }

                case "saveBg":
                    SaveCurrentBackground();
                    break;

                case "start":
                    await StartGameAsync();
                    break;

                case "install":
                    if (_installJobs.ContainsKey(_selectedKey)) ToggleInstallPause(_selectedKey);
                    else InstallGame();
                    break;

                case "installPauseToggle":
                    ToggleInstallPause(_selectedKey);
                    break;

                case "cancelInstall":
                    CancelInstall(_selectedKey);
                    break;

                case "predownload":
                {
                    var biz = UninstallBiz.TryGetValue(_selectedKey, out var value) ? value : null;
                    if (biz != null)
                    {
                        OpenOfficialLauncher(biz);
                        Toast("已打开米哈游官方启动器，预下载进度可在官方启动器查看");
                    }
                    break;
                }

                case "moveGame":
                {
                    var key = ReadString(e.WebMessageAsJson, "key");
                    var delta = (int)ReadNumber(e.WebMessageAsJson, "delta");
                    if (string.IsNullOrEmpty(key) || delta == 0) break;
                    var ordered = OrderedGames().Select(g => g.Key).ToList();
                    var index = ordered.IndexOf(key);
                    if (index < 0) break;
                    var target = Math.Clamp(index + delta, 0, ordered.Count - 1);
                    if (target == index) break;
                    ordered.RemoveAt(index);
                    ordered.Insert(target, key);
                    _gameOrder = ordered;
                    LauncherBackgroundService.SaveGameOrder(ordered);
                    PushState();
                    break;
                }

                case "locate":
                    PickGame();
                    break;

                case "openDir":
                    OpenGameDir();
                    break;

                case "uninstall":
                    UninstallGame();
                    break;

                case "openFeed":
                {
                    var url = ReadString(e.WebMessageAsJson, "url");
                    if (!string.IsNullOrWhiteSpace(url)) OpenUrlRequested?.Invoke(url);
                    break;
                }

                case "mhyy":
                    OpenUrlRequested?.Invoke("https://mhyy.mihoyo.com/");
                    break;

                case "reserve":
                {
                    var game = ReadString(e.WebMessageAsJson, "game");
                    var url = ReadString(e.WebMessageAsJson, "url");
                    if (!string.IsNullOrEmpty(game))
                    {
                        ReservationRequested?.Invoke(game, url);
                    }
                    else if (!string.IsNullOrEmpty(url))
                    {
                        OpenUrlRequested?.Invoke(url);
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Toast("操作失败：" + ex.Message);
        }
    }

    private static string? ReadString(string json, string property)
        => JsonDocument.Parse(json).RootElement.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double ReadNumber(string json, string property)
        => JsonDocument.Parse(json).RootElement.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;

    private static bool ReadBool(string json, string property)
        => JsonDocument.Parse(json).RootElement.TryGetProperty(property, out var value) &&
           (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) &&
           value.GetBoolean();

    // ---------- 壁纸 ----------

    private async Task SelectBackgroundAsync(int index)
    {
        if (index < 0 || index >= _bgItems.Count) return;

        _bgIndex = index;
        _paused = false;
        LauncherBackgroundService.SaveIndex(_selectedKey, index);

        var item = _bgItems[index];
        if (item.VideoPath == null && !string.IsNullOrWhiteSpace(item.VideoUrl))
        {
            _preparing = true;
            PushState();
            await LauncherBackgroundService.EnsureVideoAsync(_selectedKey, item.Id);
            _preparing = false;
            var cached = LauncherBackgroundService.GetCached(_selectedKey);
            if (cached != null) _bgItems = cached.Items;
        }
        PushState();
    }

    private async Task EnsureCurrentVideoAsync()
    {
        var item = _bgIndex >= 0 && _bgIndex < _bgItems.Count ? _bgItems[_bgIndex] : null;
        if (item == null || item.VideoPath != null || string.IsNullOrWhiteSpace(item.VideoUrl)) return;
        _preparing = true;
        PushState();
        await LauncherBackgroundService.EnsureVideoAsync(_selectedKey, item.Id);
        _preparing = false;
        var cached = LauncherBackgroundService.GetCached(_selectedKey);
        if (cached != null) _bgItems = cached.Items;
        PushState();
    }

    private void SaveCurrentBackground()
    {
        var item = _bgIndex >= 0 && _bgIndex < _bgItems.Count ? _bgItems[_bgIndex] : null;
        var source = item?.VideoPath ?? item?.ImagePath ??
                     LauncherBackgroundService.FallbackImage(_selectedKey);
        if (source == null || !File.Exists(source))
        {
            Toast("当前壁纸还没有下载完成，无法保存");
            return;
        }

        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "MikoMiko 壁纸");
            Directory.CreateDirectory(dir);
            var gameName = MikoNestBridge.Games.FirstOrDefault(g => g.Key == _selectedKey)?.Name ?? _selectedKey;
            File.Copy(source, Path.Combine(dir, gameName + "_" + Path.GetFileName(source)), true);
            Toast("壁纸已保存到 图片\\MikoMiko 壁纸");
        }
        catch (Exception ex)
        {
            Toast("保存失败：" + ex.Message);
        }
    }

    // ---------- 定位与启动 ----------

    private void PickGame()
    {
        var game = MikoNestBridge.Games.FirstOrDefault(g => g.Key == _selectedKey);
        if (game == null) return;

        var dialog = new OpenFileDialog
        {
            Title = $"定位「{game.Name}」游戏程序",
            Filter = "游戏程序|*.exe|所有文件|*.*",
            CheckFileExists = true,
        };
        var dir = MikoNestBridge.GetGameDir(_selectedKey);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            dialog.InitialDirectory = dir;

        var exe = MikoNestBridge.FindGameExe(_selectedKey);
        if (exe != null) dialog.FileName = Path.GetFileName(exe);

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var selected = Path.GetDirectoryName(dialog.FileName);
        if (string.IsNullOrEmpty(selected)) return;

        MikoNestBridge.SetLocalGamePath(_selectedKey, selected);
        PushState();
        Toast($"已保存「{game.Name}」的安装位置");
    }

    private void OpenGameDir()
    {
        var dir = MikoNestBridge.GetGameDir(_selectedKey);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            Toast("尚未定位到安装目录，请先点「重新定位游戏」");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{dir}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Toast("打开目录失败：" + ex.Message);
        }
    }

    /// <summary>MikoMiko 直接安装的游戏自行删除并清理；官方启动器安装的游戏仍走官方卸载入口。</summary>
    private void UninstallGame()
    {
        if (_installJobs.ContainsKey(_selectedKey))
        {
            CancelInstall(_selectedKey);
            return;
        }

        var game = MikoNestBridge.Games.FirstOrDefault(g => g.Key == _selectedKey);
        var dir = MikoNestBridge.GetGameDir(_selectedKey);
        if (game == null || string.IsNullOrWhiteSpace(dir))
        {
            Toast("尚未定位到游戏目录，无法卸载");
            return;
        }

        var installedByApp = LoadInstalledByApp();
        var appPath = installedByApp.GetValueOrDefault(_selectedKey);
        if (!string.IsNullOrWhiteSpace(appPath) && Directory.Exists(appPath) && SamePath(appPath, dir))
        {
            var answer = MessageBox.Show(
                $"将删除由 MikoMiko 安装的「{game.Name}」全部文件：\n{dir}\n\n此操作不可撤销，确定卸载并清理吗？",
                "卸载并清理", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
            try
            {
                if (_runningGames.Contains(_selectedKey))
                {
                    Toast("游戏仍在运行，请先退出游戏再卸载");
                    return;
                }
                var full = Path.GetFullPath(appPath).TrimEnd(Path.DirectorySeparatorChar);
                var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(root) || string.Equals(root, full, StringComparison.OrdinalIgnoreCase))
                {
                    Toast("安装目录异常，已停止删除");
                    return;
                }
                Directory.Delete(full, true);
                RemoveInstalledByApp(_selectedKey);
                MikoNestBridge.SetLocalGamePath(_selectedKey, string.Empty);
                _updateStates.Remove(_selectedKey);
                PushState();
                Toast($"「{game.Name}」已卸载，安装文件已清理");
            }
            catch (Exception ex)
            {
                Toast("卸载清理失败：" + ex.Message);
            }
            return;
        }

        var officialAnswer = MessageBox.Show(
            $"将通过米哈游启动器卸载「{game.Name}」。\n这会关闭游戏并删除游戏文件，确定继续吗？",
            "卸载游戏", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (officialAnswer != MessageBoxResult.Yes) return;

        var launcher = FindLauncher(dir);
        if (launcher is null)
        {
            MessageBox.Show("没有找到米哈游启动器，请打开米哈游启动器手动卸载。", "MikoMiko",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = launcher,
                Arguments = "--uninstall_game=" + UninstallBiz[_selectedKey],
                WorkingDirectory = Path.GetDirectoryName(launcher) ?? "",
                UseShellExecute = true,
            });
            Toast("已打开米哈游官方卸载流程");
        }
        catch (Exception ex)
        {
            Toast("打开卸载程序失败：" + ex.Message);
        }
    }

    private static string? FindLauncher(string dir)
    {
        try
        {
            var current = new DirectoryInfo(dir);
            for (var i = 0; i < 4 && current != null; i++, current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "launcher.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch
        {
            // 目录不可访问时走失败提示
        }
        return null;
    }

    private void InstallGame()
    {
        var key = _selectedKey;
        var game = MikoNestBridge.Games.FirstOrDefault(g => g.Key == key);
        if (game == null) return;
        if (_installJobs.ContainsKey(key))
        {
            ToggleInstallPause(key);
            return;
        }

        var dialog = new InstallGameDialog(key, game.Name)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() != true) return;
        if (string.IsNullOrWhiteSpace(dialog.TargetDirectory)) return;

        // 配置页已经关闭；实际安装转到启动页按钮的进度状态里继续执行。
        _ = RunInstallAsync(key, game.Name, dialog.TargetDirectory!, dialog.InstallLanguage);
    }

    private void OpenOfficialLauncher(string biz)
    {
        var launcher = FindLauncher(MikoNestBridge.GetGameDir(_selectedKey) ?? "");
        if (launcher is null)
        {
            var common = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "miHoYo", "miHoYo Launcher", "launcher.exe");
            if (File.Exists(common)) launcher = common;
        }
        if (launcher is null)
        {
            OpenUrl("https://mhyy.mihoyo.com/");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = launcher,
                WorkingDirectory = Path.GetDirectoryName(launcher) ?? "",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Toast("打开官方启动器失败：" + ex.Message);
        }
    }

    private async Task RefreshUpdateStatesAsync()
    {
        try
        {
            _updateStates = await GameUpdateService.GetStatesAsync();
            if (_webReady) PushState();
        }
        catch
        {
            // 官方接口不可用时保持无预下载角标
        }
    }

    private async Task StartGameAsync()
    {
        var exe = MikoNestBridge.FindGameExe(_selectedKey);
        if (exe == null)
        {
            InstallGame();
            return;
        }
        if (_runningGames.Contains(_selectedKey)) return;

        var key = _selectedKey;
        _runningGames.Add(key);
        PushState();

        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _runningGames.Remove(key);
            PushState();
            Toast("启动失败：" + ex.Message);
            return;
        }

        await Task.Delay(3000);
        if (Window.GetWindow(this) is { } owner) owner.WindowState = WindowState.Minimized;

        _ = TrackGameExitAsync(key, Path.GetFileNameWithoutExtension(exe));
    }

    private async Task TrackGameExitAsync(string key, string processName)
    {
        var appeared = false;
        var missingChecks = 0;

        for (var i = 0; i < 30 && !appeared; i++)
        {
            await Task.Delay(500);
            if (Process.GetProcessesByName(processName).Length > 0) appeared = true;
        }

        while (true)
        {
            await Task.Delay(2000);
            if (Process.GetProcessesByName(processName).Length > 0)
            {
                missingChecks = 0;
                continue;
            }
            missingChecks++;
            if (missingChecks < 3) continue;
            _runningGames.Remove(key);
            PushState();
            return;
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打不开浏览器时不影响本页
        }
    }
}
