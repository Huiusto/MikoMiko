using System.IO;

namespace MikoMiko.Services;

/// <summary>缓存自动清理：每 5 分钟清掉 5 分钟以前写入的缓存文件，避免缓存无限增长。</summary>
public static class CacheJanitor
{
    private static Timer? _timer;

    public static void Start()
    {
        if (_timer is not null) return;
        _timer = new Timer(_ => Cleanup(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public static void Cleanup()
    {
        try
        {
            var dir = SettingsService.CacheDirectory;
            if (!Directory.Exists(dir)) return;
            var cutoff = DateTime.Now.AddMinutes(-5);
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
