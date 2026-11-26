using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MikoMiko.Services;
using QRCoder;

namespace MikoMiko.Views;

/// <summary>添加米游社账号：扫码 / Cookie / 从 MikoNest 导入。</summary>
public partial class AddAccountWindow : Window
{
    private DispatcherTimer? _qrTimer;
    private string? _qrTicket;
    private int _qr502Count;
    private string _tab = "qr";

    public AddAccountWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BeginQr();
        Closed += (_, _) => StopQrTimer();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnTabQr_Click(object sender, RoutedEventArgs e) => SetTab("qr");
    private void BtnTabCookie_Click(object sender, RoutedEventArgs e) => SetTab("cookie");
    private void BtnTabImport_Click(object sender, RoutedEventArgs e) => SetTab("import");

    private void SetTab(string tab)
    {
        _tab = tab;
        PanelQr.Visibility = tab == "qr" ? Visibility.Visible : Visibility.Collapsed;
        PanelCookie.Visibility = tab == "cookie" ? Visibility.Visible : Visibility.Collapsed;
        PanelImport.Visibility = tab == "import" ? Visibility.Visible : Visibility.Collapsed;

        foreach (var (button, key) in new[]
                 {
                     (BtnTabQr, "qr"), (BtnTabCookie, "cookie"), (BtnTabImport, "import"),
                 })
        {
            var selected = key == tab;
            button.Background = new SolidColorBrush(selected
                ? Color.FromRgb(0xE9, 0xF6, 0xFE)
                : Color.FromRgb(0xF3, 0xF7, 0xFA));
            button.Foreground = new SolidColorBrush(selected
                ? Color.FromRgb(0x25, 0x87, 0xC8)
                : Color.FromRgb(0x68, 0x7B, 0x8D));
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
        if (tab != "qr") StopQrTimer();
        if (tab == "qr" && _qrTicket is null) BeginQr();
    }

    // ---------- 扫码 ----------
    private async void BeginQr()
    {
        StopQrTimer();
        _qr502Count = 0;
        ImgQr.Source = null;
        TxtQrPlaceholder.Visibility = Visibility.Visible;
        TxtQrPlaceholder.Text = "正在生成二维码…";
        TxtQrStatus.Text = "正在向米游社申请二维码…";
        BtnQrStart.IsEnabled = false;
        try
        {
            var (ok, message, payload, ticket) = await HoyoAccountService.CreateQrAsync();
            if (!ok || string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(ticket))
            {
                TxtQrPlaceholder.Text = "二维码生成失败";
                TxtQrStatus.Text = message;
                return;
            }
            _qrTicket = ticket;
            ImgQr.Source = QrToBitmapSource(payload);
            TxtQrPlaceholder.Visibility = Visibility.Collapsed;
            TxtQrStatus.Text = "使用「米游社」App 扫码，并在手机上确认登录";
            BtnQrStart.Content = "刷新二维码";
            _qrTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _qrTimer.Tick += async (_, _) => await PollQrAsync();
            _qrTimer.Start();
        }
        finally
        {
            BtnQrStart.IsEnabled = true;
        }
    }

    private async Task PollQrAsync()
    {
        if (_qrTicket is null) return;
        var (ok, message, cookie) = await HoyoAccountService.PollQrAsync(_qrTicket);
        if (!ok)
        {
            if (message.Contains("-502"))
            {
                _qr502Count++;
                TxtQrStatus.Text = _qr502Count < 10
                    ? $"服务繁忙（-502），正在重试（{_qr502Count}）…"
                    : "米游社风控较严格，请改用 Cookie 登录，或稍后再扫码。";
                if (_qr502Count >= 10)
                {
                    StopQrTimer();
                    _qrTicket = null;
                    ImgQr.Source = null;
                    TxtQrPlaceholder.Visibility = Visibility.Visible;
                    TxtQrPlaceholder.Text = "扫码暂不可用";
                    BtnQrStart.Content = "重新生成";
                }
                return;
            }
            if (message.Contains("等待") || message.Contains("已扫码"))
            {
                TxtQrStatus.Text = message;
                return;
            }
            StopQrTimer();
            _qrTicket = null;
            TxtQrStatus.Text = message;
            return;
        }

        StopQrTimer();
        _qrTicket = null;
        if (string.IsNullOrEmpty(cookie))
        {
            TxtQrStatus.Text = "登录成功但没有获取到 Cookie，请重试。";
            return;
        }
        await FinishAddAsync(cookie, TxtQrStatus);
    }

    private void BtnQrStart_Click(object sender, RoutedEventArgs e) => BeginQr();

    private void BtnQrCancel_Click(object sender, RoutedEventArgs e)
    {
        StopQrTimer();
        _qrTicket = null;
        ImgQr.Source = null;
        TxtQrPlaceholder.Visibility = Visibility.Visible;
        TxtQrPlaceholder.Text = "点击下方按钮生成二维码";
        TxtQrStatus.Text = "已取消扫码。可以刷新重新生成，或切换到 Cookie 登录。";
        BtnQrStart.Content = "生成二维码";
    }

    private void StopQrTimer()
    {
        _qrTimer?.Stop();
        _qrTimer = null;
    }

    private static BitmapSource QrToBitmapSource(string payload)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qr = new QRCode(data);
        using var bitmap = qr.GetGraphic(8);
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    // ---------- Cookie ----------
    private async void BtnCookieAdd_Click(object sender, RoutedEventArgs e)
    {
        var cookie = TxtCookie.Text.Trim();
        if (string.IsNullOrEmpty(cookie))
        {
            TxtCookieStatus.Text = "请先粘贴 Cookie。";
            TxtCookieStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C));
            return;
        }
        BtnCookieAdd.IsEnabled = false;
        TxtCookieStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x71, 0x84, 0x96));
        TxtCookieStatus.Text = "正在校验登录态…";
        try
        {
            var (ok, message, nickname, uid, _) = await HoyoAccountService.AddByCookieAsync(cookie);
            if (!ok)
            {
                TxtCookieStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C));
                TxtCookieStatus.Text = message;
                return;
            }
            TxtCookieStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x8C, 0x57));
            TxtCookieStatus.Text = $"已添加 {nickname}（{uid}）。";
            await Task.Delay(600);
            DialogResult = true;
        }
        finally
        {
            BtnCookieAdd.IsEnabled = true;
        }
    }

    // ---------- 导入 ----------
    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var added = HoyoAccountService.ImportFromMikoNest();
        if (added > 0)
        {
            TxtImportStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x8C, 0x57));
            TxtImportStatus.Text = $"已导入 {added} 个账号，可以直接关闭窗口。";
            DialogResult = true;
        }
        else
        {
            TxtImportStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x71, 0x84, 0x96));
            TxtImportStatus.Text = "没有发现新的可导入账号；相同 UID 不会重复添加。";
        }
    }

    private async Task FinishAddAsync(string cookie, System.Windows.Controls.TextBlock status)
    {
        status.Foreground = new SolidColorBrush(Color.FromRgb(0x71, 0x84, 0x96));
        status.Text = "登录成功，正在读取账号信息…";
        var (ok, message, nickname, uid, _) = await HoyoAccountService.AddByCookieAsync(cookie);
        if (!ok)
        {
            status.Foreground = new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C));
            status.Text = message;
            return;
        }
        status.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x8C, 0x57));
        status.Text = $"已添加 {nickname}（{uid}）";
        await Task.Delay(600);
        DialogResult = true;
    }
}
