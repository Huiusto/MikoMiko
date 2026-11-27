namespace MikoMiko.Services;

/// <summary>应用元信息。</summary>
public static class AppInfo
{
    public const string Name = "MikoMiko";
    public const string Version = "0.1.55";
    public const string RepositoryUrl = "https://github.com/Huiusto/MikoMiko";
    public const string ReleasesApi = "https://api.github.com/repos/Huiusto/MikoMiko/releases/latest";
    public const string License = "MIT License";

    public static string InstallDirectory => AppContext.BaseDirectory;

    /// <summary>数据目录（settings.json / cache / WebView2 登录态）。</summary>
    public static string DataDirectory => SettingsService.DataDirectory;

    /// <summary>默认下载目录：软件安装目录下的 Downloads。</summary>
    public static string DefaultDownloadDirectory => System.IO.Path.Combine(
        InstallDirectory, "Downloads");

    public static string RuntimeDescription
    {
        get
        {
            var version = Environment.Version;
            return $".NET {version.Major}.{version.Minor} · WPF · WebView2 · {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}";
        }
    }
}







