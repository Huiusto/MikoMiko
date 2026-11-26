using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using MikoMiko.Services;

namespace MikoMiko.Views;

/// <summary>抽卡记录：读取 MikoNest 本地抽卡库，按游戏 / 卡池分列展示五星与保底。</summary>
public partial class GachaView : UserControl
{
    private static readonly (string Key, string Label, Color Color)[] GameTabs =
    {
        ("genshin", "原神", Color.FromRgb(0x4E, 0xA4, 0xDD)),
        ("starrail", "星穹铁道", Color.FromRgb(0x8A, 0x6D, 0xF0)),
        ("zzz", "绝区零", Color.FromRgb(0xFF, 0x8A, 0x3D)),
        ("bh3", "崩坏3", Color.FromRgb(0xD9, 0x6B, 0xC7)),
        ("genshin_beyond", "千星奇域", Color.FromRgb(0x3F, 0x9E, 0x8A)),
    };

    private List<GachaRecord> _records = new();
    private string _game = "genshin";
    private string _uid = "";
    private bool _suppressUid;

    private sealed record UidChoice(string Uid, int Count)
    {
        public override string ToString() => $"{Uid} · {Count} 条";
    }

    private sealed class PoolStat
    {
        public string Name = "";
        public int Total;
        public int FiveCount;
        public int Pity;
        public DateTime Latest;
        public List<(string Name, int Pity, DateTime Time)> FiveRows = new();
    }

    public GachaView()
    {
        InitializeComponent();
        Loaded += (_, _) => ReloadData();
        PoolScroll.PreviewMouseWheel += PoolScroll_PreviewMouseWheel;
        foreach (var button in GameTabsPanel.Children.OfType<Button>())
            button.Click += (_, _) => SelectGame(button.Tag as string ?? "genshin");
        UpdateGameTabs();
    }

    private void ReloadData()
    {
        _records = MikoNestBridge.LoadGachaRecords().ToList();
        if (_records.Count == 0)
        {
            PoolPanel.Children.Clear();
            EmptyPanel.Visibility = Visibility.Visible;
            TxtEmptyTitle.Text = "还没有本地抽卡记录";
            TxtEmptyDesc.Text = "在 MikoNest 中更新一次抽卡记录后，这里会自动同步显示。\n记录文件位于：" +
                                Path.Combine(MikoNestBridge.DataDir, "gacha_records.json");
            TxtSubtitle.Text = "本地库 0 条 · 等待 MikoNest 抽卡数据";
            ComboUid.ItemsSource = null;
            return;
        }

        if (!_records.Any(r => r.GameKey == _game))
        {
            var firstWithData = _records
                .Select(r => r.GameKey)
                .FirstOrDefault(k => GameTabs.Any(t => t.Key == k));
            if (firstWithData is not null) _game = firstWithData;
        }

        SelectGame(_game);
    }

    private void SelectGame(string key)
    {
        _game = key;
        UpdateGameTabs();

        var gameRecords = _records.Where(r => r.GameKey == key).ToList();
        var background = GameTabs.First(t => t.Key == key).Color;

        var uids = gameRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Uid))
            .GroupBy(r => r.Uid)
            .Select(g => new UidChoice(g.Key, g.Count()))
            .OrderByDescending(g => g.Count)
            .ToList();

        _suppressUid = true;
        ComboUid.ItemsSource = uids;
        if (uids.Count == 0)
        {
            _uid = "";
            ComboUid.SelectedIndex = -1;
        }
        else
        {
            var activeUid = MikoNestBridge.ActiveAccount?.Uid;
            var choice = uids.FirstOrDefault(u => u.Uid == activeUid) ??
                         uids.FirstOrDefault(u => u.Uid == _uid) ?? uids[0];
            _uid = choice.Uid;
            ComboUid.SelectedItem = choice;
        }
        _suppressUid = false;

        RenderPools(gameRecords, background);
    }

    private void UpdateGameTabs()
    {
        foreach (var button in GameTabsPanel.Children.OfType<Button>())
        {
            var selected = (button.Tag as string) == _game;
            button.Background = selected
                ? new SolidColorBrush(Color.FromRgb(0xE8, 0xF4, 0xFC))
                : (Brush)FindResource("Brush.Surface");
            button.BorderBrush = selected
                ? (Brush)FindResource("Brush.Accent")
                : (Brush)FindResource("Brush.Border");
            button.Foreground = selected
                ? (Brush)FindResource("Brush.Accent")
                : (Brush)FindResource("Brush.TextSecondary");
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void RenderPools(List<GachaRecord> gameRecords, Color accent)
    {
        var filtered = string.IsNullOrEmpty(_uid) ? gameRecords : gameRecords.Where(r => r.Uid == _uid).ToList();
        PoolPanel.Children.Clear();

        if (filtered.Count == 0)
        {
            EmptyPanel.Visibility = Visibility.Visible;
            EmptyPanel.Width = double.NaN;
            TxtEmptyTitle.Text = "这个账号还没有记录";
            TxtEmptyDesc.Text = "切换上方 UID，或在 MikoNest 中更新抽卡记录后再刷新。";
            TxtSubtitle.Text = $"本地库 {_records.Count:N0} 条 · 当前账号 0 条";
            return;
        }

        EmptyPanel.Visibility = Visibility.Collapsed;
        var gameName = GameTabs.First(t => t.Key == _game).Label;
        TxtSubtitle.Text = $"本地库 {_records.Count:N0} 条 · {gameName} {filtered.Count:N0} 条 · 数据来自 MikoNest";

        var pools = BuildStats(filtered, _game == "genshin_beyond" ? "4" : "5")
            .OrderByDescending(p => p.Latest)
            .ThenByDescending(p => p.Total)
            .ToList();

        for (var i = 0; i < pools.Count; i++)
            PoolPanel.Children.Add(BuildPoolCard(pools[i], i % 2 == 0 ? accent : Lighten(accent, 0.18)));
    }

    private static List<PoolStat> BuildStats(List<GachaRecord> records, string topRank)
    {
        var result = new List<PoolStat>();
        foreach (var group in records.GroupBy(r => string.IsNullOrWhiteSpace(r.PoolName) ? "未命名卡池" : r.PoolName))
        {
            var stat = new PoolStat { Name = group.Key };
            var ordered = group
                .OrderBy(r => ParseTime(r.Time))
                .ThenBy(r => r.Id, StringComparer.Ordinal)
                .ToList();
            foreach (var record in ordered)
            {
                stat.Total++;
                stat.Pity++;
                var time = ParseTime(record.Time);
                if (time > stat.Latest) stat.Latest = time;
                if (record.RankType == topRank)
                {
                    stat.FiveCount++;
                    stat.FiveRows.Insert(0, (record.Name, stat.Pity, time));
                    stat.Pity = 0;
                }
            }
            result.Add(stat);
        }
        return result;
    }

    private Border BuildPoolCard(PoolStat stat, Color accent)
    {
        var card = new Border
        {
            Width = 292,
            Margin = new Thickness(0, 0, 14, 8),
            CornerRadius = new CornerRadius(16),
            Background = (Brush)FindResource("Brush.Card"),
            BorderBrush = (Brush)FindResource("Brush.Border"),
            BorderThickness = new Thickness(1),
        };
        var root = new StackPanel();
        card.Child = root;

        root.Children.Add(new Border
        {
            Height = 4,
            Background = new SolidColorBrush(accent),
            CornerRadius = new CornerRadius(16, 16, 0, 0),
        });

        var body = new StackPanel { Margin = new Thickness(17, 15, 17, 16) };
        root.Children.Add(body);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = stat.Name,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Brush.TextPrimary"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 190,
        };
        Grid.SetColumn(title, 0);
        header.Children.Add(title);
        var count = new Border
        {
            Background = (Brush)FindResource("Brush.Surface"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = System.Windows.VerticalAlignment.Top,
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = $"{stat.Total} 抽",
                FontSize = 10.5,
                Foreground = (Brush)FindResource("Brush.TextSecondary"),
            },
        };
        Grid.SetColumn(count, 1);
        header.Children.Add(count);
        body.Children.Add(header);

        var stats = new Grid { Margin = new Thickness(0, 16, 0, 12) };
        var topLabel = _game == "genshin_beyond" ? "四星" : "五星";
        for (var i = 0; i < 3; i++) stats.ColumnDefinitions.Add(new ColumnDefinition());
        AddStat(stats, 0, topLabel, stat.FiveCount.ToString(), accent);
        AddStat(stats, 1, "平均", stat.FiveCount > 0 ? $"{stat.Total / (double)stat.FiveCount:0.0} 抽" : "—",
            (Brush)FindResource("Brush.TextPrimary"));
        AddStat(stats, 2, "出货率", stat.Total > 0 ? $"{stat.FiveCount * 100d / stat.Total:0.0}%" : "—",
            (Brush)FindResource("Brush.TextPrimary"));
        body.Children.Add(stats);

        var target = _game == "bh3" ? 100 : 90;
        var bar = new Grid { Height = 5, Margin = new Thickness(0, 2, 0, 7) };
        bar.Children.Add(new Border
        {
            Background = (Brush)FindResource("Brush.Surface"),
            CornerRadius = new CornerRadius(3),
        });
        var fillWidth = 258 * Math.Clamp(stat.Pity / (double)target, 0, 1);
        bar.Children.Add(new Border
        {
            Background = new SolidColorBrush(accent),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Width = Math.Max(2, fillWidth),
        });
        body.Children.Add(bar);
        body.Children.Add(new TextBlock
        {
            Text = $"当前保底 {stat.Pity} / {target}",
            FontSize = 11,
            Foreground = (Brush)FindResource("Brush.TextSecondary"),
            Margin = new Thickness(0, 0, 0, 13),
        });
        body.Children.Add(new Border
        {
            Height = 1,
            Background = (Brush)FindResource("Brush.BorderSoft"),
            Margin = new Thickness(0, 0, 0, 11),
        });

        var recentTitle = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        recentTitle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        recentTitle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock
        {
            Text = "最近" + topLabel,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Brush.TextPrimary"),
        };
        Grid.SetColumn(label, 0);
        recentTitle.Children.Add(label);
        var fiveLabel = new TextBlock
        {
            Text = $"共 {stat.FiveCount} 个",
            FontSize = 10.5,
            Foreground = (Brush)FindResource("Brush.TextTertiary"),
        };
        Grid.SetColumn(fiveLabel, 1);
        recentTitle.Children.Add(fiveLabel);
        body.Children.Add(recentTitle);

        var list = new StackPanel();
        if (stat.FiveRows.Count == 0)
        {
            list.Children.Add(new TextBlock
            {
                Text = "这个卡池还没有" + topLabel + "记录",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("Brush.TextTertiary"),
                Margin = new Thickness(0, 8, 0, 4),
            });
        }
        else
        {
            foreach (var row in stat.FiveRows.Take(6))
                list.Children.Add(BuildFiveRow(row.Name, row.Pity, row.Time, accent));
        }
        body.Children.Add(list);

        return card;
    }

    private void AddStat(Grid grid, int column, string title, string value, object color)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            Foreground = (Brush)FindResource("Brush.TextTertiary"),
        });
        panel.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = color as Brush ?? new SolidColorBrush((Color)color),
            Margin = new Thickness(0, 3, 0, 0),
        });
        Grid.SetColumn(panel, column);
        grid.Children.Add(panel);
    }

    private Border BuildFiveRow(string name, int pity, DateTime time, Color accent)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = "★",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        });
        var text = new TextBlock
        {
            Text = name,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("Brush.TextPrimary"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var meta = new TextBlock
        {
            Text = $"{pity} 抽 · {time:MM-dd}",
            FontSize = 10,
            Foreground = new SolidColorBrush(accent),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        Grid.SetColumn(meta, 2);
        grid.Children.Add(meta);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x0F, accent.R, accent.G, accent.B)),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(0, 0, 0, 4),
            Child = grid,
        };
    }

    private static DateTime ParseTime(string text)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : DateTime.MinValue;

    private static Color Lighten(Color color, double amount)
        => Color.FromRgb(
            (byte)Math.Min(255, color.R + (255 - color.R) * amount),
            (byte)Math.Min(255, color.G + (255 - color.G) * amount),
            (byte)Math.Min(255, color.B + (255 - color.B) * amount));

    private void GameTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key }) SelectGame(key);
    }

    private void BtnReload_Click(object sender, RoutedEventArgs e)
    {
        MikoNestBridge.Reload();
        ReloadData();
    }

    private void ComboUid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUid || ComboUid.SelectedItem is not UidChoice choice) return;
        _uid = choice.Uid;
        var gameRecords = _records.Where(r => r.GameKey == _game).ToList();
        RenderPools(gameRecords, GameTabs.First(t => t.Key == _game).Color);
    }

    private void PoolScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0) return;
        PoolScroll.ScrollToHorizontalOffset(PoolScroll.HorizontalOffset - e.Delta);
        e.Handled = true;
    }
}


