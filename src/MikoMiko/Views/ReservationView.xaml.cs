using Microsoft.Web.WebView2.Core;
using MikoMiko.Services;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MikoMiko.Views;

/// <summary>
/// 启动页「预约游戏」卡片对应的软件内预约页：读取官方启动器同一份
/// 预约内容数据，按官方启动器版式展示视频、简介、标签与预约按钮。
/// </summary>
public partial class ReservationView : UserControl
{
    private readonly string _gameKey;
    private bool _initialized;

    public ReservationView(string gameKey, Action? closeRequested = null, Action<string>? reserveRequested = null)
    {
        InitializeComponent();
        _gameKey = gameKey;
        CloseRequested = closeRequested;
        ReserveRequested = reserveRequested;
        Web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xF3, 0xF7, 0xFB);
    }

    /// <summary>关闭预约页时先暂停并卸载视频，再销毁 WebView2，避免声音在页面关掉后继续播放。</summary>
    public void CloseAndDispose()
    {
        try
        {
            if (Web.CoreWebView2 is not null)
            {
                Web.CoreWebView2.ExecuteScriptAsync("window.stopMedia && window.stopMedia();");
            }
        }
        catch
        {
        }
        try
        {
            Web.Visibility = Visibility.Collapsed;
            // 消息回调里同步 Dispose 可能和 WebView2 自己的事件分发重入，放到下一个 UI 周期销毁。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { Web.Dispose(); } catch { }
            }));
        }
        catch
        {
        }
    }

    public Action? CloseRequested { get; set; }

    public Action<string>? ReserveRequested { get; set; }

    private async void Web_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var dataDir = Path.Combine(SettingsService.CacheDirectory, "webview");
            Directory.CreateDirectory(dataDir);
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await Web.EnsureCoreWebView2Async(environment);
            Web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            Web.CoreWebView2.NavigationCompleted += (_, _) => { };

            var content = await GameReservationService.LoadAsync(_gameKey);
            var html = BuildHtml(content);
            Web.CoreWebView2.NavigateToString(html);
        }
        catch
        {
            // WebView2 不可用时至少保留可读的兜底提示，避免出现空白页。
            if (GameReservationService.TryGetOfficialUrl(_gameKey, out var url))
            {
                CloseRequested?.Invoke();
                ReserveRequested?.Invoke(url);
            }
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeElement)) return;
            var type = typeElement.GetString();
            if (type == "close")
            {
                CloseRequested?.Invoke();
            }
            else if (type == "reserve")
            {
                var url = root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(url) &&
                    GameReservationService.TryGetOfficialUrl(_gameKey, out var official))
                {
                    url = official;
                }
                if (!string.IsNullOrWhiteSpace(url)) ReserveRequested?.Invoke(url);
            }
        }
        catch
        {
        }
    }

    private static string BuildHtml(GameReservationService.Content content)
    {
        var template = ReadTemplate();
        var data = new
        {
            name = content.Name,
            logo = content.Logo,
            introduction = content.Introduction,
            tags = content.Tags,
            medias = content.Medias.Select(m => new { type = m.Type.Contains("VIDEO", StringComparison.OrdinalIgnoreCase) ? "video" : "image", cover = m.Cover, resource = m.Resource }),
            link = content.OfficialUrl,
        };
        var json = JsonSerializer.Serialize(data);
        return template.Replace("__DATA__", json);
    }

    private static string ReadTemplate()
    {
        var uri = new Uri("pack://application:,,,/Assets/Launch/reservation.html");
        using var stream = Application.GetResourceStream(uri)?.Stream
            ?? throw new InvalidOperationException("reservation.html not found");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
