using MikoMiko.Models;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace MikoMiko.Services;

/// <summary>设置读写与自动保存，支持自定义数据目录（重启后生效）。</summary>
public sealed class SettingsService
{
    private readonly string _filePath;
    private Timer? _saveTimer;
    private bool _needsSave;

    public AppSettings Current { get; }

    public SettingsService()
    {
        DataDirectory = ResolveDataDirectory();
        Directory.CreateDirectory(DataDirectory);
        _filePath = Path.Combine(DataDirectory, "settings.json");
        Current = Load();
        if (_needsSave) Save();
        Current.PropertyChanged += (_, _) => QueueSave();
    }

    /// <summary>当前应用数据目录（settings.json、缓存、WebView2 数据都在这里）。</summary>
    public static string DataDirectory { get; private set; } = DefaultDirectory;

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MikoMiko");

    private static string BootstrapFile => Path.Combine(DefaultDirectory, "datadir.txt");

    /// <summary>读取用户自定义的数据目录（记录在默认目录的 datadir.txt 里）。</summary>
    private static string ResolveDataDirectory()
    {
        try
        {
            if (File.Exists(BootstrapFile))
            {
                var custom = File.ReadAllText(BootstrapFile).Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(custom)) return custom;
            }
        }
        catch
        {
            // 读取失败就用默认目录
        }

        return DefaultDirectory;
    }

    /// <summary>修改数据目录，重启后生效；不自动迁移原有数据。</summary>
    public static bool SetDataDirectory(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            path = Path.GetFullPath(path.Trim().Trim('"'));
            Directory.CreateDirectory(DefaultDirectory);
            File.WriteAllText(BootstrapFile, path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var model = JsonSerializer.Deserialize<AppSettings>(json);
                if (model is not null)
                {
                    // 0.1.41 将默认启动目的地改为启动页；仅迁移一次，之后尊重用户选择。
                    if (!model.LaunchDefaultApplied)
                    {
                        model.StartOnLaunch = true;
                        model.StartOnHome = false;
                        model.StartOnVideo = false;
                        model.LaunchDefaultApplied = true;
                        _needsSave = true;
                    }
                    return model;
                }
            }
        }
        catch
        {
            // 配置损坏时回落到默认设置
        }

        var fresh = new AppSettings { LaunchDefaultApplied = true };
        _needsSave = true;
        return fresh;
    }

    private void QueueSave()
    {
        _saveTimer?.Dispose();
        _saveTimer = new Timer(_ => Save(), null, 600, Timeout.Infinite);
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 忽略写入失败（例如只读目录）
        }
    }

    public string SettingsFilePath => _filePath;

    /// <summary>缓存目录（跟随数据目录）。</summary>
    public static string CacheDirectory => Path.Combine(DataDirectory, "cache");

    /// <summary>视频缓存目录。</summary>
    public static string VideoCacheDirectory => Path.Combine(CacheDirectory, "video");
}
