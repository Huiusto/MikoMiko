using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Localization;
using MikoMiko.Models;
using MikoMiko.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;

namespace MikoMiko.ViewModels;

/// <summary>设置页：常规、窗口、播放、维护、关于等。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;
    private readonly ContentService _content;

    public SettingsViewModel(SettingsService service, ContentService content)
    {
        _service = service;
        _content = content;

        // 账号卡片有两处文案由 VM 拼接，切换语言时主动刷新，避免停留在旧语言。
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(AccountName));
            OnPropertyChanged(nameof(AccountUid));
            OnPropertyChanged(nameof(AccountActionText));
        };

        Sections = new ObservableCollection<ChoiceOption>
        {
            new("General", Loc.Instance["S.General"]),
            new("Cache", Loc.Instance["S.Cache"]),
            new("Maintain", Loc.Instance["S.Maintain"]),
            new("AppInfo", "应用信息"),
            new("Disclaimer", Loc.Instance["S.Disclaimer"]),
            new("OpenSource", Loc.Instance["S.OpenSource"]),
        };
        Sections[0].IsSelected = true;

        ThemeOptions = new ObservableCollection<ChoiceOption>
        {
            new("System", Loc.Instance["S.Theme.System"]),
            new("Light", Loc.Instance["S.Theme.Light"]),
            new("Dark", Loc.Instance["S.Theme.Dark"]),
        };

        FontOptions = new ObservableCollection<ChoiceOption>(
            FontService.Available.Select(f => new ChoiceOption(f.Name, f.Display)));

        QualityOptions = new ObservableCollection<ChoiceOption>
        {
            new("1080P", "1080P"), new("720P", "720P"), new("480P", "480P"), new("Auto", "自动"),
        };

        WindowSizeOptions = new ObservableCollection<ChoiceOption>
        {
            new("Default", Loc.Instance["S.WindowDefault"]),
            new("Small", Loc.Instance["S.WindowSmall"]),
            new("Large", Loc.Instance["S.WindowLarge"]),
        };

        CloseOptions = new ObservableCollection<ChoiceOption>
        {
            new("Tray", Loc.Instance["S.CloseTray"]),
            new("Exit", Loc.Instance["S.CloseExit"]),
            new("Ask", Loc.Instance["S.CloseAsk"]),
        };

        AccentOptions = new ObservableCollection<AccentPreset>(ThemeService.Accents);

        SyncSelections();
        RefreshCacheSize();
    }

    public AppSettings Settings => _service.Current;

    public ObservableCollection<ChoiceOption> Sections { get; }
    public ObservableCollection<ChoiceOption> ThemeOptions { get; }
    public ObservableCollection<ChoiceOption> FontOptions { get; }
    public ObservableCollection<ChoiceOption> QualityOptions { get; }
    public ObservableCollection<ChoiceOption> WindowSizeOptions { get; }
    public ObservableCollection<ChoiceOption> CloseOptions { get; }
    public ObservableCollection<AccentPreset> AccentOptions { get; }

    public string VersionText => AppInfo.Version;
    public string RuntimeText => AppInfo.RuntimeDescription;
    public string InstallPath => AppInfo.InstallDirectory;
    public string RepositoryUrl => AppInfo.RepositoryUrl;
    public string AccountName => IsLoggedIn
        ? (string.IsNullOrWhiteSpace(Settings.Nickname) ? Loc.Instance.TranslateText("已登录") : Settings.Nickname)
        : Loc.Instance.TranslateText("未登录");

    public string AccountAvatar => IsLoggedIn && !string.IsNullOrWhiteSpace(Settings.Avatar)
        ? Settings.Avatar
        : "pack://application:,,,/Assets/icon.png";

    public string AccountUid => Loc.Instance.TranslateText(IsLoggedIn ? "已绑定米游社账号" : "扫码登录后同步米游社账号信息");

    public string AccountActionText => IsLoggedIn
        ? Loc.Instance.TranslateText("前往我的主页")
        : Loc.Instance["Mine.Bind"];

    public string DownloadDirectory => string.IsNullOrWhiteSpace(Settings.DownloadPath)
        ? AppInfo.DefaultDownloadDirectory
        : Settings.DownloadPath;

    /// <summary>账号资料在「我的」页登录后会变化，每次进入设置都重新读取。</summary>
    public void RefreshAccount()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(AccountAvatar));
        OnPropertyChanged(nameof(AccountUid));
        OnPropertyChanged(nameof(AccountActionText));
    }

    /// <summary>供右侧目录点击时同步选中态。</summary>
    public void SelectSectionByKey(string key)
    {
        var option = Sections.FirstOrDefault(s => s.Key == key);
        if (option is null) return;
        IsSelected(Sections, option.Key);
        SelectedSection = option.Key;
    }
    public string AppName => AppInfo.Name;
    public string AppDataDirectory => AppInfo.DataDirectory;
    public string ChangelogText => BuildBriefChangelog(Loc.Instance["S.Changelog.Body"]);

    private static string LatestChangelogEntry => Loc.Instance.Language switch
    {
        "zh-TW" =>
            "0.1.71 · 追番/影視移除所有外部瀏覽器與網頁跳轉，只在本軟體內解析播放，失敗僅提示。" +
            "啟動頁左下角新增與官方啟動器同步的「相關內容」，每 5 秒自動輪播，遊戲設定可隱藏。" +
            "更新日誌改為簡版，每版兩三句、帶顏文字，版本間用橫線隔開。",
        "en-US" =>
            "0.1.71 · Anime/Films no longer opens any external browser or web page; everything resolves and plays inside the app, with an in-app notice on failure. " +
            "Launch adds official-synced Related Content at the lower-left, auto-rotating every 5 seconds, with a visibility toggle in game settings. " +
            "Settings now shows a concise changelog, two or three lines per version with kaomoji and dividers.",
        _ =>
            "0.1.71 · 追番/影视去掉所有外部浏览器和网页跳转，只在本软件内解析播放，失败只提示。" +
            "启动页左下角新增与官方启动器同步的「相关内容」，每 5 秒自动轮播，游戏设置里能单独隐藏。" +
            "更新日志改为简版，每版两三句、带颜文字，版本间用横线隔开。",
    };

    private static readonly string[] ChangelogKaomoji =
    {
        "(๑•̀ㅂ•́)و✧", "(｡･ω･｡)ﾉ♡", "(๑>؂<๑)", "( •̀ ω •́ )✧", "(˶‾᷄ ⁻̫ ‾᷅˵)",
        "(ง •̀_•́)ง", "٩(๑>◡<๑)۶", "(´･ω･`)",
    };

    /// <summary>把每版长日志压成两三句短句，并在版本之间插入横线。</summary>
    private static string BuildBriefChangelog(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return LatestChangelogEntry;
        var lines = raw.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return LatestChangelogEntry;

        var builder = new StringBuilder();
        var title = lines[0];
        var versions = lines.Skip(1).Where(line => line.StartsWith("0.1.", StringComparison.Ordinal)).ToList();
        var hasLatest = versions.Any(line => line.StartsWith("0.1.71", StringComparison.Ordinal));
        if (!hasLatest) versions.Insert(0, LatestChangelogEntry);

        builder.AppendLine(title);
        for (var i = 0; i < versions.Count; i++)
        {
            builder.AppendLine();
            builder.AppendLine(BriefLine(versions[i], i));
            if (i == versions.Count - 1) continue;
            builder.AppendLine();
            builder.AppendLine("──────────────");
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd();
    }

    private static string BriefLine(string line, int index)
    {
        var dot = line.IndexOf('·');
        var version = dot > 0 ? line[..dot].Trim() : string.Empty;
        var body = dot > 0 ? line[(dot + 1)..].Trim() : line.Trim();
        if (string.IsNullOrWhiteSpace(version)) return line;

        var clauses = body.Replace('；', '。').Replace("\n", "。")
            .Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(clause => clause.Length > 0)
            .Select(clause => Shorten(clause.Trim(' ', '，', '、', '。', '；', '：')))
            .Where(clause => clause.Length > 0)
            .Take(3)
            .ToList();
        if (clauses.Count == 0) clauses.Add("细节与体验同步优化");
        if (clauses.Count == 1) clauses.Add("细节与体验同步优化");

        var text = string.Join("。", clauses) + "。";
        return $"{version} · {text} {ChangelogKaomoji[index % ChangelogKaomoji.Length]}";
    }

    private static string Shorten(string text)
    {
        const int max = 58;
        if (text.Length <= max) return text;
        var cut = max;
        var lastBreak = text.LastIndexOfAny(new[] { '，', '、', ' ' }, cut - 1);
        if (lastBreak >= cut - 18) cut = lastBreak;
        return text[..cut].TrimEnd('，', '、', ' ') + "…";
    }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Settings.Cookie);

    [ObservableProperty] private string _selectedSection = "General";
    [ObservableProperty] private string _cacheSize = "0 MB";
    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private string _toast = string.Empty;
    [ObservableProperty] private bool _isChangelogOpen;

    [RelayCommand]
    private void ToggleChangelog() => IsChangelogOpen = !IsChangelogOpen;

    /// <summary>点击抽屉外任意位置关闭更新日志。</summary>
    public void CloseChangelog() => IsChangelogOpen = false;

    public Action? ThemeChanged { get; set; }

    // ---------------------------------------------------------------- 选项

    [RelayCommand]
    private void SelectTheme(ChoiceOption? option)
    {
        if (option is null) return;
        Settings.Theme = option.Key;
        ThemeService.Apply(option.Key);
        IsSelected(ThemeOptions, option);
        ThemeChanged?.Invoke();
    }

    [RelayCommand]
    private void SelectFont(ChoiceOption? option)
    {
        if (option is null) return;
        Settings.Font = option.Key;
        FontService.Apply(option.Key);
        IsSelected(FontOptions, option);
    }

    [RelayCommand]
    private void SelectQuality(ChoiceOption? option)
    {
        if (option is null) return;
        Settings.Quality = option.Key;
        IsSelected(QualityOptions, option);
    }

    [RelayCommand]
    private void SelectWindowSize(ChoiceOption? option)
    {
        if (option is null) return;
        Settings.WindowSize = option.Key;
        IsSelected(WindowSizeOptions, option);
    }

    [RelayCommand]
    private void SelectAccent(AccentPreset? option)
    {
        if (option is null) return;
        Settings.AccentPreset = option.Key;
        ThemeService.ApplyAccent(option.Key);
        foreach (var preset in AccentOptions) preset.IsSelected = preset.Key == option.Key;
        Toast = $"页面配色：{option.Label}";
    }

    [RelayCommand]
    private void SelectCloseAction(ChoiceOption? option)
    {
        if (option is null) return;
        Settings.CloseAction = option.Key;
        IsSelected(CloseOptions, option);
    }

    [RelayCommand]
    private void SelectSection(ChoiceOption? option)
    {
        if (option is null) return;
        IsSelected(Sections, option.Key);
        SelectedSection = option.Key;
    }

    [RelayCommand]
    private void SwitchAccount()
    {
        // 不打开外部浏览器：直接切到「我的」页内的扫码登录
        App.Shell.Navigate("Mine");
    }

    [RelayCommand]
    private void AccountAction()
    {
        if (IsLoggedIn) App.Shell.Navigate("Mine");
        else SwitchAccount();
    }

    [RelayCommand]
    private void OpenProjectPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
    }

    /// <summary>开发者首页在软件内浏览器打开，不跳系统浏览器。</summary>
    [RelayCommand]
    private void OpenDeveloperHome()
    {
        App.Shell.OpenLink("https://www.miyoushe.com/planet/accountCenter/postList?id=310621322");
    }

    [RelayCommand]
    private void ChangeDownloadDirectory()
    {
        var current = DownloadDirectory;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择图片与视频的下载位置",
            InitialDirectory = Directory.Exists(current) ? current : AppInfo.DefaultDownloadDirectory,
        };
        if (dialog.ShowDialog() != true) return;

        Settings.DownloadPath = dialog.FolderName;
        OnPropertyChanged(nameof(DownloadDirectory));
        Toast = "已更新下载位置";
    }

    [RelayCommand]
    private void OpenDownloadDirectory()
    {
        try
        {
            Directory.CreateDirectory(DownloadDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", DownloadDirectory) { UseShellExecute = true });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
    }

    [RelayCommand]
    private void Logout()
    {
        Settings.Cookie = string.Empty;
        Settings.Nickname = string.Empty;
        Settings.Avatar = string.Empty;
        _content.RebuildApi();
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(AccountAvatar));
        OnPropertyChanged(nameof(AccountUid));
        OnPropertyChanged(nameof(AccountActionText));
        Toast = Loc.Instance["Mine.Unbind"];
        RefreshAccount();
    }

    // ---------------------------------------------------------------- 维护

    [RelayCommand]
    public void RefreshCacheSize()
    {
        try
        {
            var dir = SettingsService.CacheDirectory;
            if (!Directory.Exists(dir))
            {
                CacheSize = "0 MB";
                return;
            }

            var bytes = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
            CacheSize = bytes > 1024 * 1024
                ? $"{bytes / 1024.0 / 1024.0:0.#} MB"
                : $"{(bytes / 1024.0):0.#} KB";
        }
        catch
        {
            CacheSize = "-";
        }
    }

    [RelayCommand]
    private void ClearCache()
    {
        try
        {
            var dir = SettingsService.CacheDirectory;
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
            }

            RefreshCacheSize();
            Toast = Loc.Instance["S.ClearDone"];
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        IsChecking = true;
        UpdateStatus = Loc.Instance["S.Checking"];
        try
        {
            var found = await UpdateService.PromptIfAvailableAsync(
                System.Windows.Application.Current?.MainWindow, Settings, manual: true);
            UpdateStatus = found ? "已检查到可用更新" : Loc.Instance["S.UpToDate"];
        }
        catch
        {
            UpdateStatus = Loc.Instance["S.UpToDate"];
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void Uninstall()
    {
        var result = System.Windows.MessageBox.Show(
            Loc.Instance["S.UninstallTip"],
            Loc.Instance["S.Uninstall"],
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Information);
        if (result != System.Windows.MessageBoxResult.OK) return;

        var uninstaller = FindUninstaller();
        if (uninstaller is null)
        {
            System.Windows.MessageBox.Show(
                "没有找到安装版卸载程序。当前可能是开发版或便携版，请直接关闭软件后删除所在目录。",
                Loc.Instance["S.Uninstall"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            // 先拉起独立卸载进程，再退出主程序；卸载器内部还会兜底结束残留进程，
            // 这样播放器、WebView2 与托盘窗口都会一起关闭，不会占用待删除的文件。
            Process.Start(new ProcessStartInfo(uninstaller)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(uninstaller),
            });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
            return;
        }

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            System.Windows.Application.Current?.Shutdown();
            Environment.Exit(0);
        }));
    }

    /// <summary>从注册表 InstallDir 或当前运行目录查找安装版卸载程序。</summary>
    private static string? FindUninstaller()
    {
        var candidates = new List<string>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\MikoMiko");
            if (key?.GetValue("InstallDir") is string dir && !string.IsNullOrWhiteSpace(dir))
                candidates.Add(Path.Combine(dir, "Uninstall.exe"));
        }
        catch
        {
            // 注册表读取失败时继续尝试当前目录。
        }
        candidates.Add(Path.Combine(AppInfo.InstallDirectory, "Uninstall.exe"));

        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path)) return path;
            }
            catch
            {
                // 单个候选路径无效时继续下一个。
            }
        }
        return null;
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppInfo.DataDirectory) { UseShellExecute = true });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
    }

    /// <summary>更改数据目录：写入 datadir.txt，重启后生效，不自动迁移旧数据。</summary>
    [RelayCommand]
    private void ChangeDataDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择新的应用数据目录",
            InitialDirectory = Directory.Exists(AppInfo.DataDirectory)
                ? AppInfo.DataDirectory
                : AppInfo.InstallDirectory,
        };
        if (dialog.ShowDialog() != true) return;

        if (SettingsService.SetDataDirectory(dialog.FolderName))
        {
            Toast = "已更改数据目录，重启 MikoMiko 后生效（原有数据不会自动迁移）";
        }
        else
        {
            Toast = "更改失败，请换一个可写的目录";
        }
    }

    [RelayCommand]
    private void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
    }

    [RelayCommand]
    private void OpenInstallFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", AppInfo.InstallDirectory) { UseShellExecute = true });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
    }

    [RelayCommand]
    private void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
    }

    [RelayCommand]
    private void OpenRecentPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.miyoushe.com/") { UseShellExecute = true });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
    }

    // ---------------------------------------------------------------- 辅助

    public void SyncSelections()
    {
        IsSelected(ThemeOptions, Settings.Theme);
        IsSelected(FontOptions, Settings.Font);
        IsSelected(QualityOptions, Settings.Quality);
        IsSelected(WindowSizeOptions, Settings.WindowSize);
        IsSelected(CloseOptions, Settings.CloseAction);

        var accent = ThemeService.FindAccent(Settings.AccentPreset);
        foreach (var preset in AccentOptions) preset.IsSelected = preset.Key == accent.Key;
    }

    private static void IsSelected(IEnumerable<ChoiceOption> options, string key)
    {
        foreach (var option in options)
        {
            option.IsSelected = string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void IsSelected(IEnumerable<ChoiceOption> options, ChoiceOption selected)
        => IsSelected(options, selected.Key);
}
