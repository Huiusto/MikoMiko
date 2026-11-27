using System.IO;

namespace MikoMiko.Services;

/// <summary>缓存自动清理：运行期间每 1 小时清理一次超过 24 小时的过期缓存文件。</summary>
public static class CacheJanitor
{
    private static Timer? _timer;

    public static void Start()
    {
        if (_timer is not null) return;
        _timer = new Timer(_ => Cleanup(), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public static void Cleanup()
    {
        try
        {
            var dir = SettingsService.CacheDirectory;
            if (!Directory.Exists(dir)) return;
            var cutoff = DateTime.Now.AddHours(-24);
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < cutoff) info.Delete();
                }
                catch
                {
                    // 文件正在使用，跳过
                }
            }
        }
        catch
        {
            // 忽略清理失败
        }
    }
}
