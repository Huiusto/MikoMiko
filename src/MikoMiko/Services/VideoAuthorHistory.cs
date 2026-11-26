using System.IO;
using System.Text.Json;

namespace MikoMiko.Services;

/// <summary>
/// 视频随机流的作者冷却记录：同一作者（优先按 UID）展示过后在设定时间内不再推送，
/// 过期记录在启动和写入时自动清理。持久化到应用数据目录，重启后继续生效。
/// </summary>
public sealed class VideoAuthorHistory
{
    private readonly TimeSpan _cooldown;
    private readonly string _filePath;
    private readonly object _gate = new();
    private Dictionary<string, Entry> _entries = new();

    public VideoAuthorHistory(string? filePath = null, TimeSpan? cooldown = null)
    {
        _cooldown = cooldown ?? TimeSpan.FromDays(1);
        _filePath = filePath ?? Path.Combine(SettingsService.DataDirectory, "video-author-history.json");
        Load();
    }

    private sealed class Entry
    {
        public string Key { get; set; } = string.Empty;
        public string Uid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime ShownAtUtc { get; set; }
    }

    /// <summary>作者是否仍在冷却期。空作者不拦截。</summary>
    public bool IsBlocked(string? uid, string? name)
    {
        return IsBlockedKey(KeyOf(uid, name));
    }

    /// <summary>按完整键判断是否仍在冷却期，官方视频用 BVID 作为键。</summary>
    public bool IsBlockedKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_gate)
        {
            return _entries.TryGetValue(key, out var entry) && DateTime.UtcNow - entry.ShownAtUtc < _cooldown;
        }
    }

    /// <summary>记录一次展示；同名作者也会合并到同一个 key。</summary>
    public void Remember(string? uid, string? name)
    {
        RememberKey(KeyOf(uid, name), uid, name);
    }

    /// <summary>按完整键记录一次展示。</summary>
    public void RememberKey(string? key, string? uid = null, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_gate)
        {
            Prune();
            _entries[key] = new Entry
            {
                Key = key,
                Uid = uid?.Trim() ?? string.Empty,
                Name = name?.Trim() ?? string.Empty,
                ShownAtUtc = DateTime.UtcNow,
            };
            Save();
        }
    }

    /// <summary>当前有效记录数（只用于诊断/测试）。</summary>
    public int ActiveCount
    {
        get
        {
            lock (_gate)
            {
                Prune();
                return _entries.Count;
            }
        }
    }

    private static string KeyOf(string? uid, string? name)
    {
        var id = uid?.Trim() ?? string.Empty;
        if (id.Length > 0) return "uid:" + id;
        var nick = name?.Trim() ?? string.Empty;
        return nick.Length > 0 ? "name:" + nick : string.Empty;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var list = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(_filePath));
            if (list is null) return;
            foreach (var entry in list)
            {
                var key = string.IsNullOrWhiteSpace(entry.Key) ? KeyOf(entry.Uid, entry.Name) : entry.Key;
                if (key.Length <= 0) continue;
                entry.Key = key;
                _entries[key] = entry;
            }
            Prune();
            Save();
        }
        catch
        {
            _entries = new Dictionary<string, Entry>();
        }
    }

    private void Prune()
    {
        var changed = false;
        var now = DateTime.UtcNow;
        foreach (var key in _entries.Keys.ToList())
        {
            if (now - _entries[key].ShownAtUtc >= _cooldown)
            {
                _entries.Remove(key);
                changed = true;
            }
        }
        if (changed) Save();
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_entries.Values.OrderByDescending(e => e.ShownAtUtc),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 记录失败不影响视频播放
        }
    }
}
