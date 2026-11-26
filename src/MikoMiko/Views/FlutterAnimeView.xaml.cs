using Microsoft.Web.WebView2.Core;
using MikoMiko.Services;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MikoMiko.Views;

/// <summary>
/// 追番/影视的 Flutter 模块宿主。Flutter Web 产物通过 WebView2 加载，
/// 站点页面与图片由 miko.proxy 同源代理转发，播放请求回传给 WPF 内置播放器。
/// </summary>
public partial class FlutterAnimeView : UserControl
{
    private const string VirtualHost = "miko.anime";
    private const string ProxyHost = "https://miko.proxy/";
    private readonly Action<AnimeItem, bool> _playRequested;
    private readonly Action<string, string> _bilibiliRequested;
    private readonly HttpClient _http;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _bridgeTimer;
    private bool _bridgePolling;
    private CoreWebView2? _core;
    private bool _initialized;
    private bool _disposed;

    internal static void Log(string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "miko-flutter.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }

    public FlutterAnimeView(Action<AnimeItem, bool> playRequested, Action<string, string> bilibiliRequested)
    {
        InitializeComponent();
        _playRequested = playRequested;
        _bilibiliRequested = bilibiliRequested;
        _http = CreateHttpClient();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastPanel.Visibility = Visibility.Collapsed;
        };
        // WebView2 的 WebMessageReceived 在个别运行时版本里收不到消息，
        // 用 JS 队列轮询兜底，保证选集与 toast 一定能回到宿主。
        _bridgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _bridgeTimer.Tick += async (_, _) => await PollBridge();
        Loaded += OnLoaded;
    }

    public void Shutdown()
    {
        if (_disposed) return;
        _disposed = true;
        _toastTimer.Stop();
        _bridgeTimer.Stop();
        try { Web.Dispose(); } catch { }
    }

    /// <summary>
    /// 打开原生播放器前先把 WebView2 子窗口隐藏，否则它会一直盖在 WPF 播放器上方。
    /// 这里不销毁内核，只收起控件，返回时能立即恢复模块现场。
    /// </summary>
    public void Suspend()
    {
        if (_disposed) return;
        _toastTimer.Stop();
        ToastPanel.Visibility = Visibility.Collapsed;
        Web.Visibility = Visibility.Collapsed;
    }

    public void Resume()
    {
        if (_disposed) return;
        Web.Visibility = Visibility.Visible;
    }

    /// <summary>截图工具兼容入口：Flutter 页面由自身滚动，不在这里复位。</summary>
    public void ResetScroll() { }

    /// <summary>开发期截图用：向 Flutter 画布补发一次鼠标点击。</summary>
    public void DebugTap(double x, double y)
    {
        if (!_initialized) return;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Web.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
  const x = {x.ToString(System.Globalization.CultureInfo.InvariantCulture)};
  const y = {y.ToString(System.Globalization.CultureInfo.InvariantCulture)};
  const el = document.elementFromPoint(x, y) || document.body;
  const box = {{ bubbles: true, cancelable: true, composed: true, clientX: x, clientY: y,
                screenX: x, screenY: y, button: 0, buttons: 1, pointerId: 1, pointerType: 'mouse', isPrimary: true }};
  el.dispatchEvent(new PointerEvent('pointerdown', box));
  el.dispatchEvent(new MouseEvent('mousedown', box));
  el.dispatchEvent(new PointerEvent('pointerup', {{ ...box, buttons: 0 }}));
  el.dispatchEvent(new MouseEvent('mouseup', {{ ...box, buttons: 0 }}));
  el.dispatchEvent(new MouseEvent('click', {{ ...box, buttons: 0 }}));
}})();");
            }
            catch { }
        }));
    }

    public void ShowToast(string text)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text)) return;
        ToastText.Text = text;
        ToastPanel.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 8,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(28) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36 MikoMiko/0.1.35");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
        return client;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        Log("FlutterAnimeView.OnLoaded");
        try
        {
            var dataDir = Path.Combine(SettingsService.CacheDirectory, "webview-flutter");
            Directory.CreateDirectory(dataDir);
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await Web.EnsureCoreWebView2Async(environment);

            var core = Web.CoreWebView2;
            _core = core;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.Settings.IsZoomControlEnabled = false;
            _bridgeTimer.Start();

            var assetRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Anime", "flutter");
            Log("assetRoot=" + assetRoot + " exists=" + Directory.Exists(assetRoot));
            if (!Directory.Exists(assetRoot))
                throw new DirectoryNotFoundException("缺少 Assets/Anime/flutter 追番模块资源。");

            core.SetVirtualHostNameToFolderMapping(VirtualHost, assetRoot,
                CoreWebView2HostResourceAccessKind.Allow);

            core.AddWebResourceRequestedFilter($"{ProxyHost}*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += Web_ResourceRequested;
            core.WebMessageReceived += Web_MessageReceived;
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.NavigationStarting += (_, args) =>
            {
                Log("navigation=" + args.Uri);
                var uri = args.Uri ?? string.Empty;
                if (!uri.StartsWith($"https://{VirtualHost}/", StringComparison.OrdinalIgnoreCase) &&
                    !uri.StartsWith(ProxyHost, StringComparison.OrdinalIgnoreCase) &&
                    !uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase) &&
                    !uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    args.Cancel = true;
            };
            core.NavigationCompleted += (_, args) =>
            {
                Log("nav-completed success=" + args.IsSuccess + " status=" + args.WebErrorStatus);
                if (args.IsSuccess)
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    LoadingDetail.Text = $"模块加载失败：{args.WebErrorStatus}";
                }
            };

            // 追番模块随 EXE 升级，必须避免 WebView2 继续使用旧版 JS 缓存。
            try
            {
                await core.Profile.ClearBrowsingDataAsync(
                    Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.DiskCache |
                    Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.ServiceWorkers);
            }
            catch (Exception ex)
            {
                Log("clear-cache-failed=" + ex.Message);
            }

            core.Navigate($"https://{VirtualHost}/index.html?build={DateTime.Now.Ticks}");
        }
        catch (Exception ex)
        {
            LoadingDetail.Text = "Flutter 模块启动失败：" + ex.Message;
            LoadingDetail.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xC0, 0x50, 0x50));
        }
    }

    private async void Web_ResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = e.Request.Uri;
        if (!uri.StartsWith(ProxyHost, StringComparison.OrdinalIgnoreCase)) return;

        var deferral = e.GetDeferral();
        try
        {
            var parsed = new Uri(uri);
            var query = ParseQuery(parsed.Query);
            var target = query.TryGetValue("url", out var value) ? value : string.Empty;
            if (!Uri.TryCreate(target, UriKind.Absolute, out var targetUri) ||
                (targetUri.Scheme != "http" && targetUri.Scheme != "https"))
            {
                e.Response = TextResponse(Web.CoreWebView2.Environment, 400, "Bad Request", "无效的代理地址。");
                return;
            }

            var referer = query.TryGetValue("referer", out var refererValue) ? refererValue : string.Empty;
            var image = query.TryGetValue("image", out var imageValue) && imageValue == "1";
            if (!image) Log("proxy=" + target);

            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refUri))
                request.Headers.Referrer = refUri;
            request.Headers.TryAddWithoutValidation("Accept",
                image ? "image/avif,image/webp,image/apng,image/*,*/*;q=0.8"
                      : "text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode && status is not 301 and not 302 and not 403 and not 404)
            {
                e.Response = TextResponse(Web.CoreWebView2.Environment, status, response.ReasonPhrase ?? "Bad Gateway",
                    $"站点返回 HTTP {status}。");
                return;
            }

            var contentType = response.Content.Headers.ContentType;
            if (image)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var mime = contentType?.MediaType ?? "image/jpeg";
                e.Response = Web.CoreWebView2.Environment.CreateWebResourceResponse(
                    new MemoryStream(bytes), status, response.ReasonPhrase ?? "OK",
                    BuildHeaders($"Content-Type: {mime}\r\nCache-Control: public, max-age=1800\r\n"));
                return;
            }

            var raw = await response.Content.ReadAsByteArrayAsync();
            var charset = DetectCharset(contentType?.CharSet, raw);
            var text = Decode(raw, charset);
            var bytesOut = Encoding.UTF8.GetBytes(text);
            var mimeOut = contentType?.MediaType ?? "text/html";
            e.Response = Web.CoreWebView2.Environment.CreateWebResourceResponse(
                new MemoryStream(bytesOut), status, response.ReasonPhrase ?? "OK",
                BuildHeaders($"Content-Type: {mimeOut}; charset=utf-8\r\nCache-Control: no-store\r\n"));
        }
        catch (Exception ex)
        {
            try
            {
                e.Response = TextResponse(Web.CoreWebView2.Environment, 502, "Proxy Error",
                    "代理请求失败：" + ex.Message);
            }
            catch { }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static string BuildHeaders(string extra)
        => "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, OPTIONS\r\n" +
           "Access-Control-Allow-Headers: *\r\nX-Miko-Proxy: 1\r\n" + extra;

    private static CoreWebView2WebResourceResponse TextResponse(CoreWebView2Environment environment,
        int status, string reason, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return environment.CreateWebResourceResponse(new MemoryStream(bytes), status, reason,
            BuildHeaders("Content-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\n"));
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = part.IndexOf('=');
            if (index < 0) continue;
            var key = WebUtility.UrlDecode(part[..index]);
            var value = WebUtility.UrlDecode(part[(index + 1)..]);
            if (!string.IsNullOrEmpty(key)) result[key] = value;
        }
        return result;
    }

    private static string DetectCharset(string? headerCharset, byte[] bytes)
    {
        if (!string.IsNullOrWhiteSpace(headerCharset))
        {
            var normalized = headerCharset.Trim('"').ToLowerInvariant();
            if (normalized is "gb2312" or "gbk" or "gb18030") return "gb18030";
            return normalized;
        }
        try
        {
            var probe = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 2048)).ToLowerInvariant();
            if (probe.Contains("charset=gb") || probe.Contains("charset=\"gb")) return "gb18030";
        }
        catch { }
        return "utf-8";
    }

    private static string Decode(byte[] bytes, string charset)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(charset).GetString(bytes);
        }
        catch
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private void Web_MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        HandleMessage(e.WebMessageAsJson);
    }

    private async Task PollBridge()
    {
        if (_bridgePolling || _disposed || _core is null) return;
        _bridgePolling = true;
        try
        {
            var raw = await _core.ExecuteScriptAsync(
                "window.__mikoDrain ? window.__mikoDrain() : '[]'");
            if (string.IsNullOrWhiteSpace(raw)) return;
            // ExecuteScriptAsync 返回的是 JSON 字符串，需要先解一层。
            var payload = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrWhiteSpace(payload) || payload == "[]") return;
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var message = item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : item.GetRawText();
                HandleMessage(message);
            }
        }
        catch
        {
            // 页面切换或脚本尚未就绪时静默重试。
        }
        finally
        {
            _bridgePolling = false;
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : string.Empty;
            switch (type)
            {
                case "play":
                    var pageUrl = Read(root, "pageUrl");
                    if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out _))
                    {
                        ShowToast("播放地址无效。");
                        return;
                    }
                    var anime4K = root.TryGetProperty("anime4k", out var a4kNode) &&
                                  a4kNode.ValueKind == JsonValueKind.True;
                    _playRequested(new AnimeItem
                    {
                        Title = Read(root, "title", "未命名作品"),
                        Url = pageUrl,
                        PlayUrl = pageUrl,
                        Source = Read(root, "source", "自定义规则"),
                        Poster = Read(root, "poster"),
                        Note = Read(root, "episode"),
                        Kind = "动漫",
                    }, anime4K);
                    break;
                case "toast":
                    ShowToast(Read(root, "text"));
                    break;
                case "openBilibili":
                    var url = Read(root, "url");
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var biliUri) ||
                        !(biliUri.Host.EndsWith("bilibili.com", StringComparison.OrdinalIgnoreCase)))
                    {
                        ShowToast("哔哩哔哩地址无效。");
                        return;
                    }
                    _bilibiliRequested(url, Read(root, "title", "哔哩哔哩"));
                    break;
                case "openRuleFolder":
                    OpenRuleFolder();
                    break;
            }
        }
        catch
        {
            // 非法消息忽略，不打断模块。
        }
    }

    private static string Read(JsonElement root, string key, string fallback = "")
    {
        if (!root.TryGetProperty(key, out var node)) return fallback;
        var value = node.ValueKind == JsonValueKind.String ? node.GetString() ?? fallback : fallback;
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private void OpenRuleFolder()
    {
        try
        {
            var dir = Path.Combine(SettingsService.CacheDirectory, "modules", "anime");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowToast("打开规则目录失败：" + ex.Message);
        }
    }
}


