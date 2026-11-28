using Microsoft.Web.WebView2.Core;
using MikoMiko.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MikoMiko.Views;

/// <summary>软件内网页浏览器：帖内外部链接在这里打开，不弹出系统浏览器。</summary>
public partial class BrowserView : UserControl
{
    private readonly string _url;
    private readonly bool _isUploadPage;
    private readonly bool _isPlayer;
    private bool _initialized;

    public BrowserView(string url, Action? closeRequested = null, bool immersivePlayer = false)
    {
        InitializeComponent();
        _url = url;
        _isUploadPage = url.Contains("newArticle", StringComparison.OrdinalIgnoreCase);
        _isPlayer = immersivePlayer;
        CloseRequested = closeRequested;
    }

    public Action? CloseRequested { get; set; }

    private async void Web_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var dataDir = Path.Combine(Services.SettingsService.CacheDirectory, "webview");
            Directory.CreateDirectory(dataDir);
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await Web.EnsureCoreWebView2Async(environment);
            await InjectAccountCookiesAsync();
            Web.CoreWebView2.NavigationCompleted += async (_, args) =>
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                if (_isPlayer)
                {
                    try { await Web.CoreWebView2.ExecuteScriptAsync(PlayerHelperScript); } catch { }
                }
            };
            Web.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                Web.CoreWebView2.Navigate(args.Uri);
            };
            Web.CoreWebView2.Navigate(_url);
        }
        catch (Exception)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Web.CoreWebView2?.CanGoBack == true) Web.CoreWebView2.GoBack();
            else CloseRequested?.Invoke();
        }
        catch
        {
            CloseRequested?.Invoke();
        }
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Web.CoreWebView2 is not null) Web.CoreWebView2.Reload();
        }
        catch
        {
            // 忽略
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    /// <summary>把软件内已登录的米游社 Cookie 注入 WebView2，发布页无需重复扫码。</summary>
    private async Task InjectAccountCookiesAsync()
    {
        try
        {
            var raw = App.SettingsService.Current.Cookie;
            if (string.IsNullOrWhiteSpace(raw) || Web.CoreWebView2 is null) return;

            // 发布网页依赖 cookie_token；旧登录态缺失时先用 stoken 补换一次网页 Cookie。
            if (_isUploadPage &&
                !raw.Contains("cookie_token", StringComparison.OrdinalIgnoreCase) &&
                raw.Contains("stoken=", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var refreshed = await new MiHoYoLoginService().RefreshWebCookieAsync(raw);
                    if (!string.IsNullOrWhiteSpace(refreshed))
                    {
                        App.SettingsService.Current.Cookie = refreshed;
                        App.SettingsService.Save();
                        raw = refreshed;
                    }
                }
                catch
                {
                    // 换取失败时继续用原 Cookie，网页会进入自己的登录引导。
                }
            }

            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var split = part.IndexOf('=');
                if (split <= 0) continue;
                var name = part[..split].Trim();
                var value = part[(split + 1)..].Trim();
                if (name.Length == 0 || value.Length == 0) continue;
                // 只同步米游社站点可用的登录态，避免把 Cookie 交给无关域。
                foreach (var domain in new[] { ".miyoushe.com", "www.miyoushe.com", ".user.miyoushe.com" })
                {
                    var cookie = Web.CoreWebView2.CookieManager.CreateCookie(name, value, domain, "/");
                    // 米游社网页端 SDK 需要读取 ltuid / account_id 等字段，因此不能全部设为 HttpOnly。
                    cookie.IsHttpOnly = false;
                    cookie.IsSecure = true;
                    Web.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie);
                }
            }
        }
        catch
        {
            // Cookie 注入失败不阻断浏览；用户仍可用右上角头像登录。
        }
    }

    /// <summary>
    /// 站内播放辅助：关闭常见的浮层广告与下载提示，保证视频区域可获得焦点；
    /// 只对明显的广告容器生效，不隐藏站点正文或播放器。
    /// </summary>
    private const string PlayerHelperScript = """
        (() => {
          if (window.__mikoPlayerHelper) { window.__mikoPlayerHelper(); return; }
          const hide = el => { if (!el || el.dataset.mikoKeep === '1') return; el.style.setProperty('display','none','important'); };
          const AD = /(^|[-_\s])(ad|ads|advert|gg|popup|pop|downloadapp|download-app)([-_\s]|$)/i;
          function clean() {
            for (const el of document.querySelectorAll('div,section,aside,iframe')) {
              const idc = ((el.id || '') + ' ' + (el.className || '').toString());
              if (!AD.test(idc)) continue;
              const r = el.getBoundingClientRect();
              if (r.width < 80 || r.height < 40) continue;
              if (el.querySelector('video')) continue;
              hide(el);
            }
            for (const v of document.querySelectorAll('video')) {
              v.controls = true;
              v.style.setProperty('max-width','100%','important');
            }
          }
          window.__mikoPlayerHelper = clean;
          clean();
          new MutationObserver(() => { clearTimeout(window.__mikoPlayerTimer); window.__mikoPlayerTimer = setTimeout(clean, 200); })
            .observe(document.documentElement, { childList: true, subtree: true });
          setInterval(clean, 2500);
        })();
        """;
}
