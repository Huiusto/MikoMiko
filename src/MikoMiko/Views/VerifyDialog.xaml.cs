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
    private bool _submitting;
    private string? _challenge;

    public VerifyDialog(HoyoAccount account, string? aigis)
    {
        InitializeComponent();
        _account = account;
        _aigis = aigis;
        TxtAccount.Text = $"账号 {account.DisplayName} 触发了米游社风险验证，验证码已直接在软件内载入";
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) DialogResult = false;
        };
        Loaded += OnLoaded;
    }

    public static Task<string?> ShowAsync(Window? owner, HoyoAccount account, string? aigis = null)
    {
        owner ??= Application.Current?.MainWindow;
        var dialog = new VerifyDialog(account, aigis);
        if (owner is not null && owner.IsVisible) dialog.Owner = owner;
        dialog.ShowDialog();
        return Task.FromResult(dialog._challenge);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await ReloadChallengeAsync();
    }

    private async Task ReloadChallengeAsync()
    {
        StatusPill.Visibility = Visibility.Collapsed;
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

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                DialogResult = false;
                break;

            case "validate":
                if (string.IsNullOrWhiteSpace(data) || _submitting) break;
                _submitting = true;
                // 0.1.50：成功后不再用整页白色 Loading 覆盖极验，避免「验证成功」画面定格。
                TxtStatus.Text = "验证成功，正在完成验证…";
                StatusPill.Visibility = Visibility.Visible;
                try
                {
                    var verifyTask = SignService.VerifyCaptchaAsync(_account.Cookie, data);
                    var completed = await Task.WhenAny(verifyTask, Task.Delay(TimeSpan.FromSeconds(20)));
                    if (completed != verifyTask)
                    {
                        TxtStatus.Text = "验证结果提交超时，请按 Esc 关闭后重试";
                        await Task.Delay(1200);
                        _submitting = false;
                        await ReloadChallengeAsync();
                        break;
                    }

                    var (challenge, error) = await verifyTask;
                    if (!string.IsNullOrWhiteSpace(challenge))
                    {
                        TxtStatus.Text = "验证通过，正在返回任务页…";
                        _challenge = challenge;
                        DialogResult = true;
                        break;
                    }

                    // 验证未通过时短暂显示原因，再刷新一张新的验证码。
                    TxtStatus.Text = error ?? "验证未通过，正在刷新验证码…";
                    StatusPill.Visibility = Visibility.Collapsed;
                    _submitting = false;
                    await ReloadChallengeAsync();
                }
                catch (Exception ex)
                {
                    TxtStatus.Text = "验证结果提交失败：" + ex.Message;
                    await Task.Delay(1200);
                    _submitting = false;
                    await ReloadChallengeAsync();
                }
                finally
                {
                    _submitting = false;
                }
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

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
