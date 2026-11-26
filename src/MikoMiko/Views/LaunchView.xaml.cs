using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using MikoMiko.Services;
using Cursors = System.Windows.Input.Cursors;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace MikoMiko.Views;

/// <summary>启动页：仿官方启动器主视觉 + 本地游戏路径定位与启动。</summary>
public partial class LaunchView : UserControl
{
    private sealed class GameProfile
    {
        public required string Key;
        public required string Name;
        public required string Logo;
        public required string Version;
        public required string HeroTitle;
        public required string HeroSubtitle;
        public required string Website;
        public required string ActivityImage;
        public required string[] Activities;
        public required string[] Notices;
        public required string[] News;
    }

    private static readonly GameProfile[] Profiles =
    {
        new()
        {
            Key = "genshin",
            Name = "原神",
            Logo = "原神",
            Version = "VERSION   7.2",
            HeroTitle = "晶石如林生长之地",
            HeroSubtitle = "新地区「新基捷城」、「如翼之峰」现已开放",
            Website = "https://ys.mihoyo.com/",
            ActivityImage = "pack://application:,,,/Assets/Launch/activity_genshin.jpg",
            Activities = new[] { "「新基捷城」区域探索开启", "「如翼之峰」版本主题活动" },
            Notices = new[] { "7.2 版本更新说明", "已知问题修复公告" },
            News = new[] { "版本前瞻特别节目回顾", "新角色与区域玩法介绍" },
        },
        new()
        {
            Key = "starrail",
            Name = "星穹铁道",
            Logo = "星穹铁道",
            Version = "LATEST VERSION",
            HeroTitle = "愿此行，终抵群星",
            HeroSubtitle = "全新开拓任务与跃迁活动现已开放",
            Website = "https://sr.mihoyo.com/",
            ActivityImage = "pack://application:,,,/Assets/Launch/bg_StarRail.jpg",
            Activities = new[] { "全新开拓任务现已开放", "版本跃迁活动进行中" },
            Notices = new[] { "版本更新说明", "已知问题修复公告" },
            News = new[] { "版本前瞻节目回顾", "新角色玩法介绍" },
        },
        new()
        {
            Key = "zzz",
            Name = "绝区零",
            Logo = "绝区零",
            Version = "LATEST VERSION",
            HeroTitle = "最后的都市，新的日常",
            HeroSubtitle = "全新代理人、街区与委托现已开放",
            Website = "https://zzz.mihoyo.com/",
            ActivityImage = "pack://application:,,,/Assets/Launch/bg_zzz.jpg",
            Activities = new[] { "新代理人版本活动开启", "限时委托与登录活动" },
            Notices = new[] { "版本更新说明", "已知问题修复公告" },
            News = new[] { "版本前瞻特别节目", "新代理人战斗展示" },
        },
        new()
        {
            Key = "bh3",
            Name = "崩坏3",
            Logo = "崩坏3",
            Version = "LATEST VERSION",
            HeroTitle = "为世界上所有的美好而战",
            HeroSubtitle = "全新版本内容与女武神活动现已开放",
            Website = "https://bh3.mihoyo.com/",
            ActivityImage = "pack://application:,,,/Assets/Launch/bg_bh3.jpg",
            Activities = new[] { "新版本主题活动开启", "女武神补给与登录活动" },
            Notices = new[] { "版本更新说明", "已知问题修复公告" },
            News = new[] { "版本前瞻节目回顾", "新女武神玩法介绍" },
        },
    };

    private readonly Dictionary<string, Border> _gameBorders = new();
    private string _gameKey = "genshin";
    private string _tab = "activity";
    private DispatcherTimer? _toastTimer;

    public LaunchView()
    {
        InitializeComponent();
        Loaded += (_, _) => Initialize();
        Unloaded += (_, _) => _toastTimer?.Stop();
    }

    private GameProfile Profile => Profiles.First(p => p.Key == _gameKey);

    private void Initialize()
    {
        MikoNestBridge.Reload();
        BuildGameStrip();
        ApplyGame("genshin", false);
    }

    private void BuildGameStrip()
    {
        GameStrip.Children.Clear();
        _gameBorders.Clear();
        foreach (var profile in Profiles)
        {
            var image = new Image
            {
                Source = LoadImage(profile.Key == "genshin"
                    ? "pack://application:,,,/Assets/Launch/icons/icon_genshin.png"
                    : MikoNestBridge.Games.First(g => g.Key == profile.Key).Icon),
                Width = 25,
                Height = 25,
                Stretch = Stretch.Uniform,
            };
            var border = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(11),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)),
                Background = new SolidColorBrush(Color.FromArgb(0x26, 0x00, 0x00, 0x00)),
                Margin = new Thickness(7, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = profile.Name,
                Child = image,
            };
            border.MouseLeftButtonUp += (_, _) => ApplyGame(profile.Key, true);
            _gameBorders[profile.Key] = border;
            GameStrip.Children.Add(border);
        }
    }

    private void ApplyGame(string key, bool animate)
    {
        if (Profiles.All(p => p.Key != key)) return;
        _gameKey = key;
        var profile = Profile;
        ImgBackground.Source = LoadImage(MikoNestBridge.Games.First(g => g.Key == key).Background);
        ImgActivity.Source = LoadImage(profile.ActivityImage);
        TxtLogo.Text = profile.Logo;
        TxtHeroVersion.Text = profile.Version.Trim();
        TxtHeroTitle.Text = profile.HeroTitle;
        TxtHeroSubtitle.Text = profile.HeroSubtitle;
        TxtFooter.Text = "MIKOMIKO · 本地游戏启动器";

        foreach (var (gameKey, border) in _gameBorders)
        {
            var selected = gameKey == key;
            border.BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD8, 0x4D))
                : new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF));
            border.BorderThickness = new Thickness(selected ? 2 : 1);
            border.Background = new SolidColorBrush(selected
                ? Color.FromArgb(0x59, 0x00, 0x00, 0x00)
                : Color.FromArgb(0x26, 0x00, 0x00, 0x00));
            if (border.Child is Image image) image.Opacity = selected ? 1 : 0.74;
        }

        _tab = "activity";
        SetTabVisual("activity");
        UpdateActivityList();
        UpdateInstallState();

        if (animate)
        {
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0.45, 1,
                TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
        }
    }

    private void UpdateInstallState()
    {
        var exe = MikoNestBridge.FindGameExe(_gameKey);
        if (exe is not null)
        {
            BtnPrimary.Content = "开始游戏";
            BtnPrimary.CommandParameter = "▶";
            BtnLocate.Content = "已安装 · 定位游戏";
            TxtFooter.Text = "MIKOMIKO · 已找到本地游戏";
        }
        else
        {
            BtnPrimary.Content = "获取游戏";
            BtnPrimary.CommandParameter = "↓";
            BtnLocate.Content = "已安装？定位游戏";
            TxtFooter.Text = "MIKOMIKO · 本地游戏启动器";
        }
    }

    private void SetTabVisual(string tab)
    {
        _tab = tab;
        foreach (var (button, key) in new[]
                 {
                     (TabActivity, "activity"), (TabNotice, "notice"), (TabNews, "news"),
                 })
        {
            var selected = key == tab;
            button.Foreground = new SolidColorBrush(selected
                ? Color.FromRgb(0x1F, 0x5E, 0x8E)
                : Color.FromRgb(0x7C, 0x8B, 0x98));
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
        TabIndicator.Margin = new Thickness(tab switch
        {
            "notice" => 67,
            "news" => 130,
            _ => 2,
        }, 0, 0, 0);
    }

    private void UpdateActivityList()
    {
        var profile = Profile;
        var items = _tab switch
        {
            "notice" => profile.Notices,
            "news" => profile.News,
            _ => profile.Activities,
        };
        var first = items.Length > 0 ? items[0] : "暂无内容";
        var second = items.Length > 1 ? items[1] : "";
        TxtActivity1.Text = first;
        TxtActivity2.Text = second;
        TxtActivityDate1.Text = DateTime.Now.ToString("MM/dd");
        TxtActivityDate2.Text = DateTime.Now.AddDays(-4).ToString("MM/dd");
    }

    private static BitmapImage LoadImage(string uri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.UriSource = new Uri(uri, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void TabActivity_Click(object sender, RoutedEventArgs e)
    {
        SetTabVisual("activity");
        UpdateActivityList();
    }

    private void TabNotice_Click(object sender, RoutedEventArgs e)
    {
        SetTabVisual("notice");
        UpdateActivityList();
    }

    private void TabNews_Click(object sender, RoutedEventArgs e)
    {
        SetTabVisual("news");
        UpdateActivityList();
    }

    private void BtnHotspot_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(Profile.Website);
        ShowToast($"已打开 {Profile.Name} 官方网站");
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        var exe = MikoNestBridge.FindGameExe(_gameKey);
        if (exe is null)
        {
            OpenUrl(Profile.Website);
            ShowToast($"未找到本地 {Profile.Name}，已在浏览器打开官网");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            });
            ShowToast($"正在启动 {Profile.Name}…");
        }
        catch (Exception ex)
        {
            ShowToast("启动失败：" + ex.Message);
        }
    }

    private void BtnLocate_Click(object sender, RoutedEventArgs e)
    {
        var exe = MikoNestBridge.FindGameExe(_gameKey);
        if (exe is not null)
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{exe}\"");
                ShowToast("已在资源管理器中定位游戏");
                return;
            }
            catch { }
        }

        var dialog = new OpenFileDialog
        {
            Title = $"定位 {Profile.Name}",
            Filter = "游戏程序|*.exe|所有文件|*.*",
            CheckFileExists = true,
        };
        var dir = MikoNestBridge.GetGameDir(_gameKey);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            dialog.InitialDirectory = dir;
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            var selected = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(selected))
            {
                MikoNestBridge.SetLocalGamePath(_gameKey, selected);
                UpdateInstallState();
                ShowToast($"已保存 {Profile.Name} 的安装位置");
            }
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void ShowToast(string text)
    {
        TxtToastText.Text = text;
        TxtToast.Visibility = Visibility.Visible;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _toastTimer.Tick += (_, _) =>
        {
            TxtToast.Visibility = Visibility.Collapsed;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }
}
