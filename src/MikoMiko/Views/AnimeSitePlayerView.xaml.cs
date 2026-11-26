using Microsoft.Web.WebView2.Core;
using MikoMiko.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MikoMiko.Views;

/// <summary>
/// 站点网页播放器：当规则站点的播放页由前端脚本生成、无法直接提取 m3u8 时，
/// 在软件内用 WebView2 打开站点的原播放页，让站点自己的播放器完成取流与播放。
/// 这是播不出来的兜底路径，不把用户丢到系统浏览器。
/// </summary>
public partial class AnimeSitePlayerView : UserControl
{
    private readonly string _pageUrl;
    private readonly string _referer;
    private readonly string _title;
    private readonly string _episode;
    private readonly string _source;
    private readonly Action? _closeRequested;
    private bool _initialized;
    private bool _disposed;
    private DispatcherTimer? _loadWatchdog;

    public AnimeSitePlayerView(string pageUrl, string referer, string title, string episode,
        string source, Action? closeRequested = null)
    {
        InitializeComponent();
        _pageUrl = pageUrl;
        _referer = referer;
        _title = title;
        _episode = episode;
        _source = source;
        _closeRequested = closeRequested;

        TitleText.Text = string.IsNullOrWhiteSpace(title) ? "追番 / 影视" : title;
        MetaText.Text = string.IsNullOrWhiteSpace(episode)
            ? $"{source} · 网页播放"
            : $"{episode} · {source} · 网页播放";
        Loaded += OnLoaded;
    }

    public void Shutdown()
    {
        if (_disposed) return;
        _disposed = true;
        _loadWatchdog?.Stop();
        try { Web.Dispose(); } catch { }
    }

    /// <summary>最小化 / 切页时暂停网页里的音视频。</summary>
    public void PausePlayback()
    {
        if (!_initialized || _disposed) return;
        try
        {
            _ = Web.CoreWebView2?.ExecuteScriptAsync(
                "document.querySelectorAll('video,audio').forEach(function(m){try{m.pause();}catch(e){}});");
        }
        catch { }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var dataDir = Path.Combine(SettingsService.CacheDirectory, "webview-site-player");
            Directory.CreateDirectory(dataDir);
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await Web.EnsureCoreWebView2Async(environment);

            var core = Web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;

            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var target) &&
                    (target.Scheme == "http" || target.Scheme == "https"))
                {
                    core.Navigate(target.ToString());
                }
            };

            core.NavigationCompleted += (_, args) =>
            {
                _loadWatchdog?.Stop();
                if (args.IsSuccess)
                {
                    StatusPanel.Visibility = Visibility.Collapsed;
                    Web.Visibility = Visibility.Visible;
                    // 站点播放页常把新窗口链接挂在选集上，统一改成当前页打开。
                    _ = core.ExecuteScriptAsync(
                        "document.querySelectorAll('a[target=_blank]').forEach(function(a){a.removeAttribute('target');});");
                }
                else
                {
                    StatusPanel.Visibility = Visibility.Visible;
                    Web.Visibility = Visibility.Hidden;
                    StatusTitle.Text = "站点播放页打开失败";
                    StatusDetail.Text = $"HTTP 状态：{args.WebErrorStatus}。可以点右上角用系统浏览器打开，或换一个站点线路。";
                }
            };

            if (!string.IsNullOrWhiteSpace(_pageUrl))
            {
                _loadWatchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                _loadWatchdog.Tick += (_, _) =>
                {
                    _loadWatchdog?.Stop();
                    if (Web.Visibility == Visibility.Visible) return;
                    StatusPanel.Visibility = Visibility.Visible;
                    Web.Visibility = Visibility.Hidden;
                    StatusTitle.Text = "站点播放页打开超时";
                    StatusDetail.Text = "站点可能被网络阻断。点左上角返回换一个来源，或点右上角用系统浏览器打开。";
                };
                _loadWatchdog.Start();
                try
                {
                    var headers = string.IsNullOrWhiteSpace(_referer)
                        ? string.Empty
                        : $"Referer: {_referer}\r\n";
                    var request = core.Environment.CreateWebResourceRequest(
                        _pageUrl, "GET", Stream.Null, headers);
                    core.NavigateWithWebResourceRequest(request);
                }
                catch
                {
                    core.Navigate(_pageUrl);
                }
            }
        }
        catch (Exception ex)
        {
            StatusPanel.Visibility = Visibility.Visible;
            StatusTitle.Text = "网页播放器启动失败";
            StatusDetail.Text = ex.Message + "\n请确认已安装 WebView2 运行时。";
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _closeRequested?.Invoke();

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is null) return;
        try { Web.CoreWebView2.Reload(); } catch { }
    }

    private void Browser_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_pageUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_pageUrl) { UseShellExecute = true });
        }
        catch { }
    }
}
