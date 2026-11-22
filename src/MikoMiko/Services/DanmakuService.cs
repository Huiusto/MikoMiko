using System.IO;
using System.Text.Json;

namespace MikoMiko.Services;

/// <summary>本地弹幕库：按视频帖保存，数据在应用数据目录内，同一台电脑上的 MikoMiko 共享。</summary>
public sealed class DanmakuService
{
    public sealed class Entry
    {
        public string PostId { get; set; } = string.Empty;
        public double Time { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public long CreatedAt { get; set; }
    }

    private readonly string _file = Path.Combine(SettingsService.DataDirectory, "danmaku.json");
    private readonly object _gate = new();
    private Dictionary<string, List<Entry>> _items;

    public DanmakuService()
    {
        _items = Load();
    }

    public IReadOnlyList<Entry> Get(string postId, double time, double tolerance = 3)
    {
        lock (_gate)
        {
            var list = _items.TryGetValue(postId, out var entries) ? entries : new List<Entry>();
            return list.Where(e => Math.Abs(e.Time - time) <= tolerance).ToList();
        }
    }

    public void Add(Entry entry)
    {
        lock (_gate)
        {
            var list = _items.TryGetValue(entry.PostId, out var entries)
                ? entries
                : _items[entry.PostId] = new List<Entry>();
            list.Add(entry);
            Save();
        }
    }

    private Dictionary<string, List<Entry>> Load()
    {
        try
        {
            if (!File.Exists(_file)) return new Dictionary<string, List<Entry>>();
            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<Dictionary<string, List<Entry>>>(json) ??
                   new Dictionary<string, List<Entry>>();
        }
        catch
        {
            return new Dictionary<string, List<Entry>>();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_items));
            File.Move(tmp, _file, overwrite: true);
        }
        catch
        {
            // 弹幕落库失败不阻断播放
        }
    }
}
