using CommunityToolkit.Mvvm.ComponentModel;

namespace MikoMiko.Models;

/// <summary>本地持久化设置（%LOCALAPPDATA%\MikoMiko\settings.json）。</summary>
public sealed partial class AppSettings : ObservableObject
{
    // 外观
    [ObservableProperty] private string _theme = "System";        // System / Light / Dark
    [ObservableProperty] private string _language = "zh-CN";
    [ObservableProperty] private string _font = "Microsoft YaHei UI";
    [ObservableProperty] private string _accentPreset = "MikoBlue";
    [ObservableProperty] private bool _roundedWindow = true;

    // 启动 / 关闭
    [ObservableProperty] private bool _autoStart;
    [ObservableProperty] private bool _startOnHome = true;
    [ObservableProperty] private bool _startOnVideo;
    [ObservableProperty] private bool _autoNextVideo = true;
    [ObservableProperty] private bool _autoPlayOnSwipe = true;
    [ObservableProperty] private bool _autoCheckUpdate = true;
    [ObservableProperty] private string _ignoredUpdateVersion = string.Empty;
    [ObservableProperty] private string _profileBackground = string.Empty;
    [ObservableProperty] private bool _rememberLastPage = true;
    [ObservableProperty] private string _lastPage = "Home";
    [ObservableProperty] private string _closeAction = "Ask";     // Tray / Exit / Ask

    /// <summary>启动首页与启动视频页互斥：勾选其一时自动取消另一个。</summary>
    partial void OnStartOnHomeChanged(bool value)
    {
        if (value && StartOnVideo) StartOnVideo = false;
    }

    partial void OnStartOnVideoChanged(bool value)
    {
        if (value && StartOnHome) StartOnHome = false;
    }

    // 窗口
    [ObservableProperty] private double _windowWidth = 1240;
    [ObservableProperty] private double _windowHeight = 800;
    [ObservableProperty] private double _windowLeft = -1;
    [ObservableProperty] private double _windowTop = -1;
    [ObservableProperty] private bool _isMaximized;
    [ObservableProperty] private string _windowSize = "Default";

    // 播放 / 下载
    [ObservableProperty] private string _quality = "720P";
    [ObservableProperty] private bool _autoPlay;
    [ObservableProperty] private bool _showDanmaku = true;
    [ObservableProperty] private bool _mute;
    [ObservableProperty] private int _volume = 60;
    [ObservableProperty] private string _downloadPath = string.Empty;
    [ObservableProperty] private int _downloadThreads = 3;

    // 通用
    [ObservableProperty] private bool _checkUpdateOnStartup = true;
    [ObservableProperty] private bool _enableGlobalHotkey = true;
    [ObservableProperty] private string _hotkey = "Ctrl+Shift+M";

    // 账号 / 内容
    [ObservableProperty] private int _communityId = 2;
    [ObservableProperty] private string _cookie = string.Empty;
    [ObservableProperty] private string _nickname = "";
    [ObservableProperty] private string _avatar = string.Empty;
}

