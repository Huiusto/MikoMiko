using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MikoMiko.Services;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace MikoMiko.Views;

/// <summary>抽卡记录：按固定卡池列展示数量、平均、出货率、保底与最近记录。</summary>
public partial class GachaView : UserControl
{
    private sealed record GameChoice(string Key, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record UidChoice(string Uid, string Label, int Count)
    {
        public override string ToString() => Label;
    }

    private sealed class PoolDef
    {
        public required string Name;
        public string[] Types = Array.Empty<string>();
        public string[] PoolNames = Array.Empty<string>();
    }

    private sealed class PoolStat
    {
        public string Name = "";
        public int Total;
        public int TopCount;
        public int SecondCount;
        public int ThirdCount;
        public int Pity;
        public int SecondPity;
        public DateTime Earliest;
        public DateTime Latest;
        public List<(string Name, int Pity, DateTime Time)> TopRows = new();
        public List<(string Name, int Pity, DateTime Time)> SecondRows = new();
        public double Average => TopCount > 0 ? Total / (double)TopCount : 0;
        public double TopRate => Total > 0 ? TopCount * 100d / Total : 0;
        public double SecondRate => Total > 0 ? SecondCount * 100d / Total : 0;
        public double ThirdRate => Total > 0 ? ThirdCount * 100d / Total : 0;
    }

    private static readonly GameChoice[] Games =
    {
        new("genshin", "原神"),
        new("starrail", "星穹铁道"),
        new("zzz", "绝区零"),
        new("bh3", "崩坏3"),
    };

    private static readonly Dictionary<string, PoolDef[]> PoolDefs = new()
    {
        ["genshin"] = new[]
        {
            new PoolDef { Name = "常驻祈愿", Types = new[] { "200" }, PoolNames = new[] { "常驻" } },
            new PoolDef { Name = "角色活动祈愿", Types = new[] { "301", "400" }, PoolNames = new[] { "角色活动", "角色祈愿" } },
            new PoolDef { Name = "武器活动祈愿", Types = new[] { "302" }, PoolNames = new[] { "武器活动", "武器祈愿" } },
            new PoolDef { Name = "集录祈愿", Types = new[] { "500" }, PoolNames = new[] { "集录" } },
        },
        ["genshin_beyond"] = new[]
        {
            new PoolDef { Name = "常驻颂愿", Types = new[] { "1000" }, PoolNames = new[] { "常驻颂愿" } },
            new PoolDef { Name = "活动颂愿", Types = new[] { "2000" }, PoolNames = new[] { "活动颂愿" } },
        },
        ["starrail"] = new[]
        {
            new PoolDef { Name = "常驻跃迁", Types = new[] { "1" }, PoolNames = new[] { "常驻" } },
            new PoolDef { Name = "角色活动跃迁", Types = new[] { "11" }, PoolNames = new[] { "角色" } },
            new PoolDef { Name = "光锥活动跃迁", Types = new[] { "12" }, PoolNames = new[] { "光锥" } },
            new PoolDef { Name = "联动跃迁", Types = new[] { "21" }, PoolNames = new[] { "联动", "合作" } },
        },
        ["zzz"] = new[]
        {
            new PoolDef { Name = "常驻频段", Types = new[] { "1" }, PoolNames = new[] { "常驻" } },
            new PoolDef { Name = "独家频段", Types = new[] { "2" }, PoolNames = new[] { "独家", "角色" } },
            new PoolDef { Name = "音擎频段", Types = new[] { "3" }, PoolNames = new[] { "音擎", "武器" } },
            new PoolDef { Name = "邦布频段", Types = new[] { "5" }, PoolNames = new[] { "邦布" } },
        },
        ["bh3"] = new[]
        {
            new PoolDef { Name = "家园补给", Types = new[] { "1" }, PoolNames = new[] { "家园" } },
            new PoolDef { Name = "精准补给", Types = new[] { "2" }, PoolNames = new[] { "精准" } },
            new PoolDef { Name = "扩充补给", Types = new[] { "3" }, PoolNames = new[] { "扩充" } },
            new PoolDef { Name = "活动补给", Types = new[] { "4" }, PoolNames = new[] { "活动" } },
        },
    };

    private readonly Dictionary<string, bool> _rankFilter = new();
    private List<GachaRecord> _records = new();
    private string _game = "genshin";
    private string _uid = "";
    private bool _beyond;
    private bool _show4 = true;
    private bool _showIcon = true;
    private bool _suppressGame;
    private bool _suppressUid;
    private DispatcherTimer? _toastTimer;

    public GachaView()
    {
        InitializeComponent();
        ComboGame.ItemsSource = Games;
        ComboGame.SelectedIndex = 0;
        Loaded += (_, _) => ReloadData();
    }

    private void ReloadData()
    {
        MikoNestBridge.Reload();
        _records = MikoNestBridge.LoadGachaRecords().ToList();

        if (_game != "genshin_beyond")
        {
            var hasCurrent = _records.Any(r => r.GameKey == _game);
            if (!hasCurrent)
            {
                var firstNormal = Games.FirstOrDefault(g => _records.Any(r => r.GameKey == g.Key));
                if (firstNormal is not null)
                {
                    _game = firstNormal.Key;
                    _beyond = false;
                }
                else if (_records.Any(r => r.GameKey == "genshin_beyond"))
                {
                    _game = "genshin_beyond";
                    _beyond = true;
                }
            }
        }

        _suppressGame = true;
        ComboGame.SelectedItem = Games.FirstOrDefault(g => g.Key == (CurrentBaseGame));
        _suppressGame = false;

        UpdateBeyondBadge();
        RefreshUids();
        Render();
    }

    private string CurrentBaseGame => _game == "genshin_beyond" ? "genshin" : _game;

    private void UpdateBeyondBadge()
    {
        BtnBeyond.Visibility = CurrentBaseGame == "genshin" ? Visibility.Visible : Visibility.Collapsed;
        BtnBeyond.Background = new SolidColorBrush(_beyond
            ? Color.FromRgb(0x25, 0x8D, 0xD1)
            : Color.FromRgb(0xEA, 0xF7, 0xFF));
        BtnBeyond.BorderBrush = new SolidColorBrush(_beyond
            ? Color.FromRgb(0x25, 0x8D, 0xD1)
            : Color.FromRgb(0xCB, 0xE8, 0xFA));
        BtnBeyond.Foreground = new SolidColorBrush(_beyond
            ? Color.FromRgb(0xFF, 0xFF, 0xFF)
            : Color.FromRgb(0x2A, 0x78, 0xA8));
    }

    private void RefreshUids()
    {
        var gameRecords = _records.Where(r => r.GameKey == _game).ToList();
        var choices = new List<UidChoice>();
        foreach (var group in gameRecords
                     .Where(r => !string.IsNullOrWhiteSpace(r.Uid))
                     .GroupBy(r => r.Uid)
                     .OrderByDescending(g => g.Count()))
        {
            choices.Add(new UidChoice(group.Key, $"{group.Key} · {group.Count()} 条", group.Count()));
        }

        if (gameRecords.Count == 0)
        {
            foreach (var account in HoyoAccountService.All.Where(a => !string.IsNullOrWhiteSpace(a.Uid)))
            {
                if (choices.Any(c => c.Uid == account.Uid)) continue;
                choices.Add(new UidChoice(account.Uid, $"{account.Uid} · 无记录", 0));
            }
        }

        if (choices.Count == 0)
            choices.Add(new UidChoice("", "无账号", 0));

        _suppressUid = true;
        ComboUid.ItemsSource = choices;
        var selected = choices.FirstOrDefault(c => !string.IsNullOrEmpty(_uid) && c.Uid == _uid);
        if (selected is null)
        {
            selected = choices.OrderByDescending(c => c.Count).First();
            _uid = selected.Uid;
        }
        ComboUid.SelectedItem = selected;
        _suppressUid = false;
    }

    private void Render()
    {
        var poolDefs = PoolDefs[_game];
        var gameRecords = _records.Where(r => r.GameKey == _game &&
                                               (string.IsNullOrEmpty(_uid) || r.Uid == _uid)).ToList();
        var label = _game == "genshin_beyond" ? "千星奇域" : Games.First(g => g.Key == _game).Name;
        TxtHeaderTitle.Text = _game switch
        {
            "starrail" => "跃迁记录",
            "zzz" => "调频记录",
            "bh3" => "补给记录",
            "genshin_beyond" => "颂愿记录",
            _ => "祈愿记录",
        };
        TxtSubtitle.Text = $"本地库 {_records.Count:N0} 条 · {label} {gameRecords.Count:N0} 条";
        TxtDataDir.Text = "数据目录：" + MikoNestBridge.DataDir;
        TxtStatus.Text = _records.Count == 0
            ? "本地库为空 · 等待从 MikoNest 同步记录"
            : gameRecords.Count == 0
                ? $"UID {(_uid.Length == 0 ? "—" : _uid)} 暂无{label}记录"
                : $"UID {_uid} · {label} {gameRecords.Count:N0} 条 · 最近 {gameRecords.Max(r => ParseTime(r.Time)):yyyy/MM/dd HH:mm}";

        var stats = new List<(PoolDef Def, PoolStat Stat)>();
        var matched = new HashSet<GachaRecord>();
        foreach (var def in poolDefs)
        {
            var records = gameRecords.Where(r => MatchPool(r, def)).ToList();
            foreach (var record in records) matched.Add(record);
            var stat = BuildStat(def.Name, records, TopRank);
            stats.Add((def, stat));
        }

        var leftovers = gameRecords.Where(r => !matched.Contains(r)).ToList();
        if (leftovers.Count > 0)
            stats.Add((new PoolDef { Name = "其他祈愿" }, BuildStat("其他祈愿", leftovers, TopRank)));

        var totalPulls = stats.Sum(p => p.Stat.Total);
        var topPulls = stats.Sum(p => p.Stat.TopCount);

        PoolGrid.Children.Clear();
        PoolGrid.ColumnDefinitions.Clear();
        for (var i = 0; i < stats.Count; i++)
            PoolGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PoolColumnWidth(stats.Count)) });

        for (var i = 0; i < stats.Count; i++)
        {
            var card = BuildPoolCard(stats[i].Stat, i);
            Grid.SetColumn(card, i);
            PoolGrid.Children.Add(card);
        }
    }

    /// <summary>与 MikoNest 抽卡页一致：每列固定 262，横向滚动。</summary>
    private static double PoolColumnWidth(int count) => 262;

    private int TopRank => _game is "genshin" or "starrail" ? 5 : 4;

    private int PityTarget => _game switch
    {
        "bh3" => 100,
        "zzz" => 80,
        _ => 90,
    };

    private static bool MatchPool(GachaRecord record, PoolDef def)
    {
        if (!string.IsNullOrWhiteSpace(record.GachaType) && def.Types.Contains(record.GachaType.Trim()))
            return true;
        if (string.IsNullOrWhiteSpace(record.GachaType) && !string.IsNullOrWhiteSpace(record.PoolName))
            return def.PoolNames.Any(name => record.PoolName.Contains(name, StringComparison.Ordinal));
        return false;
    }

    private static PoolStat BuildStat(string name, List<GachaRecord> records, int topRank)
    {
        var stat = new PoolStat { Name = name };
        var ordered = records
            .OrderBy(r => r.Uid, StringComparer.Ordinal)
            .ThenBy(r => ParseTime(r.Time))
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
        var lastUid = "";
        foreach (var record in ordered)
        {
            if (record.Uid != lastUid)
            {
                lastUid = record.Uid;
                stat.Pity = 0;
                stat.SecondPity = 0;
            }
            stat.Total++;
            stat.Pity++;
            stat.SecondPity++;
            var time = ParseTime(record.Time);
            if (time > stat.Latest) stat.Latest = time;
            if (stat.Earliest == DateTime.MinValue && time > DateTime.MinValue) stat.Earliest = time;
            if (!int.TryParse(record.RankType, out var rank)) continue;
            if (rank == topRank)
            {
                stat.TopCount++;
                stat.TopRows.Insert(0, (record.Name, stat.Pity, time));
                stat.Pity = 0;
                stat.SecondPity = 0;
            }
            else if (rank == topRank - 1)
            {
                stat.SecondCount++;
                stat.SecondRows.Insert(0, (record.Name, stat.SecondPity, time));
                stat.SecondPity = 0;
            }
            else if (rank == topRank - 2)
            {
                stat.ThirdCount++;
            }
        }
        return stat;
    }

    private Border BuildPoolCard(PoolStat stat, int index)
    {
        var accent = _game switch
        {
            "starrail" => Color.FromRgb(0x7C, 0x86, 0xD9),
            "zzz" => Color.FromRgb(0xE0, 0xA9, 0x3B),
            "bh3" => Color.FromRgb(0xC7, 0x7D, 0xDB),
            "genshin_beyond" => Color.FromRgb(0xD9, 0xA9, 0x3C),
            _ => Color.FromRgb(0x56, 0xB3, 0xD6),
        };
        var topColor = Color.FromRgb(0xD9, 0xA2, 0x4A);
        var secondColor = Color.FromRgb(0xA4, 0x7B, 0xD0);
        var thirdColor = Color.FromRgb(0x7C, 0x93, 0xB0);
        var topLabel = $"{TopRank}★";
        var secondLabel = $"{TopRank - 1}★";
        var thirdLabel = $"{TopRank - 2}★";

        var card = new Border
        {
            Margin = new Thickness(0, 0, 10, 0),
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE9, 0xF1)),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Padding = new Thickness(0),
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 顶部 accent 线 + 淡彩 wash（仿 MikoNest 卡池卡）
        var topLine = new Border
        {
            Height = 3,
            Background = new SolidColorBrush(accent),
            CornerRadius = new CornerRadius(16, 16, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        root.Children.Add(topLine);

        var wash = new Border
        {
            Height = 96,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(16, 16, 0, 0),
            Background = new LinearGradientBrush(
                Color.FromArgb(0x26, accent.R, accent.G, accent.B),
                Color.FromArgb(0x00, accent.R, accent.G, accent.B),
                new Point(0, 0), new Point(0, 1)),
            IsHitTestVisible = false,
        };
        root.Children.Add(wash);
        card.Child = root;

        var body = new Grid { Margin = new Thickness(16, 14, 16, 12) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        // 标题 + 总抽数
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = stat.Name,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x36)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(title, 0);
        header.Children.Add(title);
        var count = new TextBlock
        {
            Text = stat.Total.ToString(),
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x63, 0x6D)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Grid.SetColumn(count, 1);
        header.Children.Add(count);
        Grid.SetRow(header, 0);
        body.Children.Add(header);

        var period = stat.Earliest > DateTime.MinValue
            ? $"{stat.Earliest:yyyy/MM/dd HH:mm} - {stat.Latest:yyyy/MM/dd HH:mm}"
            : "— —";
        var periodText = new TextBlock
        {
            Text = period,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9B, 0xA3, 0xAD)),
            Margin = new Thickness(0, 5, 0, 0),
        };
        Grid.SetRow(periodText, 1);
        body.Children.Add(periodText);

        // 四行统计
        var statsPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 9) };
        AddStatRow(statsPanel, $"{topLabel} 平均",
            stat.TopCount > 0 ? $"{stat.Average:0.00}" : "—", "", new SolidColorBrush(topColor), true);
        AddStatRow(statsPanel, $"{topLabel} 统计", stat.TopCount.ToString(),
            $"[{stat.TopRate:0.00}%]", new SolidColorBrush(topColor), false);
        AddStatRow(statsPanel, $"{secondLabel} 统计", stat.SecondCount.ToString(),
            $"[{stat.SecondRate:0.00}%]", new SolidColorBrush(secondColor), false);
        AddStatRow(statsPanel, $"{thirdLabel} 统计", stat.ThirdCount.ToString(),
            $"[{stat.ThirdRate:0.00}%]", new SolidColorBrush(thirdColor), false);
        Grid.SetRow(statsPanel, 2);
        body.Children.Add(statsPanel);

        // 5★ / 4★ 切换（隐藏 4★ 时只保留 5★）
        var topSelected = !_show4 ||
                         !_rankFilter.TryGetValue(_game + ":" + stat.Name, out var filter) || filter;
        var segmented = new Grid { Margin = new Thickness(0, 0, 0, 8), Height = 28 };
        segmented.ColumnDefinitions.Add(new ColumnDefinition { Width = _show4 ? new GridLength(1, GridUnitType.Star) : new GridLength(0) });
        segmented.ColumnDefinitions.Add(new ColumnDefinition { Width = _show4 ? new GridLength(1, GridUnitType.Star) : new GridLength(0) });
        segmented.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF7, 0xFB)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE7, 0xF0)),
            BorderThickness = new Thickness(1),
        });
        var topBtn = new Button
        {
            Content = topLabel,
            Background = topSelected ? Brushes.White : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = topSelected ? new SolidColorBrush(Color.FromRgb(0x2B, 0x8B, 0xC8)) : (Brush)FindResource("Brush.TextTertiary"),
            FontWeight = topSelected ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = 11,
            Cursor = Cursors.Hand,
            Height = 28,
            Margin = new Thickness(2, 0, 0, 0),
        };
        topBtn.Template = SegmentTemplate();
        topBtn.Click += (_, _) =>
        {
            _rankFilter[_game + ":" + stat.Name] = true;
            Render();
        };
        Grid.SetColumn(topBtn, 0);
        segmented.Children.Add(topBtn);
        var secondBtn = new Button
        {
            Content = secondLabel,
            Background = !topSelected ? Brushes.White : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = !topSelected ? new SolidColorBrush(secondColor) : (Brush)FindResource("Brush.TextTertiary"),
            FontWeight = !topSelected ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = 11,
            Cursor = Cursors.Hand,
            Height = 28,
            Margin = new Thickness(0, 0, 2, 0),
            Visibility = _show4 ? Visibility.Visible : Visibility.Collapsed,
        };
        secondBtn.Template = SegmentTemplate();
        secondBtn.Click += (_, _) =>
        {
            _rankFilter[_game + ":" + stat.Name] = false;
            Render();
        };
        Grid.SetColumn(secondBtn, 1);
        segmented.Children.Add(secondBtn);
        Grid.SetRow(segmented, 3);
        body.Children.Add(segmented);

        // 记录列表
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var list = new StackPanel { Margin = new Thickness(0, 0, 2, 0) };
        var rows = topSelected ? stat.TopRows : stat.SecondRows;
        if (rows.Count == 0)
        {
            list.Children.Add(new TextBlock
            {
                Text = "暂无记录",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9B, 0xA3, 0xAD)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
            });
        }
        else
        {
            var limited = topSelected &&
                          (stat.Name.Contains("角色活动") || stat.Name.Contains("武器活动") ||
                           stat.Name.Contains("角色祈愿") || stat.Name.Contains("武器祈愿"));
            foreach (var row in rows.Take(120))
                list.Children.Add(BuildRecordRow(row, topSelected, limited));
        }
        scroll.Content = list;
        Grid.SetRow(scroll, 4);
        body.Children.Add(scroll);

        return card;
    }

    private Border BuildRecordRow((string Name, int Pity, DateTime Time) row, bool topSelected, bool limited)
    {
        var rankColor = topSelected ? Color.FromRgb(0xD9, 0xA2, 0x4A) : Color.FromRgb(0xA4, 0x7B, 0xD0);
        var pityColor = PityColor(row.Pity, topSelected ? PityTarget : 10, topSelected);
        var wrap = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };

        // 平铺记录行：高 32，行底 rank 软色，无独立卡片
        var rowBorder = new Border
        {
            Height = 32,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 0, 8, 0),
            Background = new SolidColorBrush(topSelected
                ? Color.FromArgb(0x26, 0xD9, 0xA2, 0x4A)
                : Color.FromArgb(0x1F, 0xA4, 0x7B, 0xD0)),
        };
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (_showIcon)
        {
            var avatar = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(7),
                Background = new LinearGradientBrush(rankColor, Colors.White, new Point(0, 0), new Point(0, 1)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0),
                Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(row.Name) ? "?" : row.Name[..1],
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(rankColor),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Grid.SetColumn(avatar, 0);
            line.Children.Add(avatar);
        }
        else
        {
            line.ColumnDefinitions[0].Width = new GridLength(4);
        }

        var name = new TextBlock
        {
            Text = row.Name,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x3D)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 1);
        line.Children.Add(name);

        if (limited)
        {
            var up = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xD9, 0xA9, 0x3C)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xD9, 0xA9, 0x3C)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 4, 0),
                Child = new TextBlock
                {
                    Text = "UP",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0xA9, 0x3C)),
                },
            };
            Grid.SetColumn(up, 2);
            line.Children.Add(up);
        }

        var value = new TextBlock
        {
            Text = row.Pity.ToString(),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(pityColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(6, 0, 0, 0),
        };
        Grid.SetColumn(value, 3);
        line.Children.Add(value);

        rowBorder.Child = line;
        wrap.Children.Add(rowBorder);
        wrap.Children.Add(BuildPityBar(row.Pity, topSelected ? PityTarget : 10, pityColor));
        return new Border { Child = wrap, Background = Brushes.Transparent };
    }

    private static Color PityColor(int value, int max, bool topSelected)
    {
        if (!topSelected)
            return value > 7 ? Color.FromRgb(0xC0, 0x45, 0x3F) : Color.FromRgb(0x3F, 0x9B, 0x6B);
        var threshold = (int)Math.Round(max * 0.78);
        return value > threshold ? Color.FromRgb(0xC0, 0x45, 0x3F) : Color.FromRgb(0x3F, 0x9B, 0x6B);
    }

    private static Grid BuildPityBar(int value, int max, Color fill)
    {
        var ratio = max > 0 ? Math.Clamp(value / (double)max, 0, 1) : 0;
        var grid = new Grid { Height = 3, Margin = new Thickness(2, 2, 2, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(ratio, 0.001), GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - ratio, 0.001), GridUnitType.Star) });
        grid.Children.Add(new Border
        {
            Background = new SolidColorBrush(fill),
            CornerRadius = new CornerRadius(1.5),
        });
        var rest = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
            CornerRadius = new CornerRadius(1.5),
        };
        Grid.SetColumn(rest, 1);
        grid.Children.Add(rest);
        return grid;
    }

    private void AddStatRow(StackPanel panel, string title, string value, string percent, Brush color, bool big)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock
        {
            Text = title,
            FontSize = 11.5,
            Foreground = color,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);
        var valuePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var valueText = new TextBlock
        {
            Text = value,
            FontSize = big ? 13.5 : 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = color,
            VerticalAlignment = VerticalAlignment.Center,
        };
        valuePanel.Children.Add(valueText);
        if (!string.IsNullOrEmpty(percent))
        {
            valuePanel.Children.Add(new TextBlock
            {
                Text = percent,
                FontSize = 9.5,
                Foreground = color,
                Opacity = 0.8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 1, 0, 0),
            });
        }
        Grid.SetColumn(valuePanel, 1);
        grid.Children.Add(valuePanel);
        panel.Children.Add(grid);
    }

    private static ControlTemplate SegmentTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "B");
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        return template;
    }

    private static DateTime ParseTime(string text)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : DateTime.MinValue;

    private void DisplayOption_Changed(object sender, RoutedEventArgs e)
    {
        if (PoolGrid is null) return;
        _show4 = ChkShow4.IsChecked != false;
        _showIcon = ChkShowIcon.IsChecked != false;
        Render();
    }

    private void ComboGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressGame || ComboGame.SelectedItem is not GameChoice choice) return;
        _beyond = false;
        _game = choice.Key;
        _uid = "";
        UpdateBeyondBadge();
        RefreshUids();
        Render();
    }

    private void ComboUid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUid || ComboUid.SelectedItem is not UidChoice choice) return;
        _uid = choice.Uid;
        Render();
    }

    private void BtnBeyond_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentBaseGame != "genshin") return;
        _beyond = !_beyond;
        _game = _beyond ? "genshin_beyond" : "genshin";
        _uid = "";
        UpdateBeyondBadge();
        RefreshUids();
        Render();
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        ReloadData();
        ShowToast($"已同步离线抽卡库，共 {_records.Count:N0} 条记录");
    }

    private void BtnMore_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = BtnMore,
            Placement = PlacementMode.Bottom,
        };
        var copy = new MenuItem { Header = "复制当前统计摘要" };
        copy.Click += (_, _) => CopySummary();
        var open = new MenuItem { Header = "打开抽卡记录目录" };
        open.Click += (_, _) => OpenDataDir();
        menu.Items.Add(copy);
        menu.Items.Add(open);
        menu.IsOpen = true;
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        PanelSettings.Visibility = PanelSettings.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (PanelSettings.Visibility == Visibility.Visible)
            TxtDataDir.Text = "数据目录：" + MikoNestBridge.DataDir;
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var gameRecords = _records.Where(r => r.GameKey == _game &&
                                              (string.IsNullOrEmpty(_uid) || r.Uid == _uid)).ToList();
        if (gameRecords.Count == 0)
        {
            ShowToast("当前没有可导出的抽卡记录");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "导出抽卡记录",
            Filter = "CSV 文件|*.csv",
            FileName = $"MikoMiko-Gacha-{_game}-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("uid,game,gacha_type,item_id,name,rank_type,item_type,count,time,id,pool_name");
            foreach (var r in gameRecords.OrderBy(r => ParseTime(r.Time)))
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(r.Uid), Csv(r.GameKey), Csv(r.GachaType), Csv(r.ItemId), Csv(r.Name),
                    Csv(r.RankType), Csv(r.ItemType), Csv(r.Count), Csv(r.Time), Csv(r.Id), Csv(r.PoolName),
                }));
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
            ShowToast($"已导出 {gameRecords.Count:N0} 条记录");
        }
        catch (Exception ex)
        {
            ShowToast("导出失败：" + ex.Message);
        }
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private void OpenDataDir_Click(object sender, RoutedEventArgs e) => OpenDataDir();


    private void CopySummary()
    {
        var label = _game == "genshin_beyond" ? "千星奇域" : Games.First(g => g.Key == _game).Name;
        var filtered = _records.Where(r => r.GameKey == _game &&
                                            (string.IsNullOrEmpty(_uid) || r.Uid == _uid)).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{label} 抽卡记录 · {(_uid == "" ? "全部 UID" : _uid)} · {filtered.Count} 条");
        foreach (var def in PoolDefs[_game])
        {
            var stat = BuildStat(def.Name, filtered.Where(r => MatchPool(r, def)).ToList(), TopRank);
            sb.AppendLine($"{def.Name}：{stat.Total} 抽，{TopRank}★ {stat.TopCount} 个，平均 {stat.Average:0.0} 抽，当前保底 {stat.Pity}/{PityTarget}");
        }
        try
        {
            System.Windows.Clipboard.SetText(sb.ToString());
            ShowToast("统计摘要已复制到剪贴板");
        }
        catch
        {
            ShowToast("复制失败，请稍后再试");
        }
    }

    private void OpenDataDir()
    {
        try
        {
            Process.Start("explorer.exe", MikoNestBridge.DataDir);
        }
        catch { }
    }

    private void ShowToast(string text)
    {
        TxtToastText.Text = text;
        TxtToast.Visibility = Visibility.Visible;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _toastTimer.Tick += (_, _) =>
        {
            TxtToast.Visibility = Visibility.Collapsed;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }
}

