using MikoMiko.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace MikoMiko.Views;

/// <summary>软件内安装游戏的配置页：只选择目录与语言，点「开始安装」后立即关闭，进度回到启动页。</summary>
public partial class InstallGameDialog : Window
{
    private readonly string _gameKey;
    private readonly string _gameName;
    private string _baseDir;

    public string? TargetDirectory { get; private set; }
    public string InstallLanguage { get; private set; } = "zh-cn";

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

    private void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
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

        TargetDirectory = target;
        InstallLanguage = RdoEn.IsChecked == true ? "en-us"
            : RdoJa.IsChecked == true ? "ja-jp"
            : RdoKo.IsChecked == true ? "ko-kr"
            : "zh-cn";

        // 配置页立即消失，真正的安装进度改为显示在启动页的「获取游戏」按钮上。
        DialogResult = true;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
