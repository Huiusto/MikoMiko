using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
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
    private int _bgIndex;
    private bool _paused;
    private bool _preparing;
    private bool _webReady;
    private bool _showReserve = true;
    private bool _showMhyy = true;
    private readonly HashSet<string> _runningGames = new();

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
        LauncherBackgroundService.BackgroundReady += OnBackgroundReady;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        MikoNestBridge.Reload();
        _showReserve = LauncherBackgroundService.LoadUiFlag("showReserve", true);
        _showMhyy = LauncherBackgroundService.LoadUiFlag("showMhyy", true);
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
            };
            Web.CoreWebView2.Navigate($"https://{VirtualHost}/launch/launch.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show("启动页初始化失败：" + ex.Message, "MikoMiko",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
        foreach (var key in MikoNestBridge.Settings.LaunchGameOrder ?? new List<string>())
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
        _ = RefreshOfficialAsync(key);
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

        var games = OrderedGames().Select(game => new
        {
            key = game.Key,
            name = game.Name,
            located = IsLocated(game.Key),
            running = _runningGames.Contains(game.Key),
            icon = IconFiles.TryGetValue(game.Key, out var iconFile)
                ? ToUrl(Path.Combine(SettingsService.DataDirectory, "launch", "assets", "icons", iconFile))
                : null,
        }).ToList();

        var items = new List<object>();
        foreach (var item in _bgItems)
        {
            if (item.ImagePath == null) continue;
            items.Add(new
            {
                id = item.Id,
                image = ToUrl(item.ImagePath),
                video = ToUrl(item.VideoPath),
            });
        }
        if (items.Count == 0)
        {
            var fallback = LauncherBackgroundService.FallbackImage(_selectedKey);
            if (fallback != null)
                items.Add(new { id = "fallback", image = ToUrl(fallback), video = (string?)null });
        }

        if (_bgIndex >= items.Count) _bgIndex = Math.Max(0, items.Count - 1);

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

        var state = new
        {
            games,
            selected = _selectedKey,
            backgrounds = items,
            index = _bgIndex,
            paused = _paused,
            preparing = _preparing,
            accent = "#4EA4DD",
            hint = BuildHint(),
            showReserve = _showReserve,
            showMhyy = _showMhyy,
            gameInfo,
        };

        _ = Web.CoreWebView2.ExecuteScriptAsync(
            $"window.mikoInit({JsonSerializer.Serialize(state)});");
    }

    private void PushPatch(object patch)
    {
        if (!_webReady) return;
        _ = Web.CoreWebView2.ExecuteScriptAsync(
            $"window.mikoUpdate({JsonSerializer.Serialize(patch)});");
    }

    private void Toast(string text)
    {
        if (!_webReady) return;
        _ = Web.CoreWebView2.ExecuteScriptAsync($"window.mikoToast({JsonSerializer.Serialize(text)});");
    }

    private string BuildHint()
    {
        if (_runningGames.Contains(_selectedKey)) return "游戏运行中，退出游戏后按钮会自动恢复。";
        var dir = MikoNestBridge.GetGameDir(_selectedKey);
        if (dir == null) return "未找到本地游戏，点击「获取游戏」打开官网下载；安装后点「定位游戏」选择目录即可直接启动。";
        if (MikoNestBridge.FindGameExe(_selectedKey) == null)
            return "已找到游戏目录，但没有找到主程序：" + dir + "；点「定位游戏」重新选择包含游戏程序的文件夹。";
        return "已定位：" + dir;
    }

    private bool IsLocated(string key) => MikoNestBridge.FindGameExe(key) != null;

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

                case "pauseToggle":
                    _paused = !_paused;
                    PushPatch(new { paused = _paused });
                    break;

                case "saveBg":
                    SaveCurrentBackground();
                    break;

                case "start":
                    await StartGameAsync();
                    break;

                case "install":
                    OpenUrl(Websites[_selectedKey]);
                    Toast("已打开官网下载页；安装完成后回到本页点「定位游戏」");
                    break;

                case "locate":
                    PickGame();
                    break;

                case "openDir":
                    OpenGameDir();
                    break;

                case "uninstall":
                    UninstallGame();
                    break;

                case "uiFlag":
                {
                    var name = ReadString(e.WebMessageAsJson, "name");
                    var value = ReadBool(e.WebMessageAsJson, "value");
                    if (name == "showReserve") _showReserve = value;
                    else if (name == "showMhyy") _showMhyy = value;
                    else break;
                    LauncherBackgroundService.SaveUiFlag(name!, value);
                    PushState();
                    break;
                }

                case "mhyy":
                    OpenUrl("https://mhyy.mihoyo.com/");
                    break;

                case "reserve":
                {
                    var url = ReadString(e.WebMessageAsJson, "url");
                    if (!string.IsNullOrEmpty(url)) OpenUrl(url);
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

    /// <summary>只调用米哈游启动器的官方卸载入口，不自行删除任何游戏文件。</summary>
    private void UninstallGame()
    {
        var game = MikoNestBridge.Games.FirstOrDefault(g => g.Key == _selectedKey);
        var dir = MikoNestBridge.GetGameDir(_selectedKey);
        if (game == null || string.IsNullOrWhiteSpace(dir))
        {
            Toast("尚未定位到游戏目录，无法卸载");
            return;
        }

        var answer = MessageBox.Show(
            $"将通过米哈游启动器卸载「{game.Name}」。\n这会关闭游戏并删除游戏文件，确定继续吗？",
            "卸载游戏", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

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

    private async Task StartGameAsync()
    {
        var exe = MikoNestBridge.FindGameExe(_selectedKey);
        if (exe == null)
        {
            OpenUrl(Websites[_selectedKey]);
            Toast("未找到本地游戏，已打开官网下载页");
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
