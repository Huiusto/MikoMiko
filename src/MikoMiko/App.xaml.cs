using MikoMiko.Localization;
using MikoMiko.Services;
using MikoMiko.ViewModels;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MikoMiko;

public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private static EventWaitHandle? _activateEvent;

    public static SettingsService SettingsService { get; private set; } = null!;
    public static ContentService ContentService { get; private set; } = null!;
    public static ShellViewModel Shell { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 所有按钮统一接入果冻动效（按压缩放、松开弹性回弹）。
        Interaction.ButtonJelly.Install();

        // 安装程序在复制完文件后调用：提前铺好启动页壁纸与页面资源，首次打开不再等下载。
        if (e.Args.Contains("--prepare-launch"))
        {
            try
            {
                Views.LaunchView.PrepareLaunchAssetsForInstall();
                Task.Run(async () => await LauncherBackgroundService.PrefetchAllAsync())
                    .Wait(TimeSpan.FromSeconds(20));
            }
            catch
            {
                // 安装期预热失败不影响软件启动，首次打开时会再拉取
            }
            Shutdown(0);
            Environment.Exit(0);
            return;
        }

        // 单实例：同一台电脑只允许一个 MikoMiko 进程，避免两个进程同时写 settings.json /
        // 登录态 / 弹幕库，造成数据互相覆盖。再次启动时把已有窗口唤到前台并退出。
        _instanceMutex = new Mutex(true, @"Local\MikoMiko.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            try
            {
                _activateEvent = EventWaitHandle.OpenExisting(@"Local\MikoMiko.Activate");
                _activateEvent.Set();
            }
            catch
            {
                // 已有实例还没建好事件时忽略
            }

            Shutdown();
            Environment.Exit(0);
            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\MikoMiko.Activate");
        StartActivationListener();

        DispatcherUnhandledException += (_, args) =>
        {
#if DEBUG
            File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "error.log"),
                DateTime.Now + " " + args.Exception + Environment.NewLine);
#endif
            MessageBox.Show($"发生了未处理的异常：\n{args.Exception.Message}", "MikoMiko",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        SettingsService = new SettingsService();
        var settings = SettingsService.Current;
        CacheJanitor.Start();

        ThemeService.Apply(settings.Theme);
        ThemeService.ApplyAccent(settings.AccentPreset);
        FontService.Apply(settings.Font);
        settings.Language = "zh-CN";
        Loc.Instance.SetLanguage("zh-CN");

        ContentService = new ContentService(settings);
        Shell = new ShellViewModel(SettingsService, ContentService);

        var window = new Views.MainWindow(Shell, SettingsService);
        MainWindow = window;

        // 正常启动时先展示萌系启动画面；截图模式直接进主窗口，避免挡住验收页面。
        var showSplash = !e.Args.Contains("--shot") && !e.Args.Contains("--update-preview");
        Views.SplashWindow? splash = null;
        if (showSplash)
        {
            splash = new Views.SplashWindow();
            splash.Show();
        }

        void ShowMainWindow()
        {
            window.Show();
            _ = Shell.InitializeAsync();
            splash?.Close();
        }

        if (showSplash)
        {
            var splashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
            splashTimer.Tick += (_, _) =>
            {
                splashTimer.Stop();
                ShowMainWindow();
            };
            splashTimer.Start();
        }
        else
        {
            ShowMainWindow();
        }

        // 每次启动自动检查一次更新；点了「取消」的版本不再打扰
        if (settings.CheckUpdateOnStartup && !e.Args.Contains("--shot"))
        {
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            updateTimer.Tick += async (_, _) =>
            {
                updateTimer.Stop();
                try
                {
                    await UpdateService.PromptIfAvailableAsync(MainWindow, settings, manual: false);
                }
                catch
                {
                    // 网络异常时静默跳过启动检查
                }
            };
            updateTimer.Start();
        }
#if DEBUG
        // 设计验收：用假版本信息单独打开更新窗口，不触发网络下载。
        if (e.Args.Contains("--update-preview"))
        {
            var previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            previewTimer.Tick += (_, _) =>
            {
                previewTimer.Stop();
                var demo = new UpdateService.UpdateInfo(
                    new Version("0.1.53"), "v0.1.53", "MikoMiko 0.1.53",
                    "· 修复每日任务极验弹窗点击「完成验证」后卡住\n· 修复视频全屏四边仍能看见桌面背景",
                    "https://github.com/Huiusto/MikoMiko/releases", "MikoMiko-Setup-0.1.53.exe", "");
                new Views.UpdateWindow(demo, settings).Show();
            };
            previewTimer.Start();
        }
        TryCapture(e.Args);
#endif
    }

    // ---------------------------------------------------------------- 单实例唤醒

    private void StartActivationListener()
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    if (!_activateEvent!.WaitOne()) continue;
                }
                catch
                {
                    return;
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var window = MainWindow;
                    if (window is null) return;
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Show();
                    window.Activate();
                    window.Topmost = true;
                    window.Topmost = false;
                    window.Focus();
                }));
            }
        })
        { IsBackground = true, Name = "MikoMiko-Activate" };
        thread.Start();
    }

    // ---------------------------------------------------------------- 开发期截图（--shot 页面 路径）
    private void Log(string path, string message)
    {
        try
        {
            File.AppendAllText(path + ".log", message + Environment.NewLine);
        }
        catch
        {
            // 忽略日志失败
        }
    }

    private void TryCapture(string[] args)
    {
        Log(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "miko-shot"), "args=" + string.Join('|', args));
        if (args.Length < 3 || args[0] != "--shot") return;

        var page = args[1];
        var path = args[2];
        Log(path, "start page=" + page);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(page == "Collection" ? 16 : page == "Video" ? 10 : page == "Anime" ? 6 : 2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                MainWindow!.Width = 1614;
                MainWindow.Height = 940;
                MainWindow.Left = 0;
                MainWindow.Top = 0;
                Log(path, "resized");
                var navPage = page is "Reader" or "ReaderComments" or "Search" or "Posts" or "UserProfile" or "UserGame" ? "Home"
                    : page is "Anime" or "AnimeHome" or "AnimeMore" or "AnimeMovie" or "AnimeDownload" or "AnimeSearch" or "Anime4K" or "AnimeTap" ? "Anime"
                    : page is "VideoPlay" or "VideoOptions" or "VideoComments" ? "Video"
                    : page is "SettingsBottom" or "SettingsChangelog" or "SettingsGroups" ? "Settings"
                    : page == "MineQr" ? "Mine"
                    : page is "MineProfile" or "MineGame" or "MineSettings" ? "Mine"
                    : page == "MineAccount" ? "Mine"
                    : page is "Upload" or "UploadType" or "UploadArticle" ? "Home"
                    : page;
                Shell.Navigate(navPage, pushHistory: false);
                if (page is "Anime" or "AnimeHome" or "AnimeMore" or "AnimeMovie" or "AnimeDownload" or "AnimeSearch" or "Anime4K" or "AnimeTap")
                {
                    if (page == "AnimeTap")
                    {
                        var tapDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
                        tapDelay.Tick += (_, _) => { tapDelay.Stop(); Shell.AnimePage?.DebugTap(140, 650); Log(path, "anime-tap"); };
                        tapDelay.Start();
                    }
                    if (page == "Anime4K")
                    {
                        var demoDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                        demoDelay.Tick += (_, _) => { demoDelay.Stop(); Log(path, "anime4k-demo"); Shell.OpenAnime4KDemo(); Log(path, "anime4k-demo-called"); };
                        demoDelay.Start();
                    }
                    if (page is "AnimeDownload" or "AnimeSearch")
                    {
                        var downloadDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                        downloadDelay.Tick += (_, _) =>
                        {
                            downloadDelay.Stop();
                            if (page == "AnimeDownload") Shell.Anime.DownloadModuleCommand.Execute(null);
                            else
                            {
                                Shell.Anime.SearchText = "凡人";
                                Shell.Anime.SearchCommand.Execute(null);
                            }
                        };
                        downloadDelay.Start();
                    }
                    if (page is "AnimeMore" or "AnimeMovie")
                    {
                        var moreDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                        moreDelay.Tick += (_, _) =>
                        {
                            moreDelay.Stop();
                            Shell.Anime.ShowMoreCommand.Execute(null);
                            if (page == "AnimeMovie")
                            {
                                var feedDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                                feedDelay.Tick += (_, _) =>
                                {
                                    feedDelay.Stop();
                                    var movie = Shell.Anime.FeedFilters.FirstOrDefault(f => f.Key == "movie");
                                    if (movie is not null) Shell.Anime.SelectFeedCommand.Execute(movie);
                                };
                                feedDelay.Start();
                            }
                        };
                        moreDelay.Start();
                    }
                }
                if (page == "Collection")
                {
                    Shell.OpenCollection(new MikoMiko.Models.UserCollectionItem
                    {
                        Id = "5032",
                        Title = "原神·角色攻略合集",
                        PostCount = 156,
                    });
                }
                if (page == "Posts" && Shell.HomeTabs.Count > 1)
                {
                    foreach (var tab in Shell.HomeTabs) tab.IsSelected = tab.Key == "Posts";
                    _ = Shell.Home.ActivatePostsAsync();
                }
                if (page == "MineQr")
                {
                    Shell.MineViewModel.LoginCommand.Execute(null);
                }
                if (page is "MineProfile" or "MineGame" or "MineSettings")
                {
                    // 仅开发期截图：用示例资料检查个人主页版式，不写入设置
                    Shell.MineViewModel.IsLoggedIn = true;
                    Shell.MineViewModel.Profile = new Models.UserProfile
                    {
                        IsLoggedIn = true,
                        Nickname = "灰岁Huiusto",
                        Avatar = "pack://application:,,,/Assets/icon.png",
                        Level = 6,
                        Signature = "想吃饭…",
                        Uid = "310621322",
                        IpRegion = "贵州",
                        Fans = "21",
                        Following = "87",
                        Likes = "1万",
                        Views = "42",
                    };
                    Shell.MineViewModel.Games.Clear();
                    Shell.MineViewModel.Games.Add(new Models.GameAccount
                    {
                        GameId = 2, GameName = "原神", Uid = "508927439", Server = "世界树", Level = "60",
                        LevelLabel = "冒险等级", Extra = "派蒙", ExtraLabel = "角色", AccentHex = "#4EA4DD", Badge = "已绑定",
                    });
                    Shell.MineViewModel.Games.Add(new Models.GameAccount
                    {
                        GameName = "崩坏：星穹铁道", Uid = "102634712", Server = "星穹列车", Level = "70",
                        LevelLabel = "开拓等级", Extra = "穹", ExtraLabel = "角色", AccentHex = "#8B7BE8", Badge = "已绑定",
                    });
                    Shell.MineViewModel.Games.Add(new Models.GameAccount
                    {
                        GameName = "绝区零", Uid = "26341207", Server = "新艾利都", Level = "55",
                        LevelLabel = "绳网等级", Extra = "哲", ExtraLabel = "角色", AccentHex = "#F5B94A", Badge = "已绑定",
                    });
                    if (page == "MineGame")
                    {
                        _ = Shell.MineViewModel.OpenGameCommand.ExecuteAsync(Shell.MineViewModel.Games[0]);
                    }
                    if (page == "MineSettings")
                    {
                        Shell.MineViewModel.IsGameSettingsOpen = true;
                    }
                }
                if (page == "Search")
                {
                    Shell.SearchText = "原神";
                    _ = Shell.SearchAsync();
                }
                if (page is "Upload" or "UploadType" or "UploadArticle")
                {
                    Shell.OpenUpload();
                    if (page == "UploadType") Shell.Upload.ChooseSectionCommand.Execute(Shell.Upload.Sections[4]);
                    if (page == "UploadArticle")
                    {
                        Shell.Upload.ChooseSectionCommand.Execute(Shell.Upload.Sections[2]);
                        Shell.Upload.PublishCommand.Execute(Shell.Upload.ContentTypes[0]);
                    }
                }
                if (page == "UserProfile")
                {
                    Shell.OpenUserProfile("310621322");
                }
                if (page == "UserGame")
                {
                    Shell.OpenUserProfile("310621322");
                    var gameDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                    gameDelay.Tick += (_, _) =>
                    {
                        gameDelay.Stop();
                        if (Shell.ProfileView?.DataContext is ViewModels.UserProfileViewModel vm &&
                            vm.Games.FirstOrDefault() is { } game)
                        {
                            vm.OpenGameCommand.Execute(game);
                        }
                    };
                    gameDelay.Start();
                }
                if (page is "Reader" or "ReaderComments")
                {
                    Shell.OpenReaderForCapture();
                    if (page == "ReaderComments") Shell.ReaderView!.ScrollToComments();
                }
                if (page is "SettingsBottom" or "SettingsChangelog" or "SettingsGroups")
                {
                    var settingsView = FindChild<Views.SettingsView>(MainWindow!);
                    if (settingsView is not null)
                    {
                        if (page == "SettingsChangelog")
                        {
                            Shell.SettingsViewModel.IsChangelogOpen = true;
                            settingsView.ScrollToSection("Changelog");
                        }
                        else if (page == "SettingsGroups")
                        {
                            settingsView.ScrollToSection("Cache");
                        }
                        else
                        {
                            settingsView.ScrollToSection("AppInfo");
                        }
                    }
                }
                if (page == "MineAccount")
                {
                    var switchDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    switchDelay.Tick += (_, _) =>
                    {
                        switchDelay.Stop();
                        var mine = FindChild<Views.MineView>(MainWindow!);
                        if (mine?.FindName("BtnSwitchMineAccount") is System.Windows.Controls.Button btn)
                        {
                            btn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                            Log(path, "mine-account-click");
                        }
                    };
                    switchDelay.Start();
                }
                if (page is "VideoPlay" or "VideoOptions" or "VideoComments")
                {
                    var startDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                    startDelay.Tick += (_, _) =>
                    {
                        startDelay.Stop();
                        Shell.VideoPage?.PlayForCapture();
                    };
                    startDelay.Start();
                }
                Log(path, "navigated");
                var delayed = new DispatcherTimer { Interval = TimeSpan.FromSeconds(page is "VideoPlay" or "VideoOptions" or "VideoComments" ? 12 : page is "Search" or "MineQr" or "Posts" or "MineGame" or "UserProfile" ? 6 : page is "UserGame" or "AnimeHome" or "AnimeMore" or "AnimeMovie" or "AnimeDownload" or "AnimeSearch" ? 12 : page == "Moment" ? 12 : page == "UploadArticle" ? 32 : page == "ReaderComments" ? 9 : 2) };
                delayed.Tick += (_, _) =>
                {
                    delayed.Stop();
                    Log(path, "capturing");
                    if (page is "SettingsBottom" or "SettingsChangelog" or "SettingsGroups")
                    {
                        var view = FindChild<Views.SettingsView>(MainWindow!);
                        if (view is not null)
                        {
                            if (page == "SettingsChangelog") Shell.SettingsViewModel.IsChangelogOpen = true;
                            view.ScrollToSection(page == "SettingsGroups" ? "Cache" : page == "SettingsChangelog" ? "Changelog" : "AppInfo");
                            MainWindow!.UpdateLayout();
                        }
                    }
                    if (page == "VideoOptions")
                    {
                        Shell.Video.IsQualityOpen = true;
                        Shell.Video.IsCommentsOpen = false;
                        MainWindow!.UpdateLayout();
                    }
                    if (page == "VideoComments")
                    {
                        Shell.Video.IsCommentsOpen = true;
                        Shell.Video.IsQualityOpen = false;
                        MainWindow!.UpdateLayout();
                    }
                    if (page is "Anime" or "AnimeHome" or "AnimeMore" or "AnimeMovie" or "AnimeDownload" or "AnimeSearch")
                    {
                        Shell.CloseBrowser();
                        var animeTab = Shell.HomeTabs.FirstOrDefault(t => t.Key == "Anime");
                        if (animeTab is not null && !animeTab.IsSelected)
                            Shell.SelectTabCommand.Execute(animeTab);
                        if (page is not ("Anime" or "AnimeSearch" or "AnimeMovie") && Shell.Anime.IsInstalled)
                            Shell.Anime.PrepareForCapture(page == "AnimeMore");
                        MainWindow!.UpdateLayout();
                        Shell.AnimePage?.ResetScroll();
                        MainWindow.UpdateLayout();
                        Shell.AnimePage?.ResetScroll();
                    }
                    if (page == "ReaderComments")
                    {
                        MainWindow!.UpdateLayout();
                        Shell.ReaderView?.ScrollToComments();
                        MainWindow!.UpdateLayout();
                    }
                    if (page is not ("VideoPlay" or "VideoOptions" or "VideoComments")) Shell.Video.IsPlaying = false; // 截图时停在封面，避免抓到解码中的黑帧
                    var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(
                        page is "Anime" or "AnimeHome" or "AnimeMore" or "AnimeMovie" or "AnimeDownload" or "AnimeSearch" ? 7000
                        : page == "Anime4K" ? 22000 : page == "AnimeTap" ? 26000 : 400) };
                    settle.Tick += (_, _) =>
                    {
                        settle.Stop();
                        Shell.AnimePage?.ResetScroll();
                        if (page == "ReaderComments") Shell.ReaderView?.ScrollToComments();
                        MainWindow!.UpdateLayout();
                        if (page is "Anime" or "AnimeHome" or "AnimeMore" or "AnimeMovie" or "AnimeDownload" or "AnimeSearch" or "Anime4K" or "AnimeTap")
                            CaptureWindowFromScreen(path);
                        else
                            CaptureWindow(path);
                        Log(path, "saved");
                        Shutdown();
                        Environment.Exit(0);
                    };
                    settle.Start();
                };
                delayed.Start();
            }
            catch (Exception ex)
            {
                Log(path, "error " + ex);
                Shutdown();
            }
        };
        timer.Start();
        Log(path, "timer started");
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) return match;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindChild<T>(child);
            if (found is not null) return found;
        }

        return null;
    }

    private void CaptureWindow(string path)
    {
        var window = MainWindow!;
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(window.Width, window.Height));
        root.Arrange(new Rect(0, 0, window.Width, window.Height));
        root.UpdateLayout();

        var bitmap = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>WebView2 是独立子窗口，RenderTargetBitmap 抓不到，改用屏幕抓取。</summary>
    private void CaptureWindowFromScreen(string path)
    {
        var window = MainWindow!;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (!NativeMethods.GetWindowRect(handle, out var rect)) return;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return;
        using var bitmap = new System.Drawing.Bitmap(width, height);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, bitmap.Size);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Rect { public int Left, Top, Right, Bottom; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    }
}

