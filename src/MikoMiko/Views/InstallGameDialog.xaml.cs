using MikoMiko.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MikoMiko.Views;

/// <summary>软件内安装游戏：选择目录、语言，显示下载/写入进度，支持暂停与取消。</summary>
public partial class InstallGameDialog : Window
{
    private readonly string _gameKey;
    private readonly string _gameName;
    private string _baseDir;
    private bool _installing;
    private CancellationTokenSource? _cts;
    private GameInstallService.PauseController? _pause;

    public string? InstalledPath { get; private set; }

    public InstallGameDialog(string gameKey, string gameName)
    {
        InitializeComponent();
        _gameKey = gameKey;
        _gameName = gameName;
        TxtTitle.Text = $"安装游戏 · {gameName}";
        TxtGameInfo.Text = $"{gameName} · 由 MikoMiko 直接安装";
        _baseDir = DefaultBaseDir();
        TxtPath.Text = CurrentTarget();
        Loaded += async (_, __) =>
        {
            await LoadPackageInfoAsync();
            ChkSubFolder_Changed(null, null);
        };
    }

    private static string DefaultBaseDir()
    {
        var d = Path.Combine("D:\\", "MikoMikoGames");
        return Directory.Exists("D:\\") ? d : Path.Combine("C:\\", "MikoMikoGames");
    }

    private string CurrentTarget()
    {
        var target = Path.Combine(_baseDir, Sanitize(_gameName));
        return ChkSubFolder?.IsChecked == true ? target : _baseDir;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    private async Task LoadPackageInfoAsync()
    {
        try
        {
            var (version, size) = await GameInstallApi.GetLatestPackageAsync(_gameKey);
            var target = CurrentTarget();
            var free = FreeSpace(target);
            var info = $"官方版本：{(!string.IsNullOrEmpty(version) ? version : "未知")}";
            if (size > 0) info += $"　所需空间：{FormatSize(size)}";
            if (free > 0) info += $"　目标盘剩余：{FormatSize(free)}";
            TxtSpace.Text = info;
            if (size > 0 && free > 0 && free < size)
                TxtSpace.Text += "\n目标磁盘空间不足，请更换安装位置。";
        }
        catch
        {
            TxtSpace.Text = "暂时无法读取官方安装信息，可以直接开始安装。";
        }
    }

    private static long FreeSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return 0;
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var i = 0;
        while (value >= 1024 && i < units.Length - 1)
        {
            value /= 1024;
            i++;
        }
        return $"{value:0.##} {units[i]}";
    }

    private void ChkSubFolder_Changed(object? sender, RoutedEventArgs? e)
    {
        // InitializeComponent 期间 XAML 设置 IsChecked 会触发本事件，此时字段和控件还没初始化。
        if (TxtPath == null || string.IsNullOrEmpty(_gameName) || string.IsNullOrEmpty(_baseDir)) return;
        TxtPath.Text = CurrentTarget();
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择游戏安装位置",
            SelectedPath = Directory.Exists(_baseDir) ? _baseDir : "D:\\",
            ShowNewFolderButton = true,
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _baseDir = dlg.SelectedPath;
            TxtPath.Text = CurrentTarget();
            _ = LoadPackageInfoAsync();
        }
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_installing) return;
        var target = CurrentTarget();
        try
        {
            Directory.CreateDirectory(target);
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法创建安装目录：" + ex.Message, "MikoMiko",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var free = FreeSpace(target);
        if (free > 0 && free < 2L * 1024 * 1024 * 1024)
        {
            var answer = MessageBox.Show($"目标磁盘剩余空间只有 {FormatSize(free)}，可能不足以安装。仍要继续吗？",
                "空间不足", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
        }

        _installing = true;
        _cts = new CancellationTokenSource();
        _pause = new GameInstallService.PauseController();
        BtnInstall.IsEnabled = false;
        BtnBrowse.IsEnabled = false;
        ChkSubFolder.IsEnabled = false;
        RdoZh.IsEnabled = RdoEn.IsEnabled = RdoJa.IsEnabled = RdoKo.IsEnabled = false;
        BtnPause.Visibility = Visibility.Visible;
        BtnPause.Content = "暂停";
        Bar.Value = 0;
        TxtProgress.Text = "正在准备…";

        var lang = RdoEn.IsChecked == true ? "en-us"
            : RdoJa.IsChecked == true ? "ja-jp"
            : RdoKo.IsChecked == true ? "ko-kr"
            : "zh-cn";

        var progress = new Progress<GameInstallService.Progress>(p =>
        {
            var total = p.Total > 0 ? p.Total : p.InstallTotal;
            var done = p.Done > 0 ? p.Done : p.InstallDone;
            if (total <= 0) Bar.Value = 0;
            else Bar.Value = Math.Clamp(done * 100.0 / total, 0, 100);

            var detail = $"{p.Stage}　{FormatSize(done)} / {FormatSize(total)}";
            if (p.BytesPerSecond > 0) detail += $"　{p.BytesPerSecond / 1024.0 / 1024.0:0.0} MB/s";
            if (total > 0) detail += $"　{Math.Clamp(done * 100.0 / total, 0, 100):0.0}%";
            TxtProgress.Text = detail;
        });

        try
        {
            var installed = await GameInstallService.InstallAsync(
                _gameKey, target, lang, progress, _cts.Token, _pause);
            InstalledPath = installed;
            Bar.Value = 100;
            TxtProgress.Text = "安装完成";
            MessageBox.Show($"「{_gameName}」已安装完成。", "MikoMiko",
                MessageBoxButton.OK, MessageBoxImage.Information);
            _installing = false;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            TxtProgress.Text = "安装已取消，已清理本次写入的文件";
            _installing = false;
        }
        catch (Exception ex)
        {
            TxtProgress.Text = "安装失败：" + ex.Message;
            MessageBox.Show("安装失败：" + ex.Message, "MikoMiko",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _installing = false;
        }
        finally
        {
            BtnInstall.IsEnabled = true;
            BtnBrowse.IsEnabled = true;
            ChkSubFolder.IsEnabled = true;
            RdoZh.IsEnabled = RdoEn.IsEnabled = RdoJa.IsEnabled = RdoKo.IsEnabled = true;
            BtnPause.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        if (_pause == null) return;
        if (_pause.IsPaused)
        {
            _pause.Resume();
            BtnPause.Content = "暂停";
            TxtProgress.Text = "已继续安装…";
        }
        else
        {
            _pause.Pause();
            BtnPause.Content = "继续";
            TxtProgress.Text = "已暂停，正在下载的少量分块会先完成";
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_installing)
        {
            var answer = MessageBox.Show("安装正在进行，关闭会取消安装并清理本次写入的文件。确定关闭吗？",
                "取消安装", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
            _cts?.Cancel();
            _pause?.Resume();
            DialogResult = false;
            return;
        }
        DialogResult = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_installing)
        {
            _cts?.Cancel();
            _pause?.Resume();
        }
        base.OnClosing(e);
    }
}
