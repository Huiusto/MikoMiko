using System.IO;
using System.Text.Json;

namespace MikoMiko.Services;

/// <summary>「我的追剧」里保存的一部作品。</summary>
public sealed class AnimeFollow
{
    public string Title { get; set; } = string.Empty;
    public string Poster { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string PlayUrl { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Kind { get; set; } = "动漫";
    public string Region { get; set; } = "其他";
    public DateTime AddedAt { get; set; } = DateTime.Now;

    public AnimeItem ToItem() => new()
    {
        Title = Title,
        Poster = Poster,
        Url = Url,
        PlayUrl = PlayUrl,
        Source = Source,
        Note = Note,
        Kind = Kind,
        Region = Region,
    };
}

/// <summary>
/// 追剧收藏的本地持久化。只在软件内保存作品入口，不涉及任何账号数据。
/// 播放器点击「追剧」后立即写入，追番页的「我的追剧」同步刷新。
/// </summary>
public sealed class AnimeFollowService
{
    private readonly string _path;
    private readonly List<AnimeFollow> _items;

    public AnimeFollowService(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsService.DataDirectory, "anime-follows.json");
        _items = Load();
    }

    public event Action? Changed;

    public IReadOnlyList<AnimeFollow> Items => _items;

    public bool Contains(string? url)
        => !string.IsNullOrWhiteSpace(url) &&
           _items.Any(i => string.Equals(i.Url, url, StringComparison.OrdinalIgnoreCase));

    /// <summary>切换追剧状态，返回切换后的状态。</summary>
    public bool Toggle(AnimePlayback? playback)
    {
        var item = playback?.CatalogItem;
        if (item is null || string.IsNullOrWhiteSpace(item.Url)) return false;

        var index = _items.FindIndex(i => string.Equals(i.Url, item.Url, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _items.RemoveAt(index);
        }
        else
        {
            _items.Insert(0, new AnimeFollow
            {
                Title = item.Title,
                Poster = item.Poster,
                Url = item.Url,
                PlayUrl = item.PlayUrl,
                Source = item.Source,
                Note = item.Note,
                Kind = item.Kind,
                Region = item.Region,
                AddedAt = DateTime.Now,
            });
        }

        Save();
        Changed?.Invoke();
        return index < 0;
    }

    public void Remove(string url)
    {
        var index = _items.FindIndex(i => string.Equals(i.Url, url, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;
        _items.RemoveAt(index);
        Save();
        Changed?.Invoke();
    }

    private List<AnimeFollow> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<AnimeFollow>();
            var json = File.ReadAllText(_path);
            var items = JsonSerializer.Deserialize<List<AnimeFollow>>(json) ?? new List<AnimeFollow>();
            return items
                .Where(i => !string.IsNullOrWhiteSpace(i.Title) && !string.IsNullOrWhiteSpace(i.Url))
                .OrderByDescending(i => i.AddedAt)
                .ToList();
        }
        catch
        {
            return new List<AnimeFollow>();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_items, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
            File.Move(temp, _path, true);
        }
        catch
        {
            // 收藏写入失败不影响播放，下一次点击会重试。
        }
    }
}
