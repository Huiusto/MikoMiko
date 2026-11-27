using Microsoft.Web.WebView2.Core;
using MikoMiko.Models;
using MikoMiko.Services;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MikoMiko.Views;

/// <summary>
/// 每日任务 1034 验证弹窗：参考 TeyvatGuide 实用脚本页的极验流程，
/// 在软件内直接创建极验验证并显示滑块，验证成功后拿到 challenge 交给签到接口重试。
/// </summary>
public partial class VerifyDialog : Window
{
    private readonly HoyoAccount _account;
    private readonly string? _aigis;
    private bool _initialized;
    private string? _validation;
    private string? _challenge;
    private bool _closing;
    private readonly TaskCompletionSource<(string? Validation, string? Challenge)> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public VerifyDialog(HoyoAccount account, string? aigis)
    {
        InitializeComponent();
        _account = account;
        _aigis = aigis;
        TxtAccount.Text = $"账号 {account.DisplayName} 触发了米游社风险验证，验证码已直接在软件内载入";
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) CloseDialog();
        };
        Closed += (_, _) => _completion.TrySetResult((_validation, _challenge));
        Loaded += OnLoaded;
    }

    /// <summary>返回极验 getValidate() 的原始 JSON 与创建时的 challenge，由调用方在弹窗关闭后提交。</summary>
    public static Task<(string? Validation, string? Challenge)> ShowAsync(Window? owner, HoyoAccount account, string? aigis = null)
    {
        owner ??= Application.Current?.MainWindow;
        var dialog = new VerifyDialog(account, aigis);
        if (owner is not null && owner.IsVisible) dialog.Owner = owner;
        // 不使用模态 ShowDialog：WebView2 在模态窗口内关闭时偶发卡住消息循环，
        // 改为 Show + TaskCompletionSource，关闭后立即把结果交回任务页。
        dialog.Show();
        return dialog._completion.Task;
    }

    private void CloseDialog()
    {
        if (_closing) return;
        _closing = true;
        // 先隐藏再关闭，避免极验成功提示层盖住关闭动画、看起来像「卡住」。
        try { Hide(); } catch { }
        Close();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await ReloadChallengeAsync();
    }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseDialog();

    private async Task ReloadChallengeAsync()
    {
        LoadingPanel.Visibility = Visibility.Visible;
        TxtLoading.Text = "正在向米游社申请极验验证…";
        try
        {
            var (create, error) = await SignService.CreateVerificationAsync(_account.Cookie);
            if (create is null)
            {
                TxtLoading.Text = "申请验证码失败：" + (error ?? "未知错误");
                return;
            }
            _challenge = create.Challenge;

            var dataDir = Path.Combine(SettingsService.CacheDirectory, "webview-verify");
            Directory.CreateDirectory(dataDir);
            if (Web.CoreWebView2 is null)
            {
                var environment = await CoreWebView2Environment.CreateAsync(null, dataDir);
                await Web.EnsureCoreWebView2Async(environment);
                var core = Web.CoreWebView2;
                if (core is null) throw new InvalidOperationException("WebView2 初始化失败");
                core.WebMessageReceived += OnWebMessageReceived;
                core.NewWindowRequested += (_, args) => args.Handled = true;
            }

            var html = BuildHtml(create);
            LoadingPanel.Visibility = Visibility.Collapsed;
            Web.NavigateToString(html);
        }
        catch (Exception ex)
        {
            TxtLoading.Text = "验证码初始化失败：" + ex.Message;
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? type = null;
        string? data = null;
        try
        {
            var raw = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var t)) type = t.GetString();
            if (root.TryGetProperty("data", out var d))
                data = d.ValueKind == JsonValueKind.String ? d.GetString() : d.GetRawText();
        }
        catch
        {
            return;
        }

        switch (type)
        {
            case "ready":
                LoadingPanel.Visibility = Visibility.Collapsed;
                break;

            case "close":
                CloseDialog();
                break;

            case "validate":
                if (string.IsNullOrWhiteSpace(data)) break;
                _validation = data;
                // 拿到极验结果后立刻隐藏并关闭弹窗，后续校验与重试交回任务页。
                Dispatcher.BeginInvoke(new Action(CloseDialog), System.Windows.Threading.DispatcherPriority.Background);
                break;
        }
    }

    private string BuildHtml(SignService.GeetestChallenge create)
    {
        var sessionId = ReadSessionId(_aigis);
        var payload = JsonSerializer.Serialize(new
        {
            gt = create.Gt,
            challenge = create.Challenge,
            riskType = create.RiskType ?? "slide",
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
            // aigis 不是 JSON 时忽略，v4 用空 session_id 也能继续。
        }
        return "";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => CloseDialog();
}
