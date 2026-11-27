using Microsoft.Web.WebView2.Core;
using MikoMiko.Models;
using MikoMiko.Services;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MikoMiko.Views;

/// <summary>
/// 每日任务 1034 验证弹窗：创建极验验证后只显示官方验证器本体，
/// 验证成功拿到 challenge 交给签到接口重试。
/// </summary>
public partial class VerifyDialog : Window
{
    public sealed record VerifyResult(string? Validation, string? Challenge, string? Error);

    private readonly HoyoAccount _account;
    private readonly string? _aigis;
    private readonly SignService.GeetestChallenge _create;
    private bool _initialized;
    private string? _validation;
    private string? _challenge;
    private bool _closing;
    private readonly TaskCompletionSource<VerifyResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private VerifyDialog(HoyoAccount account, string? aigis, SignService.GeetestChallenge create)
    {
        InitializeComponent();
        _account = account;
        _aigis = aigis;
        _create = create;
        _challenge = create.Challenge;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) CloseDialog();
        };
        Closed += (_, _) => _completion.TrySetResult(new VerifyResult(_validation, _challenge, null));
        Loaded += OnLoaded;
    }

    /// <summary>
    /// 先在后台申请极验，再显示窗口；窗口初始透明，验证器渲染完成后才显示，
    /// 避免先弹出白底加载框。返回 getValidate() 原始 JSON 与 challenge。
    /// </summary>
    public static async Task<VerifyResult> ShowAsync(Window? owner, HoyoAccount account, string? aigis = null)
    {
        var (create, error) = await SignService.CreateVerificationAsync(account.Cookie);
        if (create is null)
            return new VerifyResult(null, null, error ?? "申请验证码失败");

        owner ??= Application.Current?.MainWindow;
        var dialog = new VerifyDialog(account, aigis, create)
        {
            // WebView2 在完全透明的窗口里会暂停渲染，极验 onReady 可能永远不触发；
            // 用极低透明度保持合成器工作，验证器排好版后再完全显示。
            Opacity = 1,
        };
        if (owner is not null && owner.IsVisible) dialog.Owner = owner;
        // 不使用模态 ShowDialog：WebView2 在模态窗口内关闭时偶发卡住消息循环。
        dialog.Show();
        return await dialog._completion.Task;
    }

    private void CloseDialog()
    {
        if (_closing) return;
        _closing = true;
        _completion.TrySetResult(new VerifyResult(_validation, _challenge, null));
        try { Hide(); } catch { }
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try { Close(); } catch { }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await LoadCaptchaAsync();
    }

    private async Task LoadCaptchaAsync()
    {
        try
        {
            var dataDir = Path.Combine(SettingsService.CacheDirectory, "webview-verify");
            Directory.CreateDirectory(dataDir);
            if (Web.CoreWebView2 is null)
            {
                var environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
                await Web.EnsureCoreWebView2Async(environment);
                var core = Web.CoreWebView2;
                if (core is null)
                {
                    _completion.TrySetResult(new VerifyResult(null, null, "WebView2 初始化失败"));
                    CloseDialog();
                    return;
                }
                core.WebMessageReceived += OnWebMessageReceived;
                core.NewWindowRequested += (_, args) => args.Handled = true;
            }

            Web.NavigateToString(BuildHtml(_create));
        }
        catch (Exception ex)
        {
            _completion.TrySetResult(new VerifyResult(null, null, "验证码初始化失败：" + ex.Message));
            CloseDialog();
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var (type, data) = ParseWebMessage(e.WebMessageAsJson);
        if (string.IsNullOrWhiteSpace(type)) return;

        switch (type)
        {
            case "ready":
                // 验证器已经排好版，再把整个窗口显示出来。
                Opacity = 1;
                break;

            case "error":
                Opacity = 1;
                break;

            case "size":
                ApplyCaptchaSize(data);
                break;

            case "close":
                CloseDialog();
                break;

            case "validate":
                if (string.IsNullOrWhiteSpace(data)) break;
                _validation = data;
                _completion.TrySetResult(new VerifyResult(_validation, _challenge, null));
                Dispatcher.BeginInvoke(new Action(CloseDialog), System.Windows.Threading.DispatcherPriority.Background);
                break;
        }
    }

    private void ApplyCaptchaSize(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            var w = root.TryGetProperty("width", out var wr) ? wr.GetDouble() : 0;
            var h = root.TryGetProperty("height", out var hr) ? hr.GetDouble() : 0;
            if (w < 260 || h < 260 || w > 720 || h > 900) return;
            Width = w + 2;
            Height = h + 2;
            if (Owner is { IsVisible: true } owner)
            {
                Left = owner.Left + (owner.ActualWidth - Width) / 2;
                Top = owner.Top + (owner.ActualHeight - Height) / 2;
            }
            else
            {
                Left = (SystemParameters.WorkArea.Width - Width) / 2 + SystemParameters.WorkArea.Left;
                Top = (SystemParameters.WorkArea.Height - Height) / 2 + SystemParameters.WorkArea.Top;
            }
        }
        catch { }
    }

    /// <summary>
    /// 兼容两种 WebView2 消息：宿主以 JSON 对象 postMessage，或以 JSON 字符串 postMessage。
    /// </summary>
    private static (string? Type, string? Data) ParseWebMessage(string raw)
    {
        for (var depth = 0; depth < 2; depth++)
        {
            if (string.IsNullOrWhiteSpace(raw)) return (null, null);
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.String)
                {
                    raw = root.GetString() ?? "";
                    continue;
                }
                if (root.ValueKind != JsonValueKind.Object) return (null, null);

                string? type = null;
                string? data = null;
                if (root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
                    type = t.GetString();
                if (root.TryGetProperty("data", out var d))
                    data = d.ValueKind == JsonValueKind.String ? d.GetString() : d.GetRawText();
                return (type, data);
            }
            catch
            {
                return (null, null);
            }
        }
        return (null, null);
    }

    private string BuildHtml(SignService.GeetestChallenge create)
    {
        var sessionId = ReadSessionId(_aigis);
        var payload = JsonSerializer.Serialize(new
        {
            gt = create.Gt,
            challenge = create.Challenge,
            riskType = string.IsNullOrWhiteSpace(create.RiskType) ? "slide" : create.RiskType,
            useV4 = create.UseV4,
            sessionId,
        });

        try
        {
            var uri = new Uri("pack://application:,,,/Assets/Verify/geetest.html");
            var info = Application.GetResourceStream(uri);
            if (info is not null)
            {
                using var reader = new StreamReader(info.Stream);
                return reader.ReadToEnd().Replace("__DATA__", payload);
            }
        }
        catch
        {
            // 资源读取失败时走下方提示页。
        }

        return "<html><body style=\"font-family:sans-serif;padding:24px;color:#b2564b\">" +
               "验证码页面资源缺失，请重新安装软件。</body></html>";
    }

    private static string ReadSessionId(string? aigis)
    {
        if (string.IsNullOrWhiteSpace(aigis)) return "";
        try
        {
            using var doc = JsonDocument.Parse(aigis);
            var root = doc.RootElement;
            if (root.TryGetProperty("session_id", out var s) && s.ValueKind == JsonValueKind.String)
                return s.GetString() ?? "";
            if (root.TryGetProperty("sessionId", out var s2) && s2.ValueKind == JsonValueKind.String)
                return s2.GetString() ?? "";
        }
        catch
        {
            // aigis 不是 JSON 时忽略。
        }
        return "";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => CloseDialog();
}
