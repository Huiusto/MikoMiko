using Microsoft.Web.WebView2.Core;
using MikoMiko.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;

namespace MikoMiko.Views;

/// <summary>首页「网页」页签：内置浏览器，默认打开米游社大别野，并提供常用站点快捷入口。</summary>
public partial class WebHubView : UserControl
{
    private const string DefaultUrl = "https://www.miyoushe.com/dby";
    private bool _initialized;

    public WebHubView()
    {
        InitializeComponent();
        OfficialLinks.ItemsSource = new[]
        {
            new WebLink("米哈游", "https://www.mihoyo.com/", "米哈游官方网站"),
            new WebLink("人工桌面", "https://n0va.mihoyo.com/#/", "官方桌面"),
            new WebLink("米游铺", "https://www.mihoyogift.com/", "官方周边商城"),
            new WebLink("米哈游支付中心", "https://pay.mihoyo.com/#/", "充值中心"),
            new WebLink("米哈游招聘", "https://jobs.mihoyo.com/", "官方招聘"),
            new WebLink("米哈游通行证", "https://user.miyoushe.com/index.html#/login?back_route=%252Fhome%252Faccount-overview", "账号中心"),
        };
        ThirdPartyLinks.ItemsSource = new[]
        {
            new WebLink("HOYO-MiX", "https://hoyomix.amarea.cn/", "第三方米哈游音乐平台"),
            new WebLink("原神 EELIE", "https://seelie.me/", "角色养成规划、体力安排"),
            new WebLink("星穹铁道 SEELIE", "https://hsr.seelie.me/", "角色养成规划、体力安排"),
            new WebLink("绝区零 SEELIE", "https://zzz.seelie.me/", "角色养成规划、体力安排"),
            new WebLink("椰羊 cocogoat", "https://cocogoat.work/", "圣遗物管理、成就扫描"),
            new WebLink("KeqingMains", "https://keqingmains.com/", "原神角色强度研究"),
            new WebLink("非小酋·原神", "https://feixiaoqiu.com/app/tools", "抽卡分析"),
            new WebLink("空荧酒馆地图", "https://v3.yuanshen.site/", "原神地图工具"),
            new WebLink("星穹智库", "https://sr.appfeng.com/", "跃迁记录、配队、遗器查询"),
            new WebLink("原神伤害计算", "https://genshincalc.yunlu18.net/", "角色伤害模拟计算"),
            new WebLink("原神中英日词典", "https://genshin-dictionary.com/zh-CN", "多语言名词查询"),
            new WebLink("米哈游游戏库", "https://indienova.com/gamedb/company/mihoyo/p/1", "米哈游所有游戏"),
            new WebLink("原神 B 服请愿", "https://change.org/GenshinImpact", "change.org 玩家请愿"),
        };
    }

    public sealed record WebLink(string Name, string Url, string Note);

    private async void Web_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var dataDir = Path.Combine(SettingsService.CacheDirectory, "webview2-webhub");
            Directory.CreateDirectory(dataDir);
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await Web.EnsureCoreWebView2Async(environment);

            var settings = Web.CoreWebView2.Settings;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;

            InjectAccountCookies();

            Web.CoreWebView2.NavigationStarting += (_, args) =>
            {
                AddressInput.Text = args.Uri;
                LoadingPanel.Visibility = Visibility.Visible;
                LinksPopup.IsOpen = false;
            };
            Web.CoreWebView2.NavigationCompleted += (_, __) =>
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                if (!string.IsNullOrWhiteSpace(Web.CoreWebView2.Source))
                    AddressInput.Text = Web.CoreWebView2.Source;
                UpdateNavigation();
            };
            Web.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                Navigate(args.Uri);
            };

            Navigate(DefaultUrl);
        }
        catch (Exception)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            AddressInput.Text = "网页组件初始化失败，请在设置中检查 WebView2 运行库";
        }
    }

    /// <summary>切换到别的页签或打开阅读器时暂停网页内的音视频，避免后台继续出声。</summary>
    public void PauseMedia()
    {
        try
        {
            _ = Web.CoreWebView2?.ExecuteScriptAsync(
                "document.querySelectorAll('video,audio').forEach(e=>{try{e.pause()}catch(_){}});");
        }
        catch
        {
            // WebView2 尚未初始化时无需处理
        }
    }

    private void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Web.CoreWebView2?.Navigate(url);
        }
        catch
        {
            // 地址非法时保持当前页面
        }
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        var text = AddressInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return;

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            // 不像网址时交给 Bing 搜索，避免把普通关键词当作域名。
            text = text.Contains('.') && !text.Contains(' ')
                ? "https://" + text
                : "https://www.bing.com/search?q=" + Uri.EscapeDataString(text);
        }
        Navigate(text);
    }

    private void AddressInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Go_Click(sender, e);
    }

    private void AddressInput_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        => AddressInput.SelectAll();

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Web.CoreWebView2?.CanGoBack == true) Web.CoreWebView2.GoBack();
        }
        catch
        {
            // 忽略
        }
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Web.CoreWebView2?.CanGoForward == true) Web.CoreWebView2.GoForward();
        }
        catch
        {
            // 忽略
        }
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Web.CoreWebView2?.Reload();
        }
        catch
        {
            // 忽略
        }
    }

    private void Home_Click(object sender, RoutedEventArgs e) => Navigate(DefaultUrl);

    private void Links_Click(object sender, RoutedEventArgs e)
    {
        LinksPopup.IsOpen = !LinksPopup.IsOpen;
    }

    private void Site_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WebLink link }) return;
        LinksPopup.IsOpen = false;
        Navigate(link.Url);
    }

    private void UpdateNavigation()
    {
        try
        {
            if (Web.CoreWebView2 is null) return;
            BackButton.IsEnabled = Web.CoreWebView2.CanGoBack;
            ForwardButton.IsEnabled = Web.CoreWebView2.CanGoForward;
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>把已登录的米游社 Cookie 注入网页，默认页无需重复扫码。</summary>
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
                foreach (var domain in new[] { ".miyoushe.com", "www.miyoushe.com", ".user.miyoushe.com" })
                {
                    var cookie = Web.CoreWebView2.CookieManager.CreateCookie(name, value, domain, "/");
                    cookie.IsHttpOnly = false;
                    cookie.IsSecure = true;
                    Web.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie);
                }
            }
        }
        catch
        {
            // 注入失败不阻断浏览
        }
    }
}
