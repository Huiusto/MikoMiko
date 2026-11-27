using MikoMiko.Models;
using MikoMiko.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace MikoMiko.Views;

public partial class UpdateWindow : Window
{
    private readonly UpdateService.UpdateInfo _info;
    private readonly AppSettings _settings;

    public UpdateWindow(UpdateService.UpdateInfo info, AppSettings settings)
    {
        InitializeComponent();
        _info = info;
        _settings = settings;
        VersionText.Text = $"当前版本 {AppInfo.Version}  →  最新版本 {info.Tag}";
        NotesText.Text = string.IsNullOrWhiteSpace(info.Notes) ? "该版本未提供更新说明。" : info.Notes;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // 关闭窗口不等于忽略该版本，下次启动仍会提示。
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // 用户明确取消：记住该版本，启动时不再弹出
        _settings.IgnoredUpdateVersion = _info.Tag;
        Close();
    }

    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_info.PageUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_info.PageUrl) { UseShellExecute = true });
        }
        catch
        {
            StatusText.Text = "无法打开 Release 页面";
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_info.AssetUrl))
        {
            OpenRelease_Click(sender, e);
            return;
        }

        InstallButton.IsEnabled = false;
        InstallButton.Content = "正在下载…";
        DownloadProgress.Visibility = Visibility.Visible;
        StatusText.Text = "正在下载安装包，请稍候…";

        var progress = new Progress<double>(value =>
        {
            DownloadProgress.Value = value;
            StatusText.Text = $"正在下载安装包 {value:P0}";
        });
        var path = await UpdateService.DownloadAsync(_info, progress);
        if (path is null)
        {
            InstallButton.IsEnabled = true;
            InstallButton.Content = "重新下载";
            StatusText.Text = "下载失败，请检查网络后重试";
            return;
        }

        StatusText.Text = "下载完成，正在静默安装并重启…";
        if (UpdateService.RunInstaller(path))
        {
            Application.Current.Shutdown();
        }
        else
        {
            InstallButton.IsEnabled = true;
            StatusText.Text = $"安装包已下载：{path}";
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
    }
}
