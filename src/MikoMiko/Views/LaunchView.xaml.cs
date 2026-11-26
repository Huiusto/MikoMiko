using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MikoMiko.Services;
using Button = System.Windows.Controls.Button;

namespace MikoMiko.Views;

/// <summary>启动页：沿用 MikoNest 的全屏壁纸 + 游戏切换 + 开始游戏布局。</summary>
public partial class LaunchView : UserControl
{
    private readonly Dictionary<string, Border> _railItems = new();
    private readonly DispatcherTimer _toastTimer;
    private string _selectedKey = "genshin";
    private bool _launching;

    private static readonly Dictionary<string, string[]> Info = new()
    {
        ["genshin"] = new[] { "提瓦特大陆，等待你的到来", "https://ys.mihoyo.com/main/" },
        ["starrail"] = new[] { "愿此行，终抵群星", "https://sr.mihoyo.com/main/" },
        ["zzz"] = new[] { "最后的都市，新的日常", "https://zzz.mihoyo.com/main/" },
        ["bh3"] = new[] { "为世界上所有的美好而战", "https://bh3.mihoyo.com/main/" },
    };

    public LaunchView()
    {
        InitializeComponent();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastBox.Visibility = Visibility.Collapsed;
        };
        Loaded += (_, _) => RefreshData();
    }

    private void RefreshData()
    {
        MikoNestBridge.Reload();
        BuildRail();
        if (!MikoNestBridge.Games.Any(g => g.Key == _selectedKey))
            _selectedKey = MikoNestBridge.Games[0].Key;
        SelectGame(_selectedKey, false);
    }

    private void BuildRail()
    {
        if (_railItems.Count > 0) return;
        foreach (var game in MikoNestBridge.Games)
        {
            var image = new Image
            {
                Source = new BitmapImage(new Uri(game.Icon, UriKind.Absolute)),
                Width = 42,
                Height = 42,
                Stretch = Stretch.Uniform,
            };
            var border = new Border
            {
                Width = 58,
                Height = 58,
                Margin = new Thickness(0, 0, 0, 12),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(0x66, 0x14, 0x1B, 0x24)),
                BorderThickness = new Thickness(1.5),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = game.Name,
                Child = image,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
            };
            border.MouseLeftButtonUp += (_, _) => SelectGame(game.Key, true);
            border.MouseEnter += (_, _) => border.Opacity = 0.9;
            border.MouseLeave += (_, _) => border.Opacity = 1;
            _railItems[game.Key] = border;
            GameRail.Children.Add(border);
        }
    }

    private void SelectGame(string key, bool animate)
    {
        _selectedKey = key;
        var game = MikoNestBridge.Games.First(g => g.Key == key);
        foreach (var pair in _railItems)
        {
            var selected = pair.Key == key;
            pair.Value.BorderBrush = new SolidColorBrush(selected
                ? Color.FromRgb(0x4E, 0xA4, 0xDD)
                : Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
            pair.Value.BorderThickness = new Thickness(selected ? 2.5 : 1.5);
            pair.Value.Background = new SolidColorBrush(selected
                ? Color.FromArgb(0x99, 0x16, 0x22, 0x30)
                : Color.FromArgb(0x66, 0x14, 0x1B, 0x24));
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(game.Background, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        BgImage.Source = bitmap;
        if (animate)
        {
            BgImage.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(420))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                });
        }

        TxtGameName.Text = game.Name;
        TxtSubtitle.Text = Info.TryGetValue(key, out var info) ? info[0] : "";
        UpdateGameState();
    }

    private void UpdateGameState()
    {
        var game = MikoNestBridge.Games.First(g => g.Key == _selectedKey);
        var dir = MikoNestBridge.GetGameDir(_selectedKey);
        var exe = MikoNestBridge.FindGameExe(_selectedKey);

        if (exe is not null)
        {
            InstalledBadge.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));
            TxtBadge.Text = "已安装";
            TxtPath.Text = "安装目录：" + (dir ?? Path.GetDirectoryName(exe));
            BtnLaunch.Content = "开始游戏";
            TxtHint.Text = "游戏已就绪，点击开始游戏；也可以切换左侧图标启动其它游戏。";
        }
        else
        {
            InstalledBadge.Background = new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C));
            TxtBadge.Text = "未检测到";
            TxtPath.Text = game.Key == "genshin"
                ? "未找到游戏主程序，可点击「定位游戏」选择安装目录。"
                : "MikoNest 记录的目录中没有找到主程序，可点击「定位游戏」重新选择。";
            BtnLaunch.Content = "定位游戏";
            TxtHint.Text = "选择包含 " + string.Join(" / ", game.ExeNames) + " 的文件夹即可开始启动。";
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        MikoNestBridge.Reload();
        UpdateGameState();
        ShowToast("已重新读取游戏目录与账号数据");
    }

    private void BtnOpenDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = MikoNestBridge.GetGameDir(_selectedKey) ?? Path.GetDirectoryName(MikoNestBridge.FindGameExe(_selectedKey) ?? "");
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            ShowToast("还没有找到该游戏的安装目录");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowToast("打开目录失败：" + ex.Message);
        }
    }

    private void BtnLocate_Click(object sender, RoutedEventArgs e) => LocateGame();

    private void LocateGame()
    {
        var game = MikoNestBridge.Games.First(g => g.Key == _selectedKey);
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = $"请选择「{game.Name}」的安装目录（包含 {string.Join(" / ", game.ExeNames)} 的文件夹）",
            UseDescriptionForTitle = true,
        };
        var current = MikoNestBridge.GetGameDir(_selectedKey);
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current)) dialog.SelectedPath = current;
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        MikoNestBridge.SetLocalGamePath(_selectedKey, dialog.SelectedPath);
        UpdateGameState();
        if (MikoNestBridge.FindGameExe(_selectedKey) is null)
            ShowToast("这个目录里没有找到游戏主程序，请选择包含 EXE 的文件夹");
        else
            ShowToast("已记录游戏目录");
    }

    private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (_launching) return;
        var exe = MikoNestBridge.FindGameExe(_selectedKey);
        if (exe is null)
        {
            LocateGame();
            return;
        }

        _launching = true;
        BtnLaunch.IsEnabled = false;
        var game = MikoNestBridge.Games.First(g => g.Key == _selectedKey);
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                UseShellExecute = true,
            });
            ShowToast($"正在启动 {game.Name}…");
            await Task.Delay(3000);
            if (Window.GetWindow(this) is { } window && window.WindowState != WindowState.Minimized)
                window.WindowState = WindowState.Minimized;
        }
        catch (Exception ex)
        {
            ShowToast("启动失败：" + ex.Message);
        }
        finally
        {
            _launching = false;
            BtnLaunch.IsEnabled = true;
        }
    }

    private void BtnOfficial_Click(object sender, RoutedEventArgs e)
    {
        if (!Info.TryGetValue(_selectedKey, out var info)) return;
        try
        {
            Process.Start(new ProcessStartInfo(info[1]) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowToast("打开官网失败：" + ex.Message);
        }
    }

    private void ShowToast(string text)
    {
        TxtToast.Text = text;
        ToastBox.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }
}
