using Microsoft.Web.WebView2.Core;
using MikoMiko.Services;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace MikoMiko.Views;

/// <summary>追番/影视的软件内播放器：只接收解析后的视频流，不加载第三方站点网页。</summary>
public partial class AnimePlayerView : UserControl
{
    private readonly AnimePlayback _playback;
    private bool _initialized;

    public AnimePlayerView(AnimePlayback playback, Action? closeRequested = null)
    {
        InitializeComponent();
        _playback = playback;
        CloseRequested = closeRequested;
        TitleText.Text = playback.Title;
        MetaText.Text = $"{playback.Episode} · {playback.Source}";
        ModeText.Text = playback.IsHls ? "HLS 直连" : "MP4 直连";
    }

    public Action? CloseRequested { get; set; }

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

            // 部分第三方 CDN 会校验 Referer，在软件内请求视频分片时补上播放页来源。
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) =>
            {
                try
                {
                    if (args.Request.Uri.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                        args.Request.Uri.Contains(".ts", StringComparison.OrdinalIgnoreCase) ||
                        args.Request.Uri.Contains(".key", StringComparison.OrdinalIgnoreCase) ||
                        args.Request.Uri.Contains(".mp4", StringComparison.OrdinalIgnoreCase))
                    {
                        args.Request.Headers.SetHeader("Referer", _playback.Referer);
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

            LoadingPanel.Visibility = Visibility.Collapsed;
            core.NavigateToString(BuildHtml(LoadHlsJs()));
        }
        catch (Exception)
        {
            LoadingPanel.Visibility = Visibility.Visible;
        }
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
  #stage { position:relative; width:100vw; height:100vh; background:radial-gradient(circle at 50% 42%, #132133 0%, #05080d 72%); }
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
  const source = {{source}};
  const video = document.getElementById('video');
  const status = document.getElementById('status');
  let hls = null;
  let retried = 0;

  function show(text) {
    status.textContent = text;
    status.style.display = 'block';
  }
  function hide() { status.style.display = 'none'; }

  video.addEventListener('playing', hide);
  video.addEventListener('canplay', hide);
  video.addEventListener('waiting', function () { show('正在缓冲视频…'); });
  video.addEventListener('error', function () {
    show('视频加载失败，可能是源站限制或地址已失效。');
  });

  function startNativeHls() {
    video.src = source;
    const play = video.play();
    if (play && play.catch) play.catch(function () {});
  }

  try {
    if (source.indexOf('.m3u8') >= 0) {
      if (window.Hls && Hls.isSupported()) {
        hls = new Hls({ enableWorker: true, lowLatencyMode: false, backBufferLength: 90 });
        hls.loadSource(source);
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
        startNativeHls();
      } else {
        show('当前播放器不支持 HLS 视频流。');
      }
    } else {
      video.src = source;
      const play = video.play();
      if (play && play.catch) play.catch(function () {});
    }
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
