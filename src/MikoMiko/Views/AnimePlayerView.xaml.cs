using Microsoft.Web.WebView2.Core;
using MikoMiko.Services;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MikoMiko.Views;

/// <summary>
/// 追番/影视的软件内播放器：左侧视频、右侧详情与线路/选集。
/// 只接收解析后的视频流，不加载第三方站点网页；切换网站、线路或选集时重新取流并复用同一个 WebView。
/// </summary>
public partial class AnimePlayerView : UserControl
{
    private readonly AnimeStreamService _streams = new();
    private readonly BilibiliAnimeService _bilibili = new();
    private readonly AnimeCatalogService _catalog = new();
    private readonly AnimeFollowService _follows;
    private readonly AnimeDanmakuService _danmaku;
    private readonly IReadOnlyList<AnimeProvider> _providers;
    private readonly Action<string>? _webFallback;
    private AnimePlayback _playback;
    private string _referer;
    private string _providerName = string.Empty;
    private bool _initialized;
    private bool _webReady;
    private bool _syncSelection;
    private bool _syncProvider;
    private bool _providerReady;
    private bool _switching;
    private bool _isFollowing;
    private bool _disposed;
    private bool _anime4K;
    private bool _fallbackRequested;
    private bool _windowFullscreen;
    private GridLength _detailWidthBeforeFullscreen = new(400);

    public AnimePlayerView(AnimePlayback playback, IReadOnlyList<AnimeProvider> providers,
        AnimeFollowService follows, AnimeDanmakuService danmaku, Action? closeRequested = null,
        bool anime4K = false, Action<string>? webFallback = null)
    {
        InitializeComponent();
        _playback = playback;
        _follows = follows;
        _danmaku = danmaku;
        _providers = providers;
        _referer = playback.Referer;
        _anime4K = anime4K;
        _webFallback = webFallback;
        CloseRequested = closeRequested;
        Anime4KToggle.IsChecked = anime4K;

        UpdateHeader();
        UpdateDetails();
        UpdateComments();
        WebFallbackButton.Visibility = playback.IsBilibili ? Visibility.Collapsed : Visibility.Visible;
        InitializeProviderList();
        InitializeSources();
        UpdateFollowState();
        Unloaded += (_, _) =>
        {
            if (_windowFullscreen && Window.GetWindow(this) is MainWindow window)
                window.ExitVideoFullscreen();
        };
    }

    public Action? CloseRequested { get; set; }

    private void Anime4K_Toggled(object sender, RoutedEventArgs e)
    {
        _anime4K = Anime4KToggle.IsChecked == true;
        UpdateHeader();
        if (!_webReady) return;
        _ = Web.CoreWebView2.ExecuteScriptAsync(
            $"window.mikoSetAnime4K({(_anime4K ? "true" : "false")})");
    }

    /// <summary>壳层关闭播放器时调用，确保 WebView 释放、声音立即停止。</summary>
    public void Shutdown()
    {
        if (_disposed) return;
        _disposed = true;
        try { Web.Dispose(); } catch { /* 窗口已关闭时忽略 */ }
    }

    /// <summary>最小化 / 切页 / 打开其他页面时暂停视频。</summary>
    public void PausePlayback()
    {
        if (!_webReady || _disposed) return;
        try { _ = Web.CoreWebView2.ExecuteScriptAsync("window.mikoPause && window.mikoPause()"); } catch { }
    }

    private void InitializeProviderList()
    {
        var currentName = _playback.CatalogItem?.Source ?? _playback.Source;
        _syncProvider = true;
        ProviderList.ItemsSource = _providers;
        var current = _providers.FirstOrDefault(p =>
            string.Equals(p.Name, currentName, StringComparison.OrdinalIgnoreCase));
        ProviderList.SelectedItem = current;
        _syncProvider = false;
        _providerName = current?.Name ?? currentName;

        if (_providers.Count == 0)
            ProviderSection.Visibility = Visibility.Collapsed;
        else
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => _providerReady = true, DispatcherPriority.ApplicationIdle);
    }

    private void InitializeSources()
    {
        var details = _playback.Details;
        var sources = details?.Sources;
        if (sources is null || sources.Count == 0)
        {
            SourceList.ItemsSource = null;
            EpisodeList.ItemsSource = null;
            SourceList.Visibility = Visibility.Collapsed;
            EpisodeList.Visibility = Visibility.Collapsed;
            EpisodeInfoText.Text = _playback.Episode;
            EpisodeCountText.Text = string.Empty;
            return;
        }

        _syncSelection = true;
        SourceList.ItemsSource = sources;
        var selected = _playback.SelectedSource;
        var index = selected is null ? 0 : Math.Max(0, sources.IndexOf(selected));
        if (index >= sources.Count) index = 0;
        SourceList.SelectedIndex = index;
        UpdateEpisodes(sources[index], _playback.SelectedEpisode);
        _syncSelection = false;

        SourceList.Visibility = sources.Count == 1 ? Visibility.Collapsed : Visibility.Visible;
        EpisodeList.Visibility = Visibility.Visible;
    }

    private void UpdateEpisodes(AnimeSource source, AnimeEpisode? selected)
    {
        _syncSelection = true;
        EpisodeList.ItemsSource = source.Episodes;
        var episode = selected;
        if (episode is null || !source.Episodes.Contains(episode))
        {
            episode = source.Episodes.FirstOrDefault(e => e.Order == (selected?.Order ?? -1))
                      ?? source.Episodes.FirstOrDefault();
        }

        EpisodeList.SelectedItem = episode;
        EpisodeCountText.Text = source.Episodes.Count == 0 ? "暂无剧集" : $"共 {source.Episodes.Count} 集";
        EpisodeInfoText.Text = episode?.Name ?? "选集";
        _syncSelection = false;
    }

    private async void Providers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncProvider || _switching || !_providerReady) return;
        if (ProviderList.SelectedItem is not AnimeProvider provider) return;
        if (string.Equals(provider.Name, _providerName, StringComparison.OrdinalIgnoreCase)) return;
        await SwitchProviderAsync(provider);
    }

    private async Task SwitchProviderAsync(AnimeProvider provider)
    {
        if (_switching || _disposed) return;
        var item = _playback.CatalogItem;
        if (item is null)
        {
            ProviderList_Select(_providerName);
            SwitchStatus.Text = "缺少作品信息，暂时无法查询其他网站。";
            return;
        }

        _switching = true;
        SwitchStatus.Text = $"正在从 {provider.Name} 查询《{_playback.Title}》…";
        LoadingText.Text = $"正在查询 {provider.Name}…";
        LoadingPanel.Visibility = Visibility.Visible;
        var previousProvider = _providerName;
        try
        {
            var results = await _catalog.SearchAsync(new[] { provider }, _playback.Title, CancellationToken.None);
            if (results.Count == 0)
            {
                ProviderList_Select(previousProvider);
                SwitchStatus.Text = $"{provider.Name} 没有查询到这部作品，可以换一个网站。";
                return;
            }

            var target = NormalizeTitle(_playback.Title);
            var best = results.FirstOrDefault(i => NormalizeTitle(i.Title) == target)
                       ?? results.FirstOrDefault(i =>
                           NormalizeTitle(i.Title).Contains(target, StringComparison.OrdinalIgnoreCase) ||
                           target.Contains(NormalizeTitle(i.Title), StringComparison.OrdinalIgnoreCase))
                       ?? results[0];

            var next = await _streams.ResolveAsync(best);
            if (next is null || string.IsNullOrWhiteSpace(next.StreamUrl))
            {
                ProviderList_Select(previousProvider);
                SwitchStatus.Text = $"{provider.Name} 找到了《{best.Title}》，但暂时取不到视频，可以换一个网站。";
                return;
            }

            _providerName = provider.Name;
            await ApplyPlaybackAsync(next);
            SwitchStatus.Text = $"已切换到 {provider.Name} · {best.Title}";
        }
        catch
        {
            ProviderList_Select(previousProvider);
            SwitchStatus.Text = "网站查询失败，请稍后重试。";
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            _switching = false;
        }
    }

    private void ProviderList_Select(string providerName)
    {
        _syncProvider = true;
        ProviderList.SelectedItem = _providers.FirstOrDefault(p =>
            string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase));
        _syncProvider = false;
    }

    private static string NormalizeTitle(string title)
        => Regex.Replace(title ?? string.Empty, @"[\s:：·\.\-—!！?？,，。()（）\[\]【】《》]", string.Empty);

    private async void Sources_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncSelection || _initialized == false) return;
        if (SourceList.SelectedItem is not AnimeSource source) return;

        var previousOrder = _playback.SelectedEpisode?.Order ?? 0;
        var target = source.Episodes.FirstOrDefault(x => x.Order == previousOrder)
                     ?? source.Episodes.FirstOrDefault();
        UpdateEpisodes(source, target);
        if (target is not null) await SwitchAsync(source, target);
    }

    private async void Episodes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncSelection) return;
        if (SourceList.SelectedItem is not AnimeSource source) return;
        if (EpisodeList.SelectedItem is not AnimeEpisode episode) return;
        await SwitchAsync(source, episode);
    }

    private async Task SwitchAsync(AnimeSource source, AnimeEpisode episode)
    {
        if (_switching) return;
        if (_playback.Details is null) return;

        var item = _playback.CatalogItem;
        if (item is null)
        {
            SwitchStatus.Text = "缺少作品信息，暂时无法切换剧集。";
            return;
        }

        // 同一线路的同一集不重复请求。
        if (string.Equals(_playback.SelectedEpisode?.PageUrl, episode.PageUrl, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_playback.SelectedSource?.Name, source.Name, StringComparison.Ordinal))
            return;

        _switching = true;
        SwitchStatus.Text = $"正在切换到 {episode.Name}…";
        LoadingText.Text = $"正在解析 {episode.Name}…";
        LoadingPanel.Visibility = Visibility.Visible;

        try
        {
            var playback = _playback.IsBilibili
                ? await _bilibili.ResolveEpisodeAsync(_playback, episode, CancellationToken.None)
                : await _streams.ResolveEpisodeAsync(_playback.Details, source, episode, item);
            if (playback is null || string.IsNullOrWhiteSpace(playback.StreamUrl))
            {
                SwitchStatus.Text = $"{source.Name} · {episode.Name} 无法提取，正在自动切换其他站点…";
                LoadingText.Text = "正在自动切换站点…";
                LoadingPanel.Visibility = Visibility.Visible;
                if (await TryAutoSwitchAsync(episode.Order, source.Name))
                    return;
                LoadingPanel.Visibility = Visibility.Collapsed;
                SwitchStatus.Text = $"{source.Name} · {episode.Name} 暂时无法播放，可以换一条线路。";
                return;
            }

            _playback = playback;
            await ApplyPlaybackAsync(playback);
            SwitchStatus.Text = string.Empty;
        }
        catch (Exception)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            SwitchStatus.Text = "切换失败，请稍后再试或换一条线路。";
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            _switching = false;
        }
    }

    /// <summary>当前线路取不到流时，按作品名自动检索其他站点，优先找同一集序号。</summary>
    private async Task<bool> TryAutoSwitchAsync(int episodeOrder, string failedProvider)
    {
        var item = _playback.CatalogItem;
        if (item is null) return false;

        var playable = _providers
            .Where(provider => !string.Equals(provider.Name, failedProvider, StringComparison.Ordinal))
            .Where(provider => provider.Parser is "module-poster" or "stui" or "manmankan" or "generic" or "mhys")
            .Where(provider => !string.IsNullOrWhiteSpace(provider.Search))
            .ToList();
        if (playable.Count == 0) return false;

        var results = await _catalog.SearchAsync(playable, item.Title, CancellationToken.None);
        var normalized = NormalizeTitle(item.Title);
        var candidates = results
            .Where(candidate => !string.Equals(candidate.Source, failedProvider, StringComparison.Ordinal))
            .DistinctBy(candidate => candidate.Source + "|" + candidate.Url)
            .OrderBy(candidate => NormalizeTitle(candidate.Title) == normalized ? 0 :
                NormalizeTitle(candidate.Title).Contains(normalized, StringComparison.Ordinal) ? 1 : 2)
            .Take(4)
            .ToList();

        foreach (var candidate in candidates)
        {
            SwitchStatus.Text = $"自动尝试「{candidate.Source}」…";
            try
            {
                var first = await _streams.ResolveAsync(candidate);
                var details = first?.Details;
                if (details is null) continue;
                var source = details.Sources.FirstOrDefault(s => s.Episodes.Any(e => e.Order == episodeOrder))
                             ?? details.Sources.FirstOrDefault();
                var episode = source?.Episodes.FirstOrDefault(e => e.Order == episodeOrder)
                              ?? source?.Episodes.FirstOrDefault();
                if (source is null || episode is null) continue;

                var playback = await _streams.ResolveEpisodeAsync(details, source, episode, candidate);
                if (playback is null || string.IsNullOrWhiteSpace(playback.StreamUrl)) continue;

                _providerName = candidate.Source;
                _playback = playback;
                await ApplyPlaybackAsync(playback);
                ProviderList_Select(candidate.Source);
                SwitchStatus.Text = $"已自动切换到「{candidate.Source}」· {episode.Name}";
                return true;
            }
            catch
            {
                // 单个站点失败后继续尝试下一站。
            }
        }

        return false;
    }

    /// <summary>切换网站 / 线路 / 选集后统一刷新标题、简介、线路、选集与视频画面。</summary>
    private async Task ApplyPlaybackAsync(AnimePlayback playback)
    {
        _playback = playback;
        _referer = string.IsNullOrWhiteSpace(playback.Referer) ? _referer : playback.Referer;
        UpdateHeader();
        UpdateDetails();
        UpdateComments();
        WebFallbackButton.Visibility = playback.IsBilibili ? Visibility.Collapsed : Visibility.Visible;
        InitializeSources();
        UpdateFollowState();
        await LoadPosterAsync(playback);

        if (_webReady)
        {
            var caption = $"{playback.Title} · {playback.Episode}";
            await Web.CoreWebView2.ExecuteScriptAsync(
                $"window.mikoPlay({JsonSerializer.Serialize(playback.StreamUrl)}, {JsonSerializer.Serialize(caption)})");
        }
    }

    private void Web_MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
            if (type == "playerError") RequestWebFallback();
            else if (type == "fullscreen") ToggleWindowFullscreen();
            else if (type == "exitFullscreen" && _windowFullscreen) ToggleWindowFullscreen();
        }
        catch
        {
            // 非法消息直接忽略，不打断播放。
        }
    }

    private async Task LoadPosterAsync(AnimePlayback playback)
    {
        var poster = string.IsNullOrWhiteSpace(playback.Poster) ? playback.Details?.Poster : playback.Poster;
        if (string.IsNullOrWhiteSpace(poster)) return;
        try
        {
            var image = await ImageLoader.LoadAsync(poster, _referer);
            if (image is not null) PosterImage.Source = image;
        }
        catch
        {
            // 海报失败不影响视频。
        }
    }

    private void UpdateComments()
    {
        var comments = _playback.Comments;
        CommentList.ItemsSource = comments;
        CommentCountText.Text = comments.Count > 0 ? $"{comments.Count} 条" : string.Empty;
        CommentEmpty.Visibility = comments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateHeader()
    {
        TitleText.Text = _playback.Title;
        MetaText.Text = $"{_playback.Episode} · {_playback.Source}";
        ModeText.Text = (_playback.IsHls ? "HLS 直连" : "MP4 直连") + (_anime4K ? " · Anime4K" : "");
    }

    private void UpdateDetails()
    {        DetailTitle.Text = string.IsNullOrWhiteSpace(_playback.Title) ? "追番 / 影视" : _playback.Title;
        var meta = string.IsNullOrWhiteSpace(_playback.Meta) ? _playback.Source : _playback.Meta;
        DetailMeta.Text = meta;
        var description = _playback.Description?.Trim() ?? string.Empty;
        DescriptionText.Text = description.Length < 20
            ? "该站点没有提供完整简介。播放线路与选集都在这个面板里，可直接切换。"
            : description;
    }

    private async void Web_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var dataDir = Path.Combine(SettingsService.CacheDirectory, "webview");
            Directory.CreateDirectory(dataDir);
            // WebView2 默认会拦截带声音的自动播放，播放器打开后应直接出画面。
            var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDir, options);
            await Web.EnsureCoreWebView2Async(environment);

            var core = Web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;

            await LoadPosterAsync(_playback);

            // 部分第三方 CDN 会校验 Referer，在软件内请求视频分片时补上对应详情页来源。
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) =>
            {
                try
                {
                    var uri = args.Request.Uri;
                    if (uri.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                        uri.Contains(".ts", StringComparison.OrdinalIgnoreCase) ||
                        uri.Contains(".key", StringComparison.OrdinalIgnoreCase) ||
                        uri.Contains(".mp4", StringComparison.OrdinalIgnoreCase))
                    {
                        args.Request.Headers.SetHeader("Referer", _referer);
                    }
                }
                catch
                {
                    // 个别请求头不允许修改时保持默认。
                }
            };
            core.NavigationStarting += (_, args) =>
            {
                if (!args.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase) &&
                    !args.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    args.Cancel = true;
            };
            core.NavigationCompleted += (_, _) =>
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                _webReady = true;
                _ = Web.CoreWebView2.ExecuteScriptAsync(
                    $"window.mikoSetAnime4K({(_anime4K ? "true" : "false")})");
            };
            core.WebMessageReceived += Web_MessageReceived;

            core.NavigateToString(BuildHtml(LoadHlsJs()));
        }
        catch (Exception)
        {
            LoadingPanel.Visibility = Visibility.Visible;
            LoadingText.Text = "播放器初始化失败，请重新打开。";
        }
    }

    private void ToggleWindowFullscreen()
    {
        if (Window.GetWindow(this) is not MainWindow window) return;
        if (!_windowFullscreen)
        {
            _windowFullscreen = true;
            _detailWidthBeforeFullscreen = PlayerDetailColumn.Width;
            PlayerDetailColumn.Width = new GridLength(0);
            PlayerTopRow.Height = new GridLength(0);
            window.EnterVideoFullscreen();
            _ = Web.CoreWebView2?.ExecuteScriptAsync("window.mikoSetFullscreen && window.mikoSetFullscreen(true)");
        }
        else
        {
            _windowFullscreen = false;
            window.ExitVideoFullscreen();
            PlayerTopRow.Height = new GridLength(52);
            PlayerDetailColumn.Width = _detailWidthBeforeFullscreen;
            _ = Web.CoreWebView2?.ExecuteScriptAsync("window.mikoSetFullscreen && window.mikoSetFullscreen(false)");
        }
    }

    private void Follow_Click(object sender, RoutedEventArgs e)
    {
        if (_playback.CatalogItem is null)
        {
            SwitchStatus.Text = "缺少作品信息，暂时无法加入追番。";
            return;
        }

        var following = _follows.Toggle(_playback);
        UpdateFollowState();
        SwitchStatus.Text = following ? "已同步到「我的追番」" : "已从「我的追番」移除";
    }

    private void UpdateFollowState()
    {
        _isFollowing = _follows.Contains(_playback.CatalogItem?.Url);
        FollowText.Text = _isFollowing ? "已追番" : "追番";
        FollowButton.Background = new SolidColorBrush(_isFollowing
            ? Color.FromRgb(0xDF, 0xF2, 0xFE)
            : Color.FromRgb(0x2F, 0x86, 0xB9));
        FollowText.Foreground = new SolidColorBrush(_isFollowing
            ? Color.FromRgb(0x1F, 0x6E, 0x9D)
            : Colors.White);
        FollowIcon.Stroke = FollowText.Foreground;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void WebFallback_Click(object sender, RoutedEventArgs e) => RequestWebFallback();

    /// <summary>直连流反复失败时，关闭本站播放器，改用站点的网页播放页。</summary>
    private void RequestWebFallback()
    {
        if (_fallbackRequested || _playback.IsBilibili) return;
        var url = _playback.SelectedEpisode?.PageUrl;
        if (string.IsNullOrWhiteSpace(url)) url = _playback.CatalogItem?.PlayUrl;
        if (string.IsNullOrWhiteSpace(url)) url = _playback.CatalogItem?.Url;
        if (string.IsNullOrWhiteSpace(url) || _webFallback is null) return;
        _fallbackRequested = true;
        var callback = _webFallback;
        // 不要在 WebMessageReceived 回调里直接 Dispose 当前 WebView2，延后到消息处理结束。
        Dispatcher.BeginInvoke(new Action(() => callback(url)));
    }

    private static string LoadHlsJs()
    {
        try
        {
            var info = Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/Anime/hls.min.js", UriKind.Absolute));
            if (info?.Stream is null) return string.Empty;
            using var reader = new StreamReader(info.Stream);
            return reader.ReadToEnd().Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string BuildHtml(string hlsJs)
    {
        var source = JsonSerializer.Serialize(_playback.StreamUrl);
        var caption = JsonSerializer.Serialize($"{_playback.Title} · {_playback.Episode}");
        var title = WebUtility.HtmlEncode(_playback.Title);
        var episode = WebUtility.HtmlEncode(_playback.Episode);

        return $$"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{title}}</title>
<style>
  html, body { margin:0; padding:0; width:100%; height:100%; overflow:hidden; background:#000; }
  #stage { position:relative; width:100vw; height:100vh; background:#000; }
  video { width:100%; height:100%; object-fit:contain; background:#000; outline:none; }
  #glCanvas { position:absolute; inset:0; width:100%; height:100%; background:#000;
              display:none; pointer-events:none; }
  #playerBar { position:absolute; left:0; right:0; bottom:0; height:46px; z-index:12;
               display:none; align-items:center; gap:12px; padding:0 16px; box-sizing:border-box;
               background:linear-gradient(180deg, rgba(4,10,16,.10), rgba(4,10,16,.88) 60%);
               color:#DDEBF6; font:12px "Microsoft YaHei UI", sans-serif; }
  #playerBar button { border:0; border-radius:7px; height:26px; padding:0 10px; cursor:pointer;
                      color:#EAF6FF; background:rgba(78,164,221,.18); font:11.5px "Microsoft YaHei UI", sans-serif; }
  #playerBar button:hover { background:rgba(78,164,221,.36); }
  #seekBar { flex:1; min-width:90px; accent-color:#4EA4DD; cursor:pointer; }
  #timeText { min-width:94px; text-align:center; color:#B9CEDD; font-variant-numeric:tabular-nums; }
  #a4kBadge { color:#8FD3F4; border:1px solid rgba(143,211,244,.35); border-radius:5px;
              padding:2px 6px; font-size:10px; }
  #status { position:absolute; left:50%; top:50%; transform:translate(-50%,-50%); min-width:210px;
            padding:16px 22px; border-radius:14px; background:rgba(12,20,31,.86); color:#c7d8e7;
            font:13px/1.7 "Microsoft YaHei UI", "Segoe UI", sans-serif; text-align:center;
            border:1px solid rgba(126,196,242,.25); pointer-events:none; }
  #caption { position:absolute; left:18px; top:16px; color:rgba(232,245,255,.66);
             font:12px "Microsoft YaHei UI", sans-serif; text-shadow:0 1px 4px #000; pointer-events:none; }
  #fsFloat { position:absolute; right:18px; bottom:18px; z-index:25; width:46px; height:46px;
             border:1px solid rgba(170,216,244,.42); border-radius:15px; cursor:pointer;
             background:linear-gradient(160deg, rgba(26,46,64,.86), rgba(8,16,25,.9));
             color:#E8F5FF; display:flex; align-items:center; justify-content:center;
             box-shadow:0 6px 20px rgba(0,0,0,.35); transition:background .18s, transform .18s; }
  #fsFloat:hover { background:rgba(78,164,221,.55); transform:translateY(-1px); }
  #fsFloat svg { width:24px; height:24px; stroke:#E8F5FF; stroke-width:2; fill:none; }
</style>
</head>
<body>
<div id="stage">
  <video id="video" controls autoplay playsinline preload="auto"></video>
  <canvas id="glCanvas"></canvas>
  <div id="playerBar">
    <button id="ppBtn" type="button">暂停</button>
    <span id="timeText">00:00 / 00:00</span>
    <input id="seekBar" type="range" min="0" max="1000" value="0" step="1">
    <span id="a4kBadge">Anime4K</span>
    <button id="muteBtn" type="button">静音</button>
    <button id="fullBtn" type="button">全屏</button>
  </div>
  <button id="fsFloat" type="button" title="全屏播放">
    <svg viewBox="0 0 24 24"><path d="M4 9V4h5M15 4h5v5M20 15v5h-5M9 20H4v-5"/></svg>
  </button>
  <div id="caption">{{title}} · {{episode}}</div>
  <div id="status">正在连接视频流…</div>
</div>
<script>{{hlsJs}}</script>
<script>
(function () {
  const initial = {{source}};
  const initialCaption = {{caption}};
  const initialAnime4K = {{(_anime4K ? "true" : "false")}};
  const stage = document.getElementById('stage');
  const video = document.getElementById('video');
  const status = document.getElementById('status');
  const caption = document.getElementById('caption');
  const glCanvas = document.getElementById('glCanvas');
  const playerBar = document.getElementById('playerBar');
  const ppBtn = document.getElementById('ppBtn');
  const timeText = document.getElementById('timeText');
  const seekBar = document.getElementById('seekBar');
  const muteBtn = document.getElementById('muteBtn');
  const fullBtn = document.getElementById('fullBtn');
  const fsFloat = document.getElementById('fsFloat');
  let hls = null;
  let retried = 0;
  let a4k = false;
  let gl = null, glProgram = null, glTexture = null, glTexel = null;
  let glReady = false, glWidth = 0, glHeight = 0, rafId = 0, lastDraw = 0;

  function formatTime(value) {
    const total = Math.max(0, Math.floor(value || 0));
    const m = Math.floor(total / 60), s = total % 60;
    return (m < 10 ? '0' : '') + m + ':' + (s < 10 ? '0' : '') + s;
  }
  function syncTime() {
    const duration = isFinite(video.duration) ? video.duration : 0;
    const current = video.currentTime || 0;
    timeText.textContent = formatTime(current) + ' / ' + formatTime(duration);
    if (!seekBar.dataset.dragging)
      seekBar.value = duration > 0 ? Math.round(current / duration * 1000) : 0;
    ppBtn.textContent = video.paused ? '播放' : '暂停';
    muteBtn.textContent = video.muted || video.volume === 0 ? '已静音' : '静音';
  }
  video.addEventListener('timeupdate', syncTime);
  video.addEventListener('durationchange', syncTime);
  video.addEventListener('play', syncTime);
  video.addEventListener('pause', syncTime);
  video.addEventListener('volumechange', syncTime);

  ppBtn.addEventListener('click', function () {
    if (video.paused) { const p = video.play(); if (p && p.catch) p.catch(function () {}); }
    else video.pause();
  });
  muteBtn.addEventListener('click', function () { video.muted = !video.muted; });
  function toggleWindowFullscreen() {
    if (window.chrome && chrome.webview) {
      chrome.webview.postMessage({ type: 'fullscreen' });
    } else if (document.fullscreenElement) {
      document.exitFullscreen();
    } else if (stage.requestFullscreen) {
      stage.requestFullscreen();
    }
  }
  fullBtn.addEventListener('click', toggleWindowFullscreen);
  fsFloat.addEventListener('click', toggleWindowFullscreen);
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && window.chrome && chrome.webview)
      chrome.webview.postMessage({ type: 'exitFullscreen' });
  });
  window.mikoSetFullscreen = function (active) {
    fsFloat.title = active ? '退出全屏' : '全屏播放';
    fsFloat.classList.toggle('on', !!active);
  };
  seekBar.addEventListener('input', function () {
    seekBar.dataset.dragging = '1';
    const duration = isFinite(video.duration) ? video.duration : 0;
    if (duration > 0) video.currentTime = duration * (Number(seekBar.value) || 0) / 1000;
  });
  ['change', 'pointerup', 'touchend'].forEach(function (name) {
    seekBar.addEventListener(name, function () { delete seekBar.dataset.dragging; syncTime(); });
  });
  document.addEventListener('fullscreenchange', function () {
    fullBtn.textContent = document.fullscreenElement ? '退出全屏' : '全屏';
    glWidth = 0; glHeight = 0;
    setTimeout(function () { if (a4k) drawGL(); }, 80);
  });

  function compileShader(type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
      const message = gl.getShaderInfoLog(shader);
      gl.deleteShader(shader);
      throw new Error(message || 'shader compile failed');
    }
    return shader;
  }

  function initGL() {
    if (glReady) return true;
    try {
      gl = glCanvas.getContext('webgl2', {
        alpha: false, antialias: false, depth: false, stencil: false,
        preserveDrawingBuffer: false, powerPreference: 'high-performance'
      });
      if (!gl) return false;
      const vsSource = '#version 300 es\n' +
        'in vec2 a_pos; out vec2 v_uv;\n' +
        'void main(){ v_uv = a_pos * 0.5 + 0.5; gl_Position = vec4(a_pos, 0.0, 1.0); }';
      const fsSource = '#version 300 es\n' +
        'precision mediump float;\n' +
        'in vec2 v_uv; out vec4 outColor;\n' +
        'uniform sampler2D u_tex; uniform vec2 u_texel;\n' +
        'float lum(vec3 c){ return dot(c, vec3(0.299, 0.587, 0.114)); }\n' +
        'void main() {\n' +
        '  vec2 t = u_texel;\n' +
        '  vec3 c = texture(u_tex, v_uv).rgb;\n' +
        '  vec3 l = texture(u_tex, v_uv - vec2(t.x, 0.0)).rgb;\n' +
        '  vec3 r = texture(u_tex, v_uv + vec2(t.x, 0.0)).rgb;\n' +
        '  vec3 u = texture(u_tex, v_uv + vec2(0.0, t.y)).rgb;\n' +
        '  vec3 d = texture(u_tex, v_uv - vec2(0.0, t.y)).rgb;\n' +
        '  vec3 tl = texture(u_tex, v_uv + vec2(-t.x, t.y)).rgb;\n' +
        '  vec3 tr = texture(u_tex, v_uv + vec2(t.x, t.y)).rgb;\n' +
        '  vec3 bl = texture(u_tex, v_uv + vec2(-t.x, -t.y)).rgb;\n' +
        '  vec3 br = texture(u_tex, v_uv + vec2(t.x, -t.y)).rgb;\n' +
        '  vec3 blur = (l + r + u + d) * 0.15 + (tl + tr + bl + br) * 0.1;\n' +
        '  vec3 detail = c - blur;\n' +
        '  float edge = length(c - blur);\n' +
        '  float amount = clamp(0.28 + edge * 2.4, 0.28, 1.25);\n' +
        '  vec3 sharp = c + detail * amount;\n' +
        '  vec3 lo = min(min(min(l, r), min(u, d)), c);\n' +
        '  vec3 hi = max(max(max(l, r), max(u, d)), c);\n' +
        '  sharp = clamp(sharp, lo, hi);\n' +
        '  float lineEdge = smoothstep(0.018, 0.11, edge);\n' +
        '  float g = lum(sharp);\n' +
        '  sharp = mix(sharp, vec3(g) * 0.95, lineEdge * 0.22);\n' +
        '  outColor = vec4(clamp(sharp, 0.0, 1.0), 1.0);\n' +
        '}';
      const program = gl.createProgram();
      gl.attachShader(program, compileShader(gl.VERTEX_SHADER, vsSource));
      gl.attachShader(program, compileShader(gl.FRAGMENT_SHADER, fsSource));
      gl.linkProgram(program);
      if (!gl.getProgramParameter(program, gl.LINK_STATUS)) return false;
      glProgram = program;
      gl.useProgram(program);

      const buffer = gl.createBuffer();
      gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferData(gl.ARRAY_BUFFER,
        new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);
      const position = gl.getAttribLocation(program, 'a_pos');
      gl.enableVertexAttribArray(position);
      gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);

      glTexture = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, glTexture);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
      glTexel = gl.getUniformLocation(program, 'u_texel');
      gl.uniform1i(gl.getUniformLocation(program, 'u_tex'), 0);
      glReady = true;
      return true;
    } catch (e) {
      return false;
    }
  }

  function resizeGL() {
    const ratio = Math.min(window.devicePixelRatio || 1, 1.5);
    let w = Math.max(2, Math.round(stage.clientWidth * ratio));
    let h = Math.max(2, Math.round(stage.clientHeight * ratio));
    const cap = 2560 * 1440;
    if (w * h > cap) {
      const scale = Math.sqrt(cap / (w * h));
      w = Math.round(w * scale); h = Math.round(h * scale);
    }
    if (w === glWidth && h === glHeight) return;
    glWidth = w; glHeight = h;
    glCanvas.width = w; glCanvas.height = h;
  }

  function drawGL() {
    if (!a4k || !glReady || !video.videoWidth || video.readyState < 2) return;
    resizeGL();
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, glTexture);
    try {
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, video);
    } catch (e) {
      window.mikoSetAnime4K(false);
      show('Anime4K 无法读取当前视频帧（跨域限制），已自动关闭。');
      return;
    }
    gl.uniform2f(glTexel, 1.0 / video.videoWidth, 1.0 / video.videoHeight);
    const cw = glCanvas.width, ch = glCanvas.height;
    const va = video.videoWidth / video.videoHeight, ca = cw / ch;
    let vx = 0, vy = 0, vw = cw, vh = ch;
    if (ca > va) { vw = Math.round(ch * va); vx = Math.round((cw - vw) / 2); }
    else { vh = Math.round(cw / va); vy = Math.round((ch - vh) / 2); }
    gl.viewport(vx, vy, vw, vh);
    gl.clearColor(0, 0, 0, 1);
    gl.clear(gl.COLOR_BUFFER_BIT);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
  }

  function a4kLoop(timestamp) {
    if (!a4k) return;
    if (timestamp - lastDraw > 28) { drawGL(); lastDraw = timestamp; }
    rafId = requestAnimationFrame(a4kLoop);
  }

  window.mikoSetAnime4K = function (on) {
    if (on && !initGL()) {
      show('Anime4K 初始化失败，当前设备不支持 WebGL2 超分。');
      on = false;
    }
    a4k = !!on;
    if (a4k) {
      video.controls = false;
      glCanvas.style.display = 'block';
      playerBar.style.display = 'flex';
      lastDraw = 0;
      if (!rafId) rafId = requestAnimationFrame(a4kLoop);
      drawGL();
      syncTime();
    } else {
      if (rafId) cancelAnimationFrame(rafId);
      rafId = 0;
      glCanvas.style.display = 'none';
      playerBar.style.display = 'none';
      video.controls = true;
    }
  };


  function show(text) {
    status.textContent = text;
    status.style.display = 'block';
  }
  function hide() { status.style.display = 'none'; }
  function resetVideo() {
    try { video.pause(); } catch (_) {}
    video.removeAttribute('src');
    try { video.load(); } catch (_) {}
  }

  video.addEventListener('playing', hide);
  video.addEventListener('canplay', hide);
  video.addEventListener('waiting', function () { show('正在缓冲视频…'); });
  video.addEventListener('error', function () {
    show('视频加载失败，可能是源站限制或地址已失效。');
    try {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ type: 'playerError' });
      }
    } catch (_) {}
  });

  window.mikoPause = function () {
    try { video.pause(); } catch (_) {}
  };

  window.mikoPlay = function (src, text) {
    if (!src) { show('没有拿到可播放的视频地址。'); return; }
    if (text) caption.textContent = text;
    if (hls) { try { hls.destroy(); } catch (_) {} hls = null; }
    retried = 0;
    resetVideo();
    show('正在连接视频流…');

    if (src.indexOf('.m3u8') >= 0) {
      if (window.Hls && Hls.isSupported()) {
        hls = new Hls({ enableWorker: true, lowLatencyMode: false, backBufferLength: 90 });
        hls.loadSource(src);
        hls.attachMedia(video);
        hls.on(Hls.Events.MANIFEST_PARSED, function () {
          const play = video.play();
          if (play && play.catch) play.catch(function () {});
        });
        hls.on(Hls.Events.ERROR, function (_, data) {
          if (!data || !data.fatal) return;
          if (data.type === Hls.ErrorTypes.NETWORK_ERROR && retried < 2) {
            retried++;
            show('视频流重连中…');
            hls.startLoad();
            return;
          }
          if (data.type === Hls.ErrorTypes.MEDIA_ERROR && retried < 2) {
            retried++;
            show('正在恢复画面…');
            hls.recoverMediaError();
            return;
          }
          show('视频加载失败：' + (data.details || '未知错误'));
          try {
            if (window.chrome && window.chrome.webview) {
              window.chrome.webview.postMessage({ type: 'playerError' });
            }
          } catch (_) {}
        });
      } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
        video.src = src;
        const play = video.play();
        if (play && play.catch) play.catch(function () {});
      } else {
        show('当前播放器不支持 HLS 视频流。');
      }
    } else {
      video.src = src;
      const play = video.play();
      if (play && play.catch) play.catch(function () {});
    }
  };

  try {
    window.mikoSetAnime4K(initialAnime4K);
    window.mikoPlay(initial, initialCaption);
    syncTime();
  } catch (e) {
    show('播放失败：' + (e && e.message ? e.message : e));
  }

  window.addEventListener('beforeunload', function () {
    try { if (hls) hls.destroy(); } catch (_) {}
  });
})();
</script>
</body>
</html>
""";
    }
}

