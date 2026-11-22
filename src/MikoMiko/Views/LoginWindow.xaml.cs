using Microsoft.Web.WebView2.Core;
using MikoMiko.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MikoMiko.Views;

/// <summary>
/// 米游社扫码登录：内嵌 WebView2 打开官方登录页，扫码成功后读取站点 Cookie。
/// （米游社的 createQRLogin 接口需要额外签名参数，直接内嵌官方页面最可靠。）
/// </summary>
public partial class LoginWindow : Window
{
    private static readonly string[] CookieHosts =
    {
        "https://user.miyoushe.com/",
        "https://www.miyoushe.com/",
        "https://app.miyoushe.com/",
        "https://bbs-api.miyoushe.com/",
        "https://passport-api.miyoushe.com/",
        "https://miyoushe.com/",
    };

    private readonly DispatcherTimer _poll;
    private bool _checking;
    private bool _completed;

    public LoginWindow()
    {
        InitializeComponent();
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _poll.Tick += async (_, _) => await CheckCookieAsync();
        Loaded += async (_, _) => await InitializeAsync();
        Closed += (_, _) => _poll.Stop();
    }

    /// <summary>登录成功回调，参数为可直接用于接口请求的 Cookie 头。</summary>
    public Action<string>? LoggedIn { get; set; }

    private async Task InitializeAsync()
    {
        try
        {
            // 独立的持久化用户目录：登录一次后，下次打开仍保留米游社登录态
            var dataDir = System.IO.Path.Combine(SettingsService.DataDirectory, "webview");
            System.IO.Directory.CreateDirectory(dataDir);
            var env = await CoreWebView2Environment.CreateAsync(null, dataDir);

            await Web.EnsureCoreWebView2Async(env);
            Web.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                LoadingMask.Visibility = Visibility.Collapsed;
                _poll.Start();
            };
            Web.CoreWebView2.NavigationStarting += (_, _) =>
            {
                LoadingMask.Visibility = Visibility.Visible;
            };
            Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Web.Source = new Uri("https://user.miyoushe.com/");
        }
        catch (Exception ex)
        {
            LoadingMask.Visibility = Visibility.Visible;
            StatusText.Text = "无法启动内置浏览器：" + ex.Message;
        }
    }

    private async Task CheckCookieAsync()
    {
        if (_checking || _completed || Web.CoreWebView2 is null) return;
        _checking = true;
        try
        {
            var cookie = await ReadCookieHeaderAsync();
            if (string.IsNullOrWhiteSpace(cookie)) return;

            // 新版米游社 Cookie 是 ltoken_v2 / cookie_token_v2 / ltuid_v2 / account_id_v2，
            // 旧版是 ltoken / cookie_token / ltuid，两种都要认。
            var hasToken = cookie.Contains("ltoken", StringComparison.OrdinalIgnoreCase)
                           || cookie.Contains("cookie_token", StringComparison.OrdinalIgnoreCase);
            var hasUid = cookie.Contains("ltuid", StringComparison.OrdinalIgnoreCase)
                         || cookie.Contains("account_id", StringComparison.OrdinalIgnoreCase);
            if (!hasToken || !hasUid) return;

            StatusText.Text = "已获取登录信息，正在验证账号…";
            var api = new MiyousheApi(cookie);
            var info = await api.GetUserInfoAsync(2);
            if (!info.Ok)
            {
                StatusText.Text = "登录信息不完整，请确认已扫码并在手机上确认登录";
                return;
            }

            _completed = true;
            _poll.Stop();
            StatusText.Text = $"登录成功：{info.Nickname}";
            LoggedIn?.Invoke(cookie);

            await Task.Delay(700);
            Close();
        }
        catch
        {
            // 页面未就绪时忽略，下个周期再试
        }
        finally
        {
            _checking = false;
        }
    }

    private async Task<string> ReadCookieHeaderAsync()
    {
        var pairs = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var host in CookieHosts)
        {
            IReadOnlyList<CoreWebView2Cookie> cookies;
            try
            {
                cookies = await Web.CoreWebView2.CookieManager.GetCookiesAsync(host);
            }
            catch
            {
                continue;
            }

            foreach (var cookie in cookies)
            {
                if (!names.Add(cookie.Name)) continue;
                pairs.Add($"{cookie.Name}={cookie.Value}");
            }
        }

        return string.Join("; ", pairs);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
