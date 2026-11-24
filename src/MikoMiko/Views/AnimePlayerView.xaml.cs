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
    private readonly AnimeCatalogService _catalog = new();
    private readonly AnimeFollowService _follows;
    private readonly IReadOnlyList<AnimeProvider> _providers;
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

    public AnimePlayerView(AnimePlayback playback, IReadOnlyList<AnimeProvider> providers,
        AnimeFollowService follows, Action? closeRequested = null)
    {
        InitializeComponent();
        _playback = playback;
        _follows = follows;
        _providers = providers;
        _referer = playback.Referer;
        CloseRequested = closeRequested;

        UpdateHeader();
        UpdateDetails();
        InitializeProviderList();
        InitializeSources();
        UpdateFollowState();
    }

    public Action? CloseRequested { get; set; }

    /// <summary>壳层关闭播放器时调用，确保 WebView 释放、声音立即停止。</summary>
    public void Shutdown()
    {
        if (_disposed) return;
        _disposed = true;
        try { Web.Dispose(); } catch { /* 窗口已关闭时忽略 */ }
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
            var playback = await _streams.ResolveEpisodeAsync(_playback.Details, source, episode, item);
            if (playback is null || string.IsNullOrWhiteSpace(playback.StreamUrl))
            {
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

    /// <summary>切换网站 / 线路 / 选集后统一刷新标题、简介、线路、选集与视频画面。</summary>
    private async Task ApplyPlaybackAsync(AnimePlayback playback)
    {
        _playback = playback;
        _referer = string.IsNullOrWhiteSpace(playback.Referer) ? _referer : playback.Referer;
        UpdateHeader();
        UpdateDetails();
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

    private void UpdateHeader()
    {
        TitleText.Text = _playback.Title;
        MetaText.Text = $"{_playback.Episode} · {_playback.Source}";
        ModeText.Text = _playback.IsHls ? "HLS 直连" : "MP4 直连";
    }

    private void UpdateDetails()
    {
        DetailTitle.Text = string.IsNullOrWhiteSpace(_playback.Title) ? "追番 / 影视" : _playback.Title;
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
            };

            core.NavigateToString(BuildHtml(LoadHlsJs()));
        }
        catch (Exception)
        {
            LoadingPanel.Visibility = Visibility.Visible;
            LoadingText.Text = "播放器初始化失败，请重新打开。";
        }
    }

    private void Follow_Click(object sender, RoutedEventArgs e)
    {
        if (_playback.CatalogItem is null)
        {
            SwitchStatus.Text = "缺少作品信息，暂时无法加入追剧。";
            return;
        }

        var following = _follows.Toggle(_playback);
        UpdateFollowState();
        SwitchStatus.Text = following ? "已同步到「我的追剧」" : "已从「我的追剧」移除";
    }

    private void UpdateFollowState()
    {
        _isFollowing = _follows.Contains(_playback.CatalogItem?.Url);
        FollowText.Text = _isFollowing ? "已追剧" : "追剧";
        FollowButton.Background = new SolidColorBrush(_isFollowing
            ? Color.FromRgb(0xDF, 0xF2, 0xFE)
            : Color.FromRgb(0x2F, 0x86, 0xB9));
        FollowText.Foreground = new SolidColorBrush(_isFollowing
            ? Color.FromRgb(0x1F, 0x6E, 0x9D)
            : Colors.White);
        FollowIcon.Stroke = FollowText.Foreground;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

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
  #status { position:absolute; left:50%; top:50%; transform:translate(-50%,-50%); min-width:210px;
            padding:16px 22px; border-radius:14px; background:rgba(12,20,31,.86); color:#c7d8e7;
            font:13px/1.7 "Microsoft YaHei UI", "Segoe UI", sans-serif; text-align:center;
            border:1px solid rgba(126,196,242,.25); pointer-events:none; }
  #caption { position:absolute; left:18px; top:16px; color:rgba(232,245,255,.66);
             font:12px "Microsoft YaHei UI", sans-serif; text-shadow:0 1px 4px #000; pointer-events:none; }
</style>
</head>
<body>
<div id="stage">
  <video id="video" controls autoplay playsinline preload="auto"></video>
  <div id="caption">{{title}} · {{episode}}</div>
  <div id="status">正在连接视频流…</div>
</div>
<script>{{hlsJs}}</script>
<script>
(function () {
  const initial = {{source}};
  const initialCaption = {{caption}};
  const video = document.getElementById('video');
  const status = document.getElementById('status');
  const caption = document.getElementById('caption');
  let hls = null;
  let retried = 0;

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
  });

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
    window.mikoPlay(initial, initialCaption);
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
