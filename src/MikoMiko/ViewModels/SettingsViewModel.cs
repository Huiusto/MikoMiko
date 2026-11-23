using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Localization;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

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

        Sections = new ObservableCollection<ChoiceOption>
        {
            new("General", Loc.Instance["S.General"]),
            new("Playback", Loc.Instance["S.Playback"]),
            new("Cache", Loc.Instance["S.Cache"]),
            new("Window", Loc.Instance["S.Window"]),
            new("Common", Loc.Instance["S.Common"]),
            new("Maintain", Loc.Instance["S.Maintain"]),
            new("AppInfo", Loc.Instance["S.AppInfo"]),
            new("About", Loc.Instance["S.About"]),
            new("Disclaimer", Loc.Instance["S.Disclaimer"]),
            new("Changelog", Loc.Instance["S.Changelog"]),
        };
        Sections[0].IsSelected = true;

        ThemeOptions = new ObservableCollection<ChoiceOption>
        {
            new("System", Loc.Instance["S.Theme.System"]),
            new("Light", Loc.Instance["S.Theme.Light"]),
            new("Dark", Loc.Instance["S.Theme.Dark"]),
        };

        LanguageOptions = new ObservableCollection<ChoiceOption>(
            Loc.Supported.Select(l => new ChoiceOption(l.Code, l.Display)));

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
    public ObservableCollection<ChoiceOption> LanguageOptions { get; }
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
        ? (string.IsNullOrWhiteSpace(Settings.Nickname) ? "已登录" : Settings.Nickname)
        : "未登录";

    public string AccountAvatar => IsLoggedIn && !string.IsNullOrWhiteSpace(Settings.Avatar)
        ? Settings.Avatar
        : "pack://application:,,,/Assets/icon.png";

    public string AccountUid => IsLoggedIn ? "已绑定米游社账号" : "扫码登录后同步米游社账号信息";

    /// <summary>账号资料在「我的」页登录后会变化，每次进入设置都重新读取。</summary>
    public void RefreshAccount()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(AccountAvatar));
        OnPropertyChanged(nameof(AccountUid));
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
    public string ChangelogText => Loc.Instance["S.Changelog.Body"];

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Settings.Cookie);

    [ObservableProperty] private string _selectedSection = "General";
    [ObservableProperty] private string _cacheSize = "0 MB";
    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private string _toast = string.Empty;
    [ObservableProperty] private bool _isChangelogOpen;

    [RelayCommand]
    private void ToggleChangelog() => IsChangelogOpen = !IsChangelogOpen;

    public Action? LanguageChanged { get; set; }

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
    private void SelectLanguage(ChoiceOption? option)
    {
        if (option is null) return;
        Settings.Language = option.Key;
        Loc.Instance.SetLanguage(option.Key);
        IsSelected(LanguageOptions, option);
        RefreshLabels();
        LanguageChanged?.Invoke();
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

        try
        {
            Process.Start(new ProcessStartInfo("appwiz.cpl") { UseShellExecute = true });
        }
        catch
        {
            Toast = Loc.Instance["Common.Fail"];
        }
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
        IsSelected(LanguageOptions, Settings.Language);
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

    /// <summary>语言变化后刷新所有选项文案。</summary>
    public void RefreshLabels()
    {
        foreach (var option in ThemeOptions)
        {
            option.Label = option.Key switch
            {
                "System" => Loc.Instance["S.Theme.System"],
                "Light" => Loc.Instance["S.Theme.Light"],
                _ => Loc.Instance["S.Theme.Dark"],
            };
        }

        LanguageOptions[0].Label = "简体中文";
        LanguageOptions[1].Label = "繁體中文";
        LanguageOptions[2].Label = "English";

        for (var i = 0; i < WindowSizeOptions.Count; i++)
        {
            WindowSizeOptions[i].Label = i switch
            {
                0 => Loc.Instance["S.WindowDefault"],
                1 => Loc.Instance["S.WindowSmall"],
                _ => Loc.Instance["S.WindowLarge"],
            };
        }

        for (var i = 0; i < CloseOptions.Count; i++)
        {
            CloseOptions[i].Label = i switch
            {
                0 => Loc.Instance["S.CloseTray"],
                1 => Loc.Instance["S.CloseExit"],
                _ => Loc.Instance["S.CloseAsk"],
            };
        }

        var sectionKeys = new[]
        {
            "S.General", "S.Playback", "S.Cache", "S.Window", "S.Common",
            "S.Maintain", "S.AppInfo", "S.About", "S.Disclaimer", "S.Changelog",
        };
        for (var i = 0; i < Sections.Count && i < sectionKeys.Length; i++)
        {
            Sections[i].Label = Loc.Instance[sectionKeys[i]];
        }

        OnPropertyChanged(nameof(AccountName));
        OnPropertyChanged(nameof(AccountUid));
        OnPropertyChanged(nameof(IsLoggedIn));
    }
}
