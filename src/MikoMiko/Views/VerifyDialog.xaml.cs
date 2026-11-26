using Microsoft.Web.WebView2.Core;
using MikoMiko.Models;
using MikoMiko.Services;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MikoMiko.Views;

/// <summary>
/// 每日任务 1034 验证弹窗：在软件内嵌 WebView2 打开米游社/通行证验证页面，
/// 用户完成滑块或短信验证后直接关闭并重试原任务，不再要求跳到手机 App。
/// </summary>
public partial class VerifyDialog : Window
{
    private readonly HoyoAccount _account;
    private readonly string _url;
    private bool _initialized;

    public VerifyDialog(HoyoAccount account, string? aigis)
    {
        InitializeComponent();
        _account = account;
        _url = BuildVerifyUrl(aigis);
        TxtAccount.Text = $"账号 {account.DisplayName} 触发了米游社风险验证，请在下方完成验证";
        Loaded += OnLoaded;
    }

    public static bool Show(Window? owner, HoyoAccount account, string? aigis = null)
    {
        owner ??= Application.Current?.MainWindow;
        var dialog = new VerifyDialog(account, aigis);
        if (owner is not null && owner.IsVisible) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var dataDir = Path.Combine(SettingsService.CacheDirectory, "webview-verify");
            Directory.CreateDirectory(dataDir);
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await Web.EnsureCoreWebView2Async(environment);
            InjectCookies();
            Web.CoreWebView2.NavigationCompleted += (_, _) => LoadingPanel.Visibility = Visibility.Collapsed;
            Web.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                Web.CoreWebView2.Navigate(args.Uri);
            };
            Web.CoreWebView2.Navigate(_url);
        }
        catch (Exception ex)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            TxtAccount.Text = "验证页面初始化失败：" + ex.Message;
        }
    }

    private void InjectCookies()
    {
        var raw = _account.Cookie;
        if (string.IsNullOrWhiteSpace(raw) || Web.CoreWebView2 is null) return;

        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = part.IndexOf('=');
            if (split <= 0) continue;
            var name = part[..split].Trim();
            var value = part[(split + 1)..].Trim();
            if (name.Length == 0 || value.Length == 0) continue;
            foreach (var domain in new[]
                     {
                         ".miyoushe.com", "www.miyoushe.com", ".user.miyoushe.com",
                         ".mihoyo.com", "user.mihoyo.com", ".webstatic.mihoyo.com",
                     })
            {
                try
                {
                    var cookie = Web.CoreWebView2.CookieManager.CreateCookie(name, value, domain, "/");
                    cookie.IsHttpOnly = false;
                    cookie.IsSecure = true;
                    Web.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie);
                }
                catch
                {
                    // 单个域写入失败不影响其他域。
                }
            }
        }
    }

    private static string BuildVerifyUrl(string? aigis)
    {
        if (!string.IsNullOrWhiteSpace(aigis))
        {
            try
            {
                using var doc = JsonDocument.Parse(aigis);
                var root = doc.RootElement;
                var gt = root.TryGetProperty("gt", out var g) ? g.GetString() : null;
                var challenge = root.TryGetProperty("challenge", out var c) ? c.GetString() : null;
                if (!string.IsNullOrWhiteSpace(gt) && !string.IsNullOrWhiteSpace(challenge))
                {
                    return "https://webstatic.mihoyo.com/bbs/event/e2021/verify/index.html" +
                           $"?gt={Uri.EscapeDataString(gt)}&challenge={Uri.EscapeDataString(challenge)}&lang=zh-cn";
                }
            }
            catch
            {
                // 非 JSON 或字段缺失时走米游社验证中心。
            }
        }
        return "https://user.mihoyo.com/";
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
