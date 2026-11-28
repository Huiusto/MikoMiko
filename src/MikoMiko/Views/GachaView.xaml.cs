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
using MikoMiko.Models;
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
    private sealed record GameChoice(string Key, string Name, string Icon)
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
        public int TopRank = 5;
        public int PityTarget = 90;
        public string PityNote = "";
    }

    private sealed class PoolStat
    {
        public string Name = "";
        public int TopRank = 5;
        public int PityTarget = 90;
        public string PityNote = "";
        public int Total;
        public int TopCount;
        public int SecondCount;
        public int ThirdCount;
        public int Pity;
        public int SecondPity;
        public DateTime Earliest;
        public DateTime Latest;
        public List<(string Name, string ItemType, int Pity, DateTime Time)> TopRows = new();
        public List<(string Name, string ItemType, int Pity, DateTime Time)> SecondRows = new();
        public double Average => TopRows.Count > 0 ? TopRows.Average(r => r.Pity) : 0;
        public double TopRate => Total > 0 ? TopCount * 100d / Total : 0;
        public double SecondRate => Total > 0 ? SecondCount * 100d / Total : 0;
        public double ThirdRate => Total > 0 ? ThirdCount * 100d / Total : 0;
    }

    private static readonly GameChoice[] Games =
    {
        new("genshin", "原神", "pack://application:,,,/Assets/Launch/icons/icon_genshin.png"),
        new("starrail", "星穹铁道", "pack://application:,,,/Assets/Launch/icons/icon_starrail.png"),
        new("zzz", "绝区零", "pack://application:,,,/Assets/Launch/icons/icon_zzz.png"),
        new("bh3", "崩坏3", "pack://application:,,,/Assets/Launch/icons/icon_bh3.png"),
    };

    private static readonly Dictionary<string, PoolDef[]> PoolDefs = new()
    {
        ["genshin"] = new[]
        {
            new PoolDef { Name = "常驻祈愿", Types = new[] { "200" }, PoolNames = new[] { "常驻" }, PityTarget = 90 },
            new PoolDef { Name = "角色活动祈愿", Types = new[] { "301", "400" }, PoolNames = new[] { "角色活动", "角色祈愿" }, PityTarget = 90 },
            new PoolDef { Name = "武器活动祈愿", Types = new[] { "302" }, PoolNames = new[] { "武器活动", "武器祈愿" }, PityTarget = 80 },
            new PoolDef { Name = "集录祈愿", Types = new[] { "500" }, PoolNames = new[] { "集录" }, PityTarget = 90 },
        },
        ["genshin_beyond"] = new[]
        {
            // 千星奇域常驻颂愿本身没有 5★，这一列按 4★/3★/2★ 显示；
            // 活动颂愿有 5★，保持 5★/4★/3★。
            new PoolDef { Name = "常驻颂愿", Types = new[] { "1000" }, PoolNames = new[] { "常驻颂愿" }, TopRank = 4, PityTarget = 90 },
            new PoolDef { Name = "活动颂愿", Types = new[] { "2000" }, PoolNames = new[] { "活动颂愿" }, PityTarget = 90 },
        },
        ["starrail"] = new[]
        {
            new PoolDef { Name = "群星跃迁", Types = new[] { "1" }, PoolNames = new[] { "常驻" }, PityTarget = 90 },
            new PoolDef { Name = "角色活动跃迁", Types = new[] { "11" }, PoolNames = new[] { "角色" }, PityTarget = 90 },
            new PoolDef { Name = "光锥活动跃迁", Types = new[] { "12" }, PoolNames = new[] { "光锥" }, PityTarget = 80 },
            new PoolDef { Name = "联动角色跃迁", Types = new[] { "21" }, PoolNames = new[] { "联动角色", "合作角色" }, PityTarget = 90 },
            new PoolDef { Name = "联动光锥跃迁", Types = new[] { "22" }, PoolNames = new[] { "联动光锥", "合作光锥" }, PityTarget = 80 },
            new PoolDef { Name = "新手跃迁", Types = new[] { "2" }, PoolNames = new[] { "新手" }, PityTarget = 50 },
        },
        ["zzz"] = new[]
        {
            new PoolDef { Name = "常驻频段", Types = new[] { "1" }, PoolNames = new[] { "常驻" }, PityTarget = 90 },
            new PoolDef { Name = "独家频段", Types = new[] { "2", "102" }, PoolNames = new[] { "独家", "角色" }, PityTarget = 90 },
            new PoolDef { Name = "音擎频段", Types = new[] { "3", "103" }, PoolNames = new[] { "音擎", "武器" }, PityTarget = 80 },
            new PoolDef { Name = "邦布频段", Types = new[] { "5" }, PoolNames = new[] { "邦布" }, PityTarget = 80 },
        },
        ["bh3"] = new[]
        {
            new PoolDef { Name = "家园补给", Types = new[] { "1" }, PoolNames = new[] { "家园" }, PityTarget = 100 },
            new PoolDef { Name = "扩充角色补给", Types = new[] { "3" }, PoolNames = new[] { "扩充角色", "扩充" }, PityTarget = 100 },
            new PoolDef { Name = "精准补给", Types = new[] { "2" }, PoolNames = new[] { "精准" }, PityTarget = 50, PityNote = "50 抽 UP 保护 · 200 抽毕业（A/B 独立）" },
            new PoolDef { Name = "扩充装备补给", Types = new[] { "8" }, PoolNames = new[] { "扩充装备" }, PityTarget = 50, PityNote = "50 抽 UP 保护 · 200 抽毕业（不跨期）" },
            new PoolDef { Name = "装备补给", Types = new[] { "4" }, PoolNames = new[] { "装备", "神之键" }, PityTarget = 60, PityNote = "60 抽必出 UP 武器" },
            new PoolDef { Name = "人偶补给", Types = new[] { "5" }, PoolNames = new[] { "人偶" }, PityTarget = 100, PityNote = "100 抽必出初始 S 人偶" },
            new PoolDef { Name = "进阶补给", Types = new[] { "6" }, PoolNames = new[] { "进阶" }, PityTarget = 100, PityNote = "100 抽保底当期 S" },
            new PoolDef { Name = "活动补给", Types = new[] { "7" }, PoolNames = new[] { "活动", "庆典" }, PityTarget = 100, PityNote = "100 抽必出 S 级" },
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
    private bool _gameInitialized;
    private DispatcherTimer? _toastTimer;
    private int _poolCount;
    private readonly List<Border> _poolCards = new();

    public GachaView()
    {
        InitializeComponent();
        ComboGame.ItemsSource = Games;
        _suppressGame = true;
        ComboGame.SelectedIndex = 0;
        _suppressGame = false;
        UpdateGameIcon(Games[0]);
        PoolScroll.SizeChanged += (_, _) => ResizePoolColumns();
        Loaded += (_, _) => ReloadData();
    }

    /// <summary>截图验收用：直接展开记录设置面板。</summary>
    public void DebugOpenSettings()
    {
        PanelSettings.Visibility = Visibility.Visible;
        TxtDataDir.Text = "数据目录：" + MikoNestBridge.DataDir;
    }

    private void ReloadData()
    {
        MikoNestBridge.Reload();
        _records = MikoNestBridge.LoadGachaRecords().ToList();

        if (!_gameInitialized)
        {
            _gameInitialized = true;
            // 用户刚在哪个游戏里打开过抽卡历史，抽卡页就默认停在哪个游戏。
            // 旧实现无条件默认原神，并在本地没有当前游戏记录时切回原神，
            // 结果绝区零的更新请求实际发给了原神。
            var recent = GachaFetchService.FindMostRecentGameKey();
            if (!string.IsNullOrWhiteSpace(recent))
            {
                _game = recent;
                _beyond = false;
            }
        }

        _suppressGame = true;
        var gameChoice = Games.FirstOrDefault(g => g.Key == CurrentBaseGame);
        ComboGame.SelectedItem = gameChoice;
        _suppressGame = false;
        if (gameChoice is not null) UpdateGameIcon(gameChoice);

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
        TxtBeyondLabel.Text = _beyond ? "返回祈愿记录" : "千星奇域颂愿";
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
            choices.Add(new UidChoice(group.Key, group.Key, group.Count()));
        }

        if (choices.Count == 0)
            choices.Add(new UidChoice("", "暂无UID", 0));

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

    private Brush Theme(string key) => TryFindResource(key) as Brush ?? Brushes.Transparent;

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
        TxtSubtitle.Text = $"{label} · 本地记录 · MikoNest 数据";
        TxtDataDir.Text = "数据目录：" + MikoNestBridge.DataDir;
        TxtSummarySub.Text = gameRecords.Count == 0
            ? $"{label} · 暂无记录"
            : $"{label} · 最近 {gameRecords.Max(r => ParseTime(r.Time)):yyyy/MM/dd HH:mm}";
        var activeAccount = HoyoAccountService.Active;
        TxtAccount.Text = activeAccount is null
            ? "未登录米游社 · 仅能读取游戏缓存"
            : $"米游社账号：{activeAccount.Nickname}";
        TxtStatus.Text = _records.Count == 0
            ? "本地库为空 · 点击「更新记录」从游戏缓存或账号拉取"
            : gameRecords.Count == 0
                ? $"{(string.IsNullOrEmpty(_uid) ? "暂无UID" : $"UID {_uid}")} 暂无{label}记录，可点击「更新记录」"
                : $"UID {_uid} · {label} · 已同步";

        var stats = new List<(PoolDef Def, PoolStat Stat)>();
        var matched = new HashSet<GachaRecord>();
        foreach (var def in poolDefs)
        {
            var records = gameRecords.Where(r => MatchPool(r, def)).ToList();
            foreach (var record in records) matched.Add(record);
            var stat = BuildStat(def.Name, records, def.TopRank, def.PityTarget, def.PityNote);
            stats.Add((def, stat));
        }

        var leftovers = gameRecords.Where(r => !matched.Contains(r)).ToList();
        if (leftovers.Count > 0)
            stats.Add((new PoolDef { Name = "其他祈愿" }, BuildStat("其他祈愿", leftovers, 5, 90, "")));

        var totalPulls = stats.Sum(p => p.Stat.Total);
        var topPulls = stats.Where(p => p.Stat.TopRank == 5).Sum(p => p.Stat.TopCount);
        BuildSummary(stats, totalPulls, topPulls);
        BuildChart(stats);

        PoolGrid.Children.Clear();
        _poolCards.Clear();
        _poolCount = stats.Count;
        for (var i = 0; i < stats.Count; i++)
        {
            var card = BuildPoolCard(stats[i].Stat, i);
            _poolCards.Add(card);
            PoolGrid.Children.Add(card);
        }
        ResizePoolColumns();
        Dispatcher.BeginInvoke(new Action(ResizePoolColumns), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 0.1.57：所有抽卡记录卡固定排成一行。宽窗口下按可视宽度均分铺满，
    /// 窄窗口下保持最小可读卡宽并改为横向滚动，不再折行。
    /// </summary>
    private void ResizePoolColumns()
    {
        if (_poolCount <= 0 || _poolCards.Count != _poolCount) return;
        var available = PoolScroll.ViewportWidth - 36; // StackPanel 左右各 18
        if (double.IsNaN(available) || available <= 0)
            available = Math.Max(0, ActualWidth - 38);
        if (double.IsNaN(available) || available <= 0) return;

        const double gap = 14;
        // 0.1.67：崩坏3 有 6 个补给卡池，原最小卡宽 232 会把精准/扩充装备等挤到窗口
        // 右侧之外。降到 170 后 6 张卡可以在常见窗口宽度内同排铺满，不再默认藏到右侧。
        const double minReadable = 170;
        var fitWidth = (available - (_poolCount - 1) * gap) / _poolCount;
        var cardWidth = fitWidth >= minReadable ? fitWidth : 268;
        var contentWidth = _poolCount * cardWidth + (_poolCount - 1) * gap;
        var viewportHeight = PoolScroll.ViewportHeight;
        var cardHeight = Math.Max(390, viewportHeight - 8);

        for (var i = 0; i < _poolCards.Count; i++)
        {
            var card = _poolCards[i];
            card.Width = cardWidth;
            card.Height = cardHeight;
            card.Margin = new Thickness(0, 0, i == _poolCards.Count - 1 ? 0 : gap, 0);
        }
        PoolGrid.Width = Math.Max(available, contentWidth);
    }

    private void BuildSummary(List<(PoolDef Def, PoolStat Stat)> stats, int totalPulls, int topPulls)
    {
        SummaryPanel.Children.Clear();
        var topLabel = $"{TopRank}★";
        var secondLabel = $"{TopRank - 1}★";
        var thirdLabel = $"{TopRank - 2}★";
        var topAverage = stats.SelectMany(p => p.Stat.TopRows).Select(r => r.Pity).DefaultIfEmpty(0).Average();
        var limitedTop = stats
            .Where(p => p.Stat.TopRank == 5 && (
                        p.Stat.Name.Contains("活动") || p.Stat.Name.Contains("限定") ||
                        p.Stat.Name.Contains("独家") || p.Stat.Name.Contains("角色") ||
                        p.Stat.Name.Contains("武器") || p.Stat.Name.Contains("联动") ||
                        p.Stat.Name.Contains("扩充") || p.Stat.Name.Contains("精准")))
            .Sum(p => p.Stat.TopCount);
        // 常驻颂愿按 4★ 上限统计，它的 TopCount 是 4★，需要归到 4★ 行；
        // 其余卡池仍按 5★/4★/3★ 三档。
        var secondTotal = stats.Sum(p => p.Stat.TopRank == 5 ? p.Stat.SecondCount : p.Stat.TopCount);
        var thirdTotal = stats.Sum(p => p.Stat.TopRank == 5 ? p.Stat.ThirdCount : p.Stat.SecondCount);

        AddSummaryRow($"{topLabel} 平均", topPulls > 0 ? $"{topAverage:0.0}" : "—", "抽", "#E4BB72");
        AddSummaryRow($"{topLabel} 统计", topPulls.ToString(), $"{Rate(topPulls, totalPulls)}", "#E4BB72");
        AddSummaryRow("UP 出货", limitedTop.ToString(), "限定卡池", "#E9A23B");
        AddSummaryRow($"{secondLabel} 统计", secondTotal.ToString(), $"{Rate(secondTotal, totalPulls)}", "#B18AE0");
        AddSummaryRow($"{thirdLabel} 统计", thirdTotal.ToString(), $"{Rate(thirdTotal, totalPulls)}", "#7E96AF");

        var totalLine = new Border
        {
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(0, 9, 0, 0),
            BorderBrush = Theme("Brush.Border"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new TextBlock
            {
                Text = $"共 {totalPulls:N0} 抽",
                FontSize = 10,
                Foreground = Theme("Brush.TextTertiary"),
            },
        };
        SummaryPanel.Children.Add(totalLine);
    }

    private static string Rate(int part, int total) => total > 0 ? $"{part * 100d / total:0.0}%" : "—";

    private void AddSummaryRow(string label, string value, string hint, string colorText)
    {
        var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(colorText);
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = Theme("Brush.TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(text);
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Center,
        });
        right.Children.Add(new TextBlock
        {
            Text = hint,
            FontSize = 9,
            Foreground = Theme("Brush.TextTertiary"),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(5, 0, 0, 2),
        });
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        SummaryPanel.Children.Add(grid);
    }

    private void BuildChart(List<(PoolDef Def, PoolStat Stat)> stats)
    {
        ChartPanel.Children.Clear();
        var gold = Color.FromRgb(0xE0, 0xB1, 0x5C);
        var purple = Color.FromRgb(0xA9, 0x82, 0xD6);

        ChartPanel.Children.Add(new TextBlock
        {
            Text = $"{TopRank}★ 分段",
            FontSize = 10,
            Foreground = new SolidColorBrush(gold),
            Margin = new Thickness(0, 2, 0, 5),
        });
        var tops = stats.SelectMany(p => p.Stat.TopRows).Select(r => r.Pity).ToList();
        var chartTarget = stats.Count > 0 ? stats.Max(p => p.Stat.PityTarget) : 90;
        ChartPanel.Children.Add(BuildPityStrip(tops, chartTarget, gold));

        ChartPanel.Children.Add(new TextBlock
        {
            Text = $"{TopRank - 1}★ 分段",
            FontSize = 10,
            Foreground = new SolidColorBrush(purple),
            Margin = new Thickness(0, 12, 0, 5),
        });
        var seconds = stats.SelectMany(p => p.Stat.SecondRows).Select(r => r.Pity).ToList();
        ChartPanel.Children.Add(BuildPityStrip(seconds, 10, purple));

        ChartPanel.Children.Add(new TextBlock
        {
            Text = "分段条宽 = 该区间出货次数；悬停查看具体数量。",
            FontSize = 9,
            Foreground = Theme("Brush.TextTertiary"),
            Margin = new Thickness(0, 9, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private static Grid BuildPityStrip(List<int> values, int max, Color color)
    {
        const int buckets = 10;
        var counts = new int[buckets];
        foreach (var value in values)
        {
            var index = max <= 0 ? 0 : Math.Clamp(value * buckets / (max + 1), 0, buckets - 1);
            counts[index]++;
        }
        var total = values.Count;
        var grid = new Grid { Height = 13 };
        for (var i = 0; i < buckets; i++)
        {
            var count = counts[i];
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(Math.Max(count, 0.25), GridUnitType.Star),
            });
            var start = i * max / buckets + 1;
            var end = (i + 1) * max / buckets;
            var bar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(
                    count == 0 ? (byte)0x2A : (byte)0xD8, color.R, color.G, color.B)),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(1, 0, 1, 0),
                ToolTip = $"{start}-{end} 抽：{count} 次",
            };
            Grid.SetColumn(bar, i);
            grid.Children.Add(bar);
        }
        if (total == 0)
        {
            var empty = new TextBlock
            {
                Text = "暂无出货记录",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x62, 0x73, 0x84)),
            };
            var host = new Grid();
            host.Children.Add(grid);
            host.Children.Add(empty);
            Grid.SetColumn(empty, 0);
            var wrapper = new Grid();
            wrapper.Children.Add(host);
            return wrapper;
        }
        return grid;
    }

    // 四个游戏的官方记录都已统一成 5★/4★/3★ 口径：
    // 绝区零接口原始 rank_type 比界面星级小 1，拉取时已 +1；崩坏3、千星奇域活动颂愿本身也有 5★。
    // 旧实现按游戏把上限写成 4，导致 5★→4★、4★→3★、3★→2★ 全部降了一档。
    private int TopRank => 5;

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

    private static PoolStat BuildStat(string name, List<GachaRecord> records, int topRank, int pityTarget, string pityNote)
    {
        var stat = new PoolStat
        {
            Name = name,
            TopRank = topRank,
            PityTarget = pityTarget,
            PityNote = pityNote,
        };
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
                stat.TopRows.Insert(0, (record.Name, record.ItemType, stat.Pity, time));
                stat.Pity = 0;
                stat.SecondPity = 0;
            }
            else if (rank == topRank - 1)
            {
                stat.SecondCount++;
                stat.SecondRows.Insert(0, (record.Name, record.ItemType, stat.SecondPity, time));
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
            "starrail" => Color.FromRgb(0x8C, 0x96, 0xE6),
            "zzz" => Color.FromRgb(0xE0, 0xB0, 0x48),
            "bh3" => Color.FromRgb(0xD0, 0x8C, 0xE4),
            "genshin_beyond" => Color.FromRgb(0xE0, 0xB0, 0x48),
            _ => Color.FromRgb(0x74, 0xC4, 0xE2),
        };
        var topColor = Color.FromRgb(0xE0, 0xB1, 0x5C);
        var secondColor = Color.FromRgb(0xA9, 0x82, 0xD6);
        var thirdColor = Color.FromRgb(0x86, 0x9C, 0xB3);
        var topLabel = $"{stat.TopRank}★";
        var secondLabel = $"{stat.TopRank - 1}★";
        var thirdLabel = $"{stat.TopRank - 2}★";

        var card = new Border
        {
            Margin = new Thickness(0, 0, 12, 0),
            CornerRadius = new CornerRadius(14),
            BorderBrush = Theme("Brush.Border"),
            BorderThickness = new Thickness(1),
            Background = Theme("Brush.Card"),
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var root = new Grid();
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
        Grid.SetRow(body, 0);
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
            Foreground = Theme("Brush.TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(title, 0);
        header.Children.Add(title);
        var count = new TextBlock
        {
            Text = stat.Total.ToString(),
            FontSize = 15,
            Foreground = Theme("Brush.TextSecondary"),
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
        var meta = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
        meta.Children.Add(new TextBlock
        {
            Text = period,
            FontSize = 10,
            Foreground = Theme("Brush.TextTertiary"),
        });
        var pityLabel = stat.PityNote.Length > 0
            ? $"{stat.PityTarget} 抽 · {stat.PityNote}"
            : $"{stat.PityTarget} 抽保底";
        meta.Children.Add(new TextBlock
        {
            Text = pityLabel,
            FontSize = 9,
            Foreground = Theme("Brush.TextTertiary"),
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = pityLabel,
        });
        Grid.SetRow(meta, 1);
        body.Children.Add(meta);

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
            Background = Theme("Brush.ControlBg"),
            BorderBrush = Theme("Brush.ControlBorder"),
            BorderThickness = new Thickness(1),
        });
        var topBtn = new Button
        {
            Content = topLabel,
            Background = topSelected ? Theme("Brush.AccentSoft") : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = topSelected ? new SolidColorBrush(topColor) : Theme("Brush.TextTertiary"),
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
            Background = !topSelected ? Theme("Brush.AccentSoft") : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = !topSelected ? new SolidColorBrush(secondColor) : Theme("Brush.TextTertiary"),
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
            Cursor = Cursors.SizeAll,
        };
        var list = new StackPanel { Margin = new Thickness(0, 0, 2, 0) };
        var rows = topSelected ? stat.TopRows : stat.SecondRows;
        if (rows.Count == 0)
        {
            list.Children.Add(new TextBlock
            {
                Text = "暂无记录",
                FontSize = 11.5,
                Foreground = Theme("Brush.TextTertiary"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0),
            });
        }
        else
        {
            var limited = topSelected && IsLimitedPool(_game, stat.Name);
            foreach (var row in rows.Take(120))
                list.Children.Add(BuildRecordRow(row, topSelected, limited, stat.PityTarget, stat.PityNote));
        }
        scroll.Content = list;

        // 记录区支持按住鼠标左键上下拖动。
        var dragging = false;
        var dragStart = new Point();
        var dragOffset = 0d;
        scroll.PreviewMouseLeftButtonDown += (_, args) =>
        {
            if (IsInsideScrollBar(args.OriginalSource)) return;
            dragging = true;
            dragStart = args.GetPosition(scroll);
            dragOffset = scroll.VerticalOffset;
            scroll.CaptureMouse();
        };
        scroll.PreviewMouseMove += (_, args) =>
        {
            if (!dragging || args.LeftButton != MouseButtonState.Pressed) return;
            var delta = args.GetPosition(scroll).Y - dragStart.Y;
            scroll.ScrollToVerticalOffset(dragOffset - delta);
            args.Handled = true;
        };
        scroll.PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (!dragging) return;
            dragging = false;
            scroll.ReleaseMouseCapture();
        };
        scroll.LostMouseCapture += (_, _) => dragging = false;

        Grid.SetRow(scroll, 4);
        body.Children.Add(scroll);

        return card;
    }

    private Border BuildRecordRow((string Name, string ItemType, int Pity, DateTime Time) row, bool topSelected, bool limited, int pityTarget, string pityNote)
    {
        var rankColor = topSelected ? Color.FromRgb(0xE0, 0xB1, 0x5C) : Color.FromRgb(0xA9, 0x82, 0xD6);
        var currentTarget = topSelected ? pityTarget : 10;
        var pityColor = PityColor(row.Pity, currentTarget, topSelected);
        var wrap = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };

        // 平铺记录行：高 32，行底 rank 软色，无独立卡片
        var rowBorder = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(6, 0, 8, 0),
            Background = new SolidColorBrush(topSelected
                ? Color.FromArgb(0x24, 0xE0, 0xB1, 0x5C)
                : Color.FromArgb(0x20, 0xA9, 0x82, 0xD6)),
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
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromArgb(0x38, rankColor.R, rankColor.G, rankColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x99, rankColor.R, rankColor.G, rankColor.B)),
                BorderThickness = new Thickness(1),
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
            Foreground = Theme("Brush.TextPrimary"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 1);
        line.Children.Add(name);

        if (limited && IsUpTopItem(_game, row.Name, row.ItemType))
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
                    Text = "UP！",
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
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(pityColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = pityNote,
        };
        Grid.SetColumn(value, 3);
        line.Children.Add(value);

        rowBorder.Child = line;
        wrap.Children.Add(rowBorder);
        wrap.Children.Add(BuildPityBar(row.Pity, currentTarget, pityColor));
        return new Border { Child = wrap, Background = Brushes.Transparent };
    }

    private static bool IsInsideScrollBar(object? source)
    {
        while (source is DependencyObject node)
        {
            if (node is System.Windows.Controls.Primitives.ScrollBar) return true;
            try { source = VisualTreeHelper.GetParent(node); }
            catch { return false; }
        }
        return false;
    }

    private static bool IsLimitedPool(string game, string poolName) => game switch
    {
        "genshin" => poolName is "角色活动祈愿" or "武器活动祈愿",
        "starrail" => poolName is "角色活动跃迁" or "光锥活动跃迁" or "联动角色跃迁" or "联动光锥跃迁",
        "zzz" => poolName is "独家频段" or "音擎频段",
        "bh3" => poolName is "精准补给" or "扩充角色补给" or "扩充装备补给" or "活动补给" or "装备补给" or "进阶补给" or "人偶补给",
        _ => false,
    };

    /// <summary>限定卡池里的最高稀有度条目：排除常驻角色/武器、圣痕等非角色武器后显示 UP！。</summary>
    private static bool IsUpTopItem(string game, string name, string itemType)
    {
        if (!NonUpTopItems.TryGetValue(game, out var standard)) return false;
        if (string.IsNullOrWhiteSpace(name)) return false;
        itemType ??= "";
        if (itemType.Contains("圣痕", StringComparison.Ordinal) ||
            itemType.Contains("材料", StringComparison.Ordinal) ||
            itemType.Contains("道具", StringComparison.Ordinal)) return false;
        return !standard.Contains(name.Trim());
    }

    private static readonly Dictionary<string, HashSet<string>> NonUpTopItems = new(StringComparer.Ordinal)
    {
        ["genshin"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "迪卢克", "刻晴", "琴", "莫娜", "七七", "提纳里", "迪希雅", "梦见月瑞希",
            "阿莫斯之弓", "和璞鸢", "狼的末路", "风鹰剑", "四风原典",
            "天空之翼", "天空之傲", "天空之脊", "天空之刃", "天空之卷",
        },
        ["zzz"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "冯·莱卡恩", "丽娜", "猫宫又奈", "「11号」", "格莉丝", "珂蕾妲",
            "硫磺石", "拘缚者", "嵌合编译器", "啜泣摇篮", "燃狱齿轮",
            "钢铁肉垫", "深海访客", "时光切片", "防爆者VI型", "旋钻机-赤轴",
        },
        ["starrail"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "姬子", "瓦尔特", "布洛妮娅", "杰帕德", "克拉拉", "彦卿", "白露",
            "银河铁道之夜", "以世界之名", "无可取代的东西", "时节不居",
            "制胜的瞬间", "如泥酣眠", "但战斗还未结束",
        },
        ["bh3"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "空之律者", "理之律者", "识之律者", "失落迷迭", "不灭星锚", "魇夜星渊",
            "螺旋·愚戏之匣", "真我·人之律者", "玉骑士·月痕", "真理之律者",
            "终焉之律者", "织羽梦旌", "薪炎之律者", "梅比乌斯", "天元骑英",
            "阿波尼亚", "辉骑士·月魄", "彼岸双生", "云墨丹心", "雷之律者",
            "空无之钥", "无存之钥", "2nd圣遗物", "3rd圣遗物", "5th圣遗物", "11th圣遗物",
            "索尔之锤", "超电磁手炮", "雷刀·鬼角", "幽色咏叹调", "灵刀·樱吹雪",
            "妖刀·赤染樱", "磁暴·斩", "虚无之刃", "提尔的断腕", "火刀·真田",
            "鲜血之舞", "犹大的誓约", "仿犹大·血之拥抱", "永暮双狼", "断光之努亚达",
            "残火之鸢", "天殛之境的残响", "诞于寂壤的无殃", "暂无星尘的挽歌",
            "月痕", "星陨断影", "终焉之律者专属武器", "脉冲太刀17式", "脉冲装·太刀",
            "凯旋轰鸣", "异度黑核侵蚀", "天火圣裁", "霜落之心", "往世的蛇影",
            "樱色之约", "丹樱", "苍玄之书人偶", "圣裁与恩典", "所罗门王之钥",
        },
    };

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
        if (PoolGrid is null || ChkShow4 is null || ChkShowIcon is null) return;
        _show4 = ChkShow4.IsChecked != false;
        _showIcon = ChkShowIcon.IsChecked != false;
        Render();
    }

    public void DebugSelectGame(string key)
    {
        var choice = Games.FirstOrDefault(g => g.Key == key);
        if (choice is null) return;
        ComboGame.SelectedItem = choice;
        if (_game != key)
        {
            _beyond = false;
            _game = key;
            _uid = "";
            UpdateGameIcon(choice);
            UpdateBeyondBadge();
            RefreshUids();
            Render();
        }
    }

    private void ComboGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressGame || ComboGame.SelectedItem is not GameChoice choice) return;
        _beyond = false;
        _game = choice.Key;
        UpdateGameIcon(choice);
        _uid = "";
        UpdateBeyondBadge();
        RefreshUids();
        Render();
    }

    private void UpdateGameIcon(GameChoice choice)
    {
        try
        {
            ImgGameIcon.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(choice.Icon));
        }
        catch
        {
            ImgGameIcon.Source = null;
        }
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

    private bool _updating;

    public void DebugUpdateRecords()
    {
        BtnUpdate_Click(null!, new RoutedEventArgs());
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        var gameKey = CurrentBaseGame;
        var gameLabel = Games.FirstOrDefault(g => g.Key == gameKey)?.Name ?? gameKey;

        // 更新前先确认对应游戏正在运行，并且最近确实打开过抽卡记录页。
        // 只凭历史缓存会读到几小时前甚至上次启动的 authkey，游戏内的记录页其实并没有打开。
        var cacheEntry = GachaFetchService.FindCachedEntry(gameKey);
        var pageOpen = cacheEntry is not null
                       && GachaFetchService.IsGameRunning(gameKey)
                       && DateTime.UtcNow - cacheEntry.WriteTimeUtc <= TimeSpan.FromMinutes(15);
        if (!pageOpen)
        {
            TxtStatus.Text = $"未检测到 {gameLabel} 抽卡记录页";
            ShowToast("请在游戏内打开抽卡记录");
            return;
        }

        var uid = _uid;
        var account = HoyoAccountService.Active;
        var roleList = new List<SignRole>();
        var cookie = account?.Cookie ?? "";
        var biz = gameKey switch
        {
            "starrail" => "hkrpg_cn",
            "zzz" => "nap_cn",
            "bh3" => "bh3_cn",
            _ => "hk4e_cn",
        };

        _updating = true;
        BtnUpdate.IsEnabled = false;
        LoadingOverlay.Visibility = Visibility.Visible;
        TxtStatus.Text = $"正在读取 {gameLabel} 抽卡记录…";
        try
        {
            var cachedUrl = GachaFetchService.FindCachedUrl(gameKey);
            // 游戏刚打开过抽卡历史时，缓存 authkey 最可靠。此时不要先请求角色列表：
            // 登录态失效或接口慢会让更新按钮一直卡在加载层，缓存明明有效却永远读不到。
            if (!string.IsNullOrWhiteSpace(cookie) && string.IsNullOrWhiteSpace(uid)
                && string.IsNullOrWhiteSpace(cachedUrl))
            {
                var roles = await SignService.GetRolesAsync(cookie, biz);
                roleList = roles.roles;
                if (string.IsNullOrWhiteSpace(uid))
                {
                    var role = roleList.FirstOrDefault();
                    if (role is not null) uid = role.Uid;
                }
            }
            if (string.IsNullOrWhiteSpace(uid))
            {
                // 登录态可能失效，角色列表取不到 UID；只要游戏缓存里有刚打开的抽卡页，
                // 仍然先拉记录，UID 从接口返回的记录里回填，不能在这里直接挡住。
                if (string.IsNullOrWhiteSpace(cachedUrl))
                {
                    ShowToast("请先登录米游社，或在游戏内打开一次抽卡记录页面");
                    return;
                }
            }

            var roleInfo = roleList.FirstOrDefault(r => r.Uid == uid);
            var region = roleInfo?.Region ?? (gameKey switch
            {
                "genshin" => "cn_gf01",
                "starrail" => "prod_gf_cn",
                "zzz" => "prod_gf_cn",
                _ => "prod_gf_cn",
            });

            TxtStatus.Text = $"正在读取 {gameLabel} 抽卡记录…";
            var fetched = await GachaFetchService.FetchWithCacheOrAccountAsync(
                gameKey, cookie, uid, region, CancellationToken.None);
            var added = GachaFetchService.MergeRecords(fetched.Records);
            if (string.IsNullOrWhiteSpace(uid))
                uid = fetched.Records.Select(r => r.Uid).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)) ?? "";
            _uid = uid;
            ReloadData();
            if (fetched.Records.Count == 0)
                ShowToast(fetched.Message);
            else
                ShowToast(added > 0
                    ? $"已更新 {gameLabel} {fetched.Records.Count:N0} 条记录（新增 {added:N0} 条）"
                    : $"已更新 {gameLabel} {fetched.Records.Count:N0} 条记录，没有新增");
        }
        catch (Exception ex)
        {
            ShowToast("更新失败：" + ex.Message);
            TxtStatus.Text = "更新失败：" + ex.Message;
        }
        finally
        {
            _updating = false;
            BtnUpdate.IsEnabled = true;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnOverview_Click(object sender, RoutedEventArgs e)
    {
        OverviewCard.Visibility = OverviewCard.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        BtnOverview.Content = OverviewCard.Visibility == Visibility.Visible ? "记录概览 ▾" : "记录概览 ▸";
    }

    private void BtnAnalysis_Click(object sender, RoutedEventArgs e)
    {
        AnalysisCard.Visibility = AnalysisCard.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        BtnAnalysis.Content = AnalysisCard.Visibility == Visibility.Visible ? "出货分析 ▾" : "出货分析 ▸";
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

    private void ClearAccount_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_uid))
        {
            ShowToast("当前没有可清理的游戏 UID 数据");
            return;
        }
        var label = _game == "genshin_beyond"
            ? "千星奇域"
            : Games.FirstOrDefault(g => g.Key == _game)?.Name ?? _game;
        var confirm = MessageBox.Show(
            Window.GetWindow(this),
            $"确定清理「{label}」游戏 UID {_uid} 的全部本地抽卡记录吗？\n\n只会清理这一个游戏下的这个 UID，不影响米游社登录账号或其他游戏。清理前会自动保留时间戳备份。",
            "清理账号数据",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        var removed = GachaFetchService.ClearRecords(_game, _uid);
        if (removed < 0)
        {
            ShowToast("清理失败，记录文件可能正被占用");
            return;
        }

        MikoNestBridge.Reload();
        _records = MikoNestBridge.LoadGachaRecords().ToList();
        RefreshUids();
        Render();
        ShowToast(removed == 0
            ? $"UID {_uid} 没有可清理的{label}记录"
            : $"已清理 UID {_uid} 的 {removed:N0} 条{label}记录");
    }


    private void CopySummary()
    {
        var label = _game == "genshin_beyond" ? "千星奇域" : Games.First(g => g.Key == _game).Name;
        var filtered = _records.Where(r => r.GameKey == _game &&
                                            (string.IsNullOrEmpty(_uid) || r.Uid == _uid)).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{label} 抽卡记录 · {(_uid == "" ? "全部 UID" : _uid)} · {filtered.Count} 条");
        foreach (var def in PoolDefs[_game])
        {
            var stat = BuildStat(def.Name, filtered.Where(r => MatchPool(r, def)).ToList(), def.TopRank, def.PityTarget, def.PityNote);
            sb.AppendLine($"{def.Name}：{stat.Total} 抽，{def.TopRank}★ {stat.TopCount} 个，平均 {stat.Average:0.0} 抽，当前保底 {stat.Pity}/{stat.PityTarget}");
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
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) =>
        {
            TxtToast.Visibility = Visibility.Collapsed;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }
}

