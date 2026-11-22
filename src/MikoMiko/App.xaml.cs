using MikoMiko.Localization;
using MikoMiko.Services;
using MikoMiko.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MikoMiko;

public partial class App : Application
{
    public static SettingsService SettingsService { get; private set; } = null!;
    public static ContentService ContentService { get; private set; } = null!;
    public static ShellViewModel Shell { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
#if DEBUG
        TryCapture(e.Args);
#endif
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

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                MainWindow!.Width = 1624;
                MainWindow.Height = 1000;
                Log(path, "resized");
                Shell.Navigate(page is "Reader" or "ReaderComments" or "Search" ? "Home" : page == "VideoPlay" ? "Video" : (page == "SettingsBottom" || page == "SettingsChangelog") ? "Settings" : page, pushHistory: false);
                if (page == "Search")
                {
                    Shell.SearchText = "原神";
                    _ = Shell.SearchAsync();
                }
                if (page is "Reader" or "ReaderComments")
                {
                    Shell.OpenReaderForCapture();
                    if (page == "ReaderComments") Shell.ReaderView!.ScrollToComments();
                }
                if (page is "SettingsBottom" or "SettingsChangelog")
                {
                    var settingsView = FindChild<Views.SettingsView>(MainWindow!);
                    if (settingsView is not null)
                    {
                        if (page == "SettingsChangelog")
                        {
                            Shell.SettingsViewModel.IsChangelogOpen = true;
                            settingsView.ScrollToSection("Changelog");
                        }
                        else
                        {
                            settingsView.ScrollToSection("AppInfo");
                        }
                    }
                }
                if (page == "VideoPlay")
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
                var delayed = new DispatcherTimer { Interval = TimeSpan.FromSeconds(page is "VideoPlay" ? 12 : page is "Search" ? 5 : 2) };
                delayed.Tick += (_, _) =>
                {
                    delayed.Stop();
                    Log(path, "capturing");
                    if (page is "SettingsBottom" or "SettingsChangelog")
                    {
                        var view = FindChild<Views.SettingsView>(MainWindow!);
                        if (view is not null)
                        {
                            if (page == "SettingsChangelog") Shell.SettingsViewModel.IsChangelogOpen = true;
                            view.ScrollToSection(page == "SettingsChangelog" ? "Changelog" : "AppInfo");
                            MainWindow!.UpdateLayout();
                        }
                    }
                    if (page != "VideoPlay") Shell.Video.IsPlaying = false; // 截图时停在封面，避免抓到解码中的黑帧
                    var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                    settle.Tick += (_, _) =>
                    {
                        settle.Stop();
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
