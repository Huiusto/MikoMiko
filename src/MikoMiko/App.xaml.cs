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
        Loc.Instance.SetLanguage(settings.Language);

        ContentService = new ContentService(settings);
        Shell = new ShellViewModel(SettingsService, ContentService);

        var window = new Views.MainWindow(Shell, SettingsService);
        MainWindow = window;
        window.Show();

        _ = Shell.InitializeAsync();

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

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(page == "Video" ? 10 : 2) };
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
                    : page is "Anime" or "AnimeHome" or "AnimeMore" or "AnimeDownload" or "AnimeSearch" ? "Home"
                    : page is "VideoPlay" or "VideoOptions" or "VideoComments" ? "Video"
                    : page is "SettingsBottom" or "SettingsChangelog" or "SettingsGroups" ? "Settings"
                    : page == "MineQr" ? "Mine"
                    : page is "MineProfile" or "MineGame" or "MineSettings" ? "Mine"
                    : page is "Upload" or "UploadType" or "UploadArticle" ? "Home"
                    : page;
                Shell.Navigate(navPage, pushHistory: false);
                if (page is "Anime" or "AnimeHome" or "AnimeMore" or "AnimeDownload" or "AnimeSearch")
                {
                    var animeTab = Shell.HomeTabs.FirstOrDefault(t => t.Key == "Anime");
                    if (animeTab is not null) Shell.SelectTabCommand.Execute(animeTab);
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
                    if (page == "AnimeMore")
                    {
                        var moreDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                        moreDelay.Tick += (_, _) =>
                        {
                            moreDelay.Stop();
                            Shell.Anime.ShowMoreCommand.Execute(null);
                        };
                        moreDelay.Start();
                    }
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
                var delayed = new DispatcherTimer { Interval = TimeSpan.FromSeconds(page is "VideoPlay" or "VideoOptions" or "VideoComments" ? 12 : page is "Search" or "MineQr" or "Posts" or "MineGame" or "UserProfile" ? 6 : page is "UserGame" or "AnimeHome" or "AnimeMore" or "AnimeDownload" or "AnimeSearch" ? 12 : page == "Moment" ? 12 : page == "UploadArticle" ? 32 : page == "ReaderComments" ? 9 : 2) };
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
                    if (page is "Anime" or "AnimeHome" or "AnimeMore" or "AnimeDownload" or "AnimeSearch")
                    {
                        Shell.CloseBrowser();
                        var animeTab = Shell.HomeTabs.FirstOrDefault(t => t.Key == "Anime");
                        if (animeTab is not null && !animeTab.IsSelected)
                            Shell.SelectTabCommand.Execute(animeTab);
                        if (page is not ("Anime" or "AnimeSearch") && Shell.Anime.IsInstalled)
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
                    var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                    settle.Tick += (_, _) =>
                    {
                        settle.Stop();
                        Shell.AnimePage?.ResetScroll();
                        if (page == "ReaderComments") Shell.ReaderView?.ScrollToComments();
                        MainWindow!.UpdateLayout();
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
}
