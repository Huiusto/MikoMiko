using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MikoMiko.Views;

/// <summary>软件内网页浏览器：帖内外部链接在这里打开，不弹出系统浏览器。</summary>
public partial class BrowserView : UserControl
{
    private readonly string _url;
    private bool _initialized;

    public BrowserView(string url, Action? closeRequested = null)
    {
        InitializeComponent();
        _url = url;
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
            Web.CoreWebView2.NavigationStarting += (_, args) => AddressText.Text = args.Uri;
            Web.CoreWebView2.NavigationCompleted += (_, _) => LoadingPanel.Visibility = Visibility.Collapsed;
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
}
