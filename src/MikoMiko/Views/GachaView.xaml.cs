using System.Diagnostics;
using System.Globalization;
using System.Windows;
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
        var choices = new List<UidChoice>
        {
            new("", $"全部账号 · {gameRecords.Count} 条", gameRecords.Count),
        };
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
                choices.Add(new UidChoice(account.Uid, $"{account.Nickname} · 无记录", 0));
        }

        _suppressUid = true;
        ComboUid.ItemsSource = choices;
        var selected = choices.FirstOrDefault(c => !string.IsNullOrEmpty(_uid) && c.Uid == _uid);
        if (selected is null)
        {
            var best = choices.Skip(1).OrderByDescending(c => c.Count).FirstOrDefault();
            selected = best ?? choices[0];
        }
        _uid = selected.Uid;
        ComboUid.SelectedItem = selected;
        _suppressUid = false;
    }

    private void Render()
    {
        var poolDefs = PoolDefs[_game];
        var gameRecords = _records.Where(r => r.GameKey == _game &&
                                               (string.IsNullOrEmpty(_uid) || r.Uid == _uid)).ToList();
        var label = _game == "genshin_beyond" ? "千星奇域" : Games.First(g => g.Key == _game).Name;
        TxtSubtitle.Text = $"本地库 {_records.Count:N0} 条 · {label} {gameRecords.Count:N0} 条" +
                           (string.IsNullOrEmpty(_uid) ? " · 全部 UID" : $" · UID {_uid}");

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

        PoolGrid.Children.Clear();
        PoolGrid.ColumnDefinitions.Clear();
        for (var i = 0; i < stats.Count; i++)
            PoolGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < stats.Count; i++)
        {
            var card = BuildPoolCard(stats[i].Stat, i);
            Grid.SetColumn(card, i);
            PoolGrid.Children.Add(card);
        }
    }

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
            .OrderBy(r => ParseTime(r.Time))
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
        foreach (var record in ordered)
        {
            stat.Total++;
            stat.Pity++;
            var time = ParseTime(record.Time);
            if (time > stat.Latest) stat.Latest = time;
            if (!int.TryParse(record.RankType, out var rank)) continue;
            if (rank == topRank)
            {
                stat.TopCount++;
                stat.TopRows.Insert(0, (record.Name, stat.Pity, time));
                stat.Pity = 0;
            }
            else if (rank == topRank - 1)
            {
                stat.SecondCount++;
                stat.SecondRows.Insert(0, (record.Name, 0, time));
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
        var accent = _game == "genshin_beyond"
            ? Color.FromRgb(0x2E, 0x99, 0x87)
            : Color.FromRgb(0x2B, 0x8B, 0xC8);
        var topColor = Color.FromRgb(0xC2, 0x83, 0x1D);
        var secondColor = Color.FromRgb(0x8B, 0x63, 0xC7);
        var thirdColor = Color.FromRgb(0x79, 0x88, 0x93);
        var topLabel = $"{TopRank}★";
        var secondLabel = $"{TopRank - 1}★";
        var thirdLabel = $"{TopRank - 2}★";

        var card = new Border
        {
            Margin = new Thickness(6, 0, 6, 0),
            CornerRadius = new CornerRadius(12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0xEA, 0xF5)),
            BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush(
                Color.FromRgb(0xF2, 0xFB, 0xFF),
                Color.FromRgb(0xFF, 0xFF, 0xFF),
                new Point(0, 0), new Point(0, 1)),
        };
        var body = new Grid { Margin = new Thickness(14, 13, 14, 12) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        card.Child = body;

        // 标题 + 总抽数
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(new Border
        {
            Width = 4,
            Height = 17,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(accent),
            Margin = new Thickness(0, 0, 7, 0),
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = stat.Name,
            FontSize = 14.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Brush.TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(titlePanel, 0);
        header.Children.Add(titlePanel);
        var count = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x14, accent.R, accent.G, accent.B)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"{stat.Total} 抽",
                FontSize = 10,
                Foreground = new SolidColorBrush(accent),
            },
        };
        Grid.SetColumn(count, 1);
        header.Children.Add(count);
        Grid.SetRow(header, 0);
        body.Children.Add(header);

        // 四行统计
        var statsPanel = new StackPanel { Margin = new Thickness(0, 11, 0, 0) };
        AddStatRow(statsPanel, $"{topLabel} 平均",
            stat.TopCount > 0 ? $"{stat.Average:0.0}" : "—", "", new SolidColorBrush(topColor), true);
        AddStatRow(statsPanel, $"{topLabel} 统计", stat.TopCount.ToString(),
            $"[{stat.TopRate:0.00}%]", new SolidColorBrush(topColor), false);
        AddStatRow(statsPanel, $"{secondLabel} 统计", stat.SecondCount.ToString(),
            $"[{stat.SecondRate:0.00}%]", new SolidColorBrush(secondColor), false);
        AddStatRow(statsPanel, $"{thirdLabel} 统计", stat.ThirdCount.ToString(),
            $"[{stat.ThirdRate:0.00}%]", new SolidColorBrush(thirdColor), false);
        Grid.SetRow(statsPanel, 1);
        body.Children.Add(statsPanel);

        // 白色汇总卡
        var summary = new Border
        {
            Margin = new Thickness(0, 9, 0, 9),
            Padding = new Thickness(11, 9, 11, 9),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xEC, 0xF2, 0xF6)),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0x2E, 0x72, 0x9C),
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.10,
            },
        };
        var summaryGrid = new Grid();
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = TopRank == 5 ? "五星" : "四星",
            FontSize = 9.5,
            Foreground = (Brush)FindResource("Brush.TextTertiary"),
        });
        left.Children.Add(new TextBlock
        {
            Text = stat.TopCount.ToString(),
            FontSize = 25,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Brush.TextPrimary"),
            Margin = new Thickness(0, -2, 0, 0),
        });
        Grid.SetColumn(left, 0);
        summaryGrid.Children.Add(left);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        var avgText = stat.TopCount > 0 ? $"{stat.Average:0.0} 抽" : "—";
        var rateText = stat.Total > 0 ? $"{stat.TopRate:0.0}%" : "—";
        right.Children.Add(new TextBlock
        {
            Text = "平均  " + avgText,
            FontSize = 10,
            Foreground = (Brush)FindResource("Brush.TextSecondary"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        });
        right.Children.Add(new TextBlock
        {
            Text = "出货率  " + rateText,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x35, 0x9B, 0x5A)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 3, 0, 0),
        });
        Grid.SetColumn(right, 1);
        summaryGrid.Children.Add(right);

        var summaryWrap = new StackPanel();
        summaryWrap.Children.Add(summaryGrid);
        var progress = new ProgressBar
        {
            Height = 5,
            Minimum = 0,
            Maximum = PityTarget,
            Value = Math.Min(stat.Pity, PityTarget),
            Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0xB1, 0x70)),
            Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF1, 0xF6)),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 8, 0, 5),
        };
        summaryWrap.Children.Add(progress);
        summaryWrap.Children.Add(new TextBlock
        {
            Text = $"当前保底  {stat.Pity} / {PityTarget}",
            FontSize = 9.5,
            Foreground = (Brush)FindResource("Brush.TextSecondary"),
        });
        summary.Child = summaryWrap;
        Grid.SetRow(summary, 2);
        body.Children.Add(summary);

        // 5★ / 4★ 切换
        var topSelected = !_rankFilter.TryGetValue(_game + ":" + stat.Name, out var filter) || filter;
        var segmented = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        segmented.ColumnDefinitions.Add(new ColumnDefinition());
        segmented.ColumnDefinitions.Add(new ColumnDefinition());
        segmented.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF7, 0xFB)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xEB, 0xF4)),
            BorderThickness = new Thickness(1),
        });
        var topBtn = new Button
        {
            Content = $"{topLabel}",
            Background = topSelected ? Brushes.White : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = topSelected ? new SolidColorBrush(accent) : (Brush)FindResource("Brush.TextTertiary"),
            FontWeight = topSelected ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = 11.5,
            Cursor = Cursors.Hand,
            Height = 28,
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
            Content = $"{secondLabel}",
            Background = topSelected ? Brushes.Transparent : Brushes.White,
            BorderThickness = new Thickness(0),
            Foreground = topSelected ? (Brush)FindResource("Brush.TextTertiary") : new SolidColorBrush(secondColor),
            FontWeight = topSelected ? FontWeights.Normal : FontWeights.SemiBold,
            FontSize = 11.5,
            Cursor = Cursors.Hand,
            Height = 28,
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
        var list = new StackPanel();
        var rows = topSelected ? stat.TopRows : stat.SecondRows;
        if (rows.Count == 0)
        {
            list.Children.Add(new TextBlock
            {
                Text = "暂无记录",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("Brush.TextTertiary"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 22, 0, 0),
            });
        }
        else
        {
            foreach (var row in rows.Take(60))
            {
                var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock
                {
                    Text = $"★ {row.Name}",
                    FontSize = 11,
                    Foreground = (Brush)FindResource("Brush.TextPrimary"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(name, 0);
                rowGrid.Children.Add(name);
                var meta = new TextBlock
                {
                    Text = row.Pity > 0 ? $"{row.Pity} 抽 · {row.Time:MM-dd}" : $"{row.Time:MM-dd}",
                    FontSize = 9.5,
                    Foreground = topSelected ? new SolidColorBrush(topColor) : new SolidColorBrush(secondColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                };
                Grid.SetColumn(meta, 1);
                rowGrid.Children.Add(meta);
                list.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x0A, 0x2B, 0x8B, 0xC8)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(0, 0, 0, 3),
                    Child = rowGrid,
                });
            }
        }
        scroll.Content = list;
        Grid.SetRow(scroll, 4);
        body.Children.Add(scroll);

        return card;
    }

    private void AddStatRow(StackPanel panel, string title, string value, string percent, Brush color, bool big)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock
        {
            Text = title,
            FontSize = 10.5,
            Foreground = color,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);
        var valuePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var valueText = new TextBlock
        {
            Text = value,
            FontSize = big ? 16 : 15,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Brush.TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        valuePanel.Children.Add(valueText);
        if (!string.IsNullOrEmpty(percent))
        {
            valuePanel.Children.Add(new TextBlock
            {
                Text = percent,
                FontSize = 9,
                Foreground = (Brush)FindResource("Brush.TextTertiary"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 2, 0, 0),
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
        var menu = new ContextMenu
        {
            PlacementTarget = BtnSettings,
            Placement = PlacementMode.Bottom,
        };
        var reload = new MenuItem { Header = "重新读取本地记录" };
        reload.Click += (_, _) => { ReloadData(); ShowToast("已重新读取本地抽卡记录"); };
        var open = new MenuItem { Header = "打开 MikoNest 数据目录" };
        open.Click += (_, _) => OpenDataDir();
        var about = new MenuItem { Header = "数据来源：MikoNest gacha_records.json" };
        about.Click += (_, _) => ShowToast("MikoMiko 0.1.35 · 抽卡数据来自 MikoNest 本地记录");
        menu.Items.Add(reload);
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(about);
        menu.IsOpen = true;
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
