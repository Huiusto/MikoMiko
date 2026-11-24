using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MikoMiko.Views;

/// <summary>软件内网页浏览器：帖内外部链接在这里打开，不弹出系统浏览器。</summary>
public partial class BrowserView : UserControl
{
    private readonly string _url;
    private readonly bool _isUploadPage;
    private bool _initialized;

    public BrowserView(string url, Action? closeRequested = null)
    {
        InitializeComponent();
        _url = url;
        _isUploadPage = url.Contains("newArticle", StringComparison.OrdinalIgnoreCase);
        CloseRequested = closeRequested;
        AddressText.Text = url;
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
            InjectAccountCookies();
            Web.CoreWebView2.NavigationStarting += (_, args) => AddressText.Text = args.Uri;
            Web.CoreWebView2.NavigationCompleted += async (_, _) =>
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                if (_isUploadPage || (Web.Source?.AbsoluteUri.Contains("newArticle", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    try { await Web.CoreWebView2.ExecuteScriptAsync(UploadChromeScript); } catch { }
                }
            };
            Web.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                Web.CoreWebView2.Navigate(args.Uri);
            };
            Web.CoreWebView2.Navigate(_url);
        }
        catch
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            AddressText.Text = "网页组件初始化失败，请在设置中检查 WebView2 运行库";
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
    private void InjectAccountCookies()
    {
        try
        {
            var raw = App.SettingsService.Current.Cookie;
            if (string.IsNullOrWhiteSpace(raw) || Web.CoreWebView2 is null) return;
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
                    cookie.IsHttpOnly = true;
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
    /// 上传页精简脚本：隐藏站点 logo、米游社文字、首页/校园/ACG 等导航和搜索框，
    /// 只保留右上角头像登录；脚本会持续观察 DOM，兼容前端异步渲染。
    /// </summary>
    private const string UploadChromeScript = """
        (() => {
          if (!location.pathname.includes('/newArticle/')) return;
          if (window.__mikoUploadChrome) { window.__mikoUploadChrome(); return; }
          const navText = /^(米游社|首页|校园|ACG|搜索|关注|推荐|消息|发现|下载|创作中心|我的|首页推荐|游戏中心|话题|活动)$/i;
          const hide = el => { if (!el || el.dataset.mikoKeep === '1') return; el.style.setProperty('display','none','important'); };
          const ghost = el => { if (!el || el.dataset.mikoKeep === '1') return; el.style.setProperty('visibility','hidden','important'); };

          function pickAvatar() {
            const nodes = [...document.querySelectorAll('a,button,div,img')];
            const rightEdge = innerWidth * 0.72;
            for (const el of nodes) {
              const r = el.getBoundingClientRect();
              if (r.width < 18 || r.height < 18 || r.width > 90 || r.height > 90) continue;
              if (r.top > 150 || r.right < rightEdge) continue;
              const img = el.tagName === 'IMG' ? el : el.querySelector('img');
              if (!img) continue;
              const ir = img.getBoundingClientRect();
              if (ir.width >= 18 && ir.height >= 18 && ir.top <= 150 && ir.right >= rightEdge) {
                el.dataset.mikoKeep = '1';
                return el;
              }
            }
            return null;
          }

          function apply() {
            const avatar = pickAvatar();
            const topNodes = [...document.querySelectorAll('a,button,span,div,p,input,svg,img')];
            for (const el of topNodes) {
              if (avatar && (el === avatar || el.contains(avatar))) continue;
              const r = el.getBoundingClientRect();
              if (r.width === 0 || r.height === 0) continue;
              const topArea = r.top < 140 && r.height < 150;
              if (el.tagName === 'INPUT') {
                if (topArea || /搜索|search/i.test(el.getAttribute('placeholder') || '')) {
                  let p = el;
                  for (let i = 0; i < 4 && p; i++) p = p.parentElement;
                  hide(p || el);
                }
                continue;
              }
              if (!topArea) continue;
              if (el.children.length === 0) {
                const t = (el.textContent || '').trim();
                if (navText.test(t)) hide(el);
              }
              if (el.tagName === 'IMG' || el.tagName === 'SVG') {
                if (r.left < 260 && r.width < 240 && r.height < 100) ghost(el);
              }
              if ((el.tagName === 'A' || el.tagName === 'BUTTON') && r.left < 260 && r.width < 220 && r.height < 60) {
                const t = (el.textContent || '').trim();
                if (t === '米游社' || navText.test(t) || /米游社/.test(t)) hide(el);
              }
            }

            if (!avatar) return;
            let node = avatar;
            while (node && node.parentElement && node.parentElement !== document.body) {
              const parent = node.parentElement;
              for (const child of [...parent.children]) {
                if (child === node || child.contains(node)) continue;
                if (child.tagName === 'SCRIPT' || child.tagName === 'STYLE') continue;
                const text = (child.textContent || '').trim();
                const hasMedia = !!child.querySelector?.('img,svg,input');
                const cr = child.getBoundingClientRect();
                if (navText.test(text) || /米游社/.test(text) || (hasMedia && cr.top < 150 && cr.height < 150)) {
                  child.style.setProperty('display','none','important');
                }
              }
              node = parent;
              if (node === document.body) break;
            }
            avatar.style.setProperty('visibility','visible','important');
            avatar.style.setProperty('opacity','1','important');
            avatar.style.setProperty('display','flex','important');
          }

          window.__mikoUploadChrome = apply;
          apply();
          new MutationObserver(() => { clearTimeout(window.__mikoChromeTimer); window.__mikoChromeTimer = setTimeout(apply, 120); })
            .observe(document.documentElement, { childList: true, subtree: true });
          addEventListener('resize', apply);
          setInterval(apply, 2500);
        })();
        """;
}
