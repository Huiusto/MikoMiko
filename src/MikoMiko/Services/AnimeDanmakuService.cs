using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MikoMiko.Services;

/// <summary>一条追番播放器里的弹幕，按作品名与集数共享，不跟随站点变化。</summary>
public sealed class AnimeDanmaku
{
    public string Title { get; set; } = string.Empty;
    public string Episode { get; set; } = "正片";
    public double TimeMs { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 追番播放器弹幕的本地持久化。弹幕按「作品 + 集数」而不是按站点保存，
/// 因此从 PIDEC 切到樱花动漫或换线路后，同一集已经发过的弹幕仍会同步出现。
/// </summary>
public sealed class AnimeDanmakuService
{
    private readonly string _path;
    private readonly List<AnimeDanmaku> _items;
    private static readonly Regex NormalizePattern = new(@"[\s\p{P}\p{S}]+", RegexOptions.Compiled);

    public AnimeDanmakuService(string? path = null)
    {
        _path = path ?? Path.Combine(SettingsService.DataDirectory, "anime-danmaku.json");
        _items = Load();
    }

    public IReadOnlyList<AnimeDanmaku> Get(string title, string episode)
    {
        var key = Key(title, episode);
        return _items
            .Where(item => Key(item.Title, item.Episode) == key)
            .OrderBy(item => item.TimeMs)
            .Take(3000)
            .ToList();
    }

    public AnimeDanmaku Add(string title, string episode, double timeMs, string text)
    {
        var entry = new AnimeDanmaku
        {
            Title = (title ?? string.Empty).Trim(),
            Episode = string.IsNullOrWhiteSpace(episode) ? "正片" : episode.Trim(),
            TimeMs = Math.Max(0, timeMs),
            Text = (text ?? string.Empty).Trim(),
            CreatedAt = DateTime.Now,
        };
        if (entry.Text.Length == 0) return entry;
        if (entry.Text.Length > 60) entry.Text = entry.Text[..60];

        _items.Add(entry);
        if (_items.Count > 6000)
            _items.RemoveRange(0, _items.Count - 6000);
        Save();
        return entry;
    }

    private static string Key(string title, string episode)
        => NormalizePattern.Replace(title ?? string.Empty, string.Empty).ToLowerInvariant()
           + "|" + NormalizePattern.Replace(episode ?? string.Empty, string.Empty).ToLowerInvariant();

    private List<AnimeDanmaku> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<AnimeDanmaku>();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<AnimeDanmaku>>(json) ?? new List<AnimeDanmaku>();
        }
        catch
        {
            return new List<AnimeDanmaku>();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = false }));
            File.Move(temp, _path, true);
        }
        catch
        {
            // 弹幕写入失败不影响播放，下一条会再次尝试。
        }
    }
}
