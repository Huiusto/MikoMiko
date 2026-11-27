using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MikoMiko.Services;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace MikoMiko.Views;

/// <summary>
/// 追番 / 影视原生目录页：只展示用户指定的哔哩哔哩番剧与官方音乐会，
/// 点击卡片后交给内置播放器解析直连流；播放失败时直接提示，不跳转到网页。
/// </summary>
public partial class AnimeHubView : UserControl
{
    private sealed class Entry
    {
        public required string Title;
        public required string Url;
        public required string Kind;
        public string Note = "";
        public Border Card = null!;
        public Border CoverHost = null!;
        public TextBlock Meta = null!;
    }

    private static readonly Entry[] Catalog =
    {
        new() { Title = "女武神的餐桌", Url = "https://www.bilibili.com/bangumi/media/md28221384", Kind = "番剧", Note = "崩坏3 · 美食日常" },
        new() { Title = "女武神的餐桌 第二季", Url = "https://www.bilibili.com/bangumi/media/md28229117", Kind = "番剧", Note = "崩坏3 · 第二季" },
        new() { Title = "人偶学园", Url = "https://www.bilibili.com/bangumi/media/md28234658", Kind = "番剧", Note = "崩坏3 · 学园日常" },
        new() { Title = "黄金庭院：冬日里的新年愿望", Url = "https://www.bilibili.com/bangumi/media/md28351532", Kind = "番剧", Note = "崩坏3 · 冬日特别篇" },
        new() { Title = "旅途危机！圣芙蕾雅号特别事件", Url = "https://www.bilibili.com/bangumi/media/md21176968", Kind = "番剧", Note = "崩坏3 · 特别动画" },
        new() { Title = "《原神》长期项目启动·概念PV", Url = "https://www.bilibili.com/video/BV15B4y1n7dg/?spm_id_from=333.337.search-card.all.click&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "番剧", Note = "原神 · 概念 PV" },
        new() { Title = "《崩坏：星穹铁道》原创动画·概念PV——「死于午后」", Url = "https://www.bilibili.com/video/BV1YXDfBUETP/?spm_id_from=333.788.recommend_more_video.0&trackid=web_related_0.router-related-2589621-cb5r7.1795614157921.427&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "番剧", Note = "星穹铁道 · 概念 PV" },

        new() { Title = "崩坏3「林海绮律」", Url = "https://www.bilibili.com/bangumi/media/md20310734", Kind = "音乐会", Note = "崩坏3 · 官方音乐会" },
        new() { Title = "崩坏3「纯真梦歌」", Url = "https://www.bilibili.com/bangumi/media/md28339264", Kind = "音乐会", Note = "崩坏3 · 官方音乐会" },
        new() { Title = "崩坏3「星火流音」", Url = "https://www.bilibili.com/bangumi/media/md28235471", Kind = "音乐会", Note = "崩坏3 · 官方音乐会" },
        new() { Title = "未定事件簿「心乐未名」", Url = "https://www.bilibili.com/video/BV1nE826rEJ4/?spm_id_from=333.337.search-card.all.click&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "音乐会", Note = "未定事件簿 · 官方音乐会" },
        new() { Title = "原神「无际之旅的旋律」", Url = "https://www.bilibili.com/video/BV1E44y1t7Kn/?spm_id_from=333.1387.search.video_card.click&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "音乐会", Note = "原神 · 官方音乐会" },
        new() { Title = "原神「映春华章」", Url = "https://www.bilibili.com/video/BV1TY411L7ZD/?spm_id_from=333.1387.search.video_card.click&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "音乐会", Note = "原神 · 新春音乐会" },
        new() { Title = "原神交响音乐会2023", Url = "https://www.bilibili.com/video/BV1Rj411p7b1/?spm_id_from=333.1387.search.video_card.click&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "音乐会", Note = "原神 · 交响音乐会" },
        new() { Title = "崩坏：星穹铁道「2024星铁LIVE」", Url = "https://www.bilibili.com/video/BV1ZH4y1V7AE/?spm_id_from=333.1387.search.video_card.click&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "音乐会", Note = "星穹铁道 · 官方 LIVE" },
        new() { Title = "崩坏：星穹铁道「2025星铁LIVE」", Url = "https://www.bilibili.com/video/BV1aDGfziEGh/?spm_id_from=333.1387.search.video_card.click&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "音乐会", Note = "星穹铁道 · 官方 LIVE" },
        new() { Title = "崩坏：星穹铁道「2026星铁奇妙夜」", Url = "https://www.bilibili.com/video/BV16p9UBgEhT/?spm_id_from=333.1387.search.video_card.click&vd_source=2a3f4b3e272a07a72077b9a851b308a6", Kind = "音乐会", Note = "星穹铁道 · 官方 LIVE" },
    };

    private static readonly HttpClient Http = CreateHttpClient();
    private readonly Action<string, string> _playRequested;
    private readonly BilibiliAnimeService _bilibili = new();
    private readonly AnimeFollowService? _follows;
    private readonly DispatcherTimer _toastTimer;
    private CancellationTokenSource? _cts;
    private WrapPanel _bangumiPanel = null!;
    private WrapPanel _concertPanel = null!;
    private Border _bangumiSection = null!;
    private Border _concertSection = null!;
    private string _filter = "all";
    private string _query = "";
    private bool _followMode;
    private bool _started;

    public AnimeHubView(Action<string, string> playRequested, AnimeFollowService? follows = null)
    {
        InitializeComponent();
        _playRequested = playRequested;
        _follows = follows;
        if (_follows is not null) _follows.Changed += OnFollowsChanged;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastPanel.Visibility = Visibility.Collapsed;
        };
        BuildSections();
        ApplyFilter("all");
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            _cts?.Cancel();
            if (_follows is not null) _follows.Changed -= OnFollowsChanged;
        };
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(18) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.bilibili.com/");
        return client;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;
        _cts = new CancellationTokenSource();
        Dispatcher.BeginInvoke(new Action(() => ContentScroll.ScrollToTop()), System.Windows.Threading.DispatcherPriority.Loaded);
        _ = LoadCoversAsync(_cts.Token);
    }

    private void BuildSections()
    {
        _bangumiSection = CreateSection("番剧", out _bangumiPanel);
        _concertSection = CreateSection("音乐会", out _concertPanel);
        Sections.Children.Add(_bangumiSection);
        Sections.Children.Add(_concertSection);

        foreach (var entry in Catalog)
        {
            var card = BuildCard(entry);
            entry.Card = card;
            if (entry.Kind == "音乐会") _concertPanel.Children.Add(card);
            else _bangumiPanel.Children.Add(card);
        }
    }

    private Border CreateSection(string title, out WrapPanel panel)
    {
        var root = new Border
        {
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 0, 0, 12),
        };
        var stack = new StackPanel();
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new Border
        {
            Width = 4,
            Height = 22,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(title == "番剧" ? Color.FromRgb(0x4B, 0x7B, 0xC8) : Color.FromRgb(0xB8, 0x8A, 0x3E)),
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Brush.TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        stack.Children.Add(header);

        panel = new WrapPanel();
        stack.Children.Add(panel);
        root.Child = stack;
        return root;
    }

    private Border BuildCard(Entry entry)
    {
        var accent = entry.Kind == "音乐会" ? Color.FromRgb(0xB8, 0x8A, 0x3E) : Color.FromRgb(0x4B, 0x7B, 0xC8);
        var card = new Border
        {
            Width = 302,
            Height = 236,
            Margin = new Thickness(0, 0, 16, 16),
            CornerRadius = new CornerRadius(14),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDF, 0xEA, 0xF4)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ClipToBounds = true,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x35, 0x5F, 0x92),
                BlurRadius = 14,
                ShadowDepth = 2,
                Opacity = 0.12,
            },
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(158) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 封面区域
        var coverHost = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = new LinearGradientBrush(
                Color.FromRgb(0x24, 0x39, 0x5B),
                accent,
                new Point(0, 0), new Point(1, 1)),
            ClipToBounds = true,
        };
        var coverGrid = new Grid();
        coverGrid.Children.Add(new Border
        {
            Background = new LinearGradientBrush(
                Color.FromArgb(0x00, 0x10, 0x1C, 0x2C),
                Color.FromArgb(0x9A, 0x08, 0x14, 0x24),
                new Point(0, 0.25), new Point(0, 1)),
        });

        var kindBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, accent.R, accent.G, accent.B)),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(9, 3, 9, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12, 12, 0, 0),
            Child = new TextBlock { Text = entry.Kind, FontSize = 10.5, Foreground = Brushes.White },
        };
        coverGrid.Children.Add(kindBadge);

        var playBadge = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = new SolidColorBrush(Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 11, 11, 0),
            Child = new TextBlock
            {
                Text = "▶",
                FontSize = 12,
                Foreground = new SolidColorBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1, 0, 0, 0),
            },
        };
        coverGrid.Children.Add(playBadge);

        var coverTitle = new TextBlock
        {
            Text = entry.Title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 48,
            Margin = new Thickness(13, 0, 13, 11),
            VerticalAlignment = VerticalAlignment.Bottom,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 1, Opacity = 0.45 },
        };
        coverGrid.Children.Add(coverTitle);
        coverHost.Child = coverGrid;
        Grid.SetRow(coverHost, 0);
        grid.Children.Add(coverHost);

        // 文字信息
        var footer = new Grid { Margin = new Thickness(13, 11, 13, 10) };
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var title = new TextBlock
        {
            Text = entry.Title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Brush.TextPrimary"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        footer.Children.Add(title);
        var meta = new TextBlock
        {
            Text = entry.Note,
            FontSize = 10,
            Foreground = (Brush)FindResource("Brush.TextTertiary"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 4, 0, 0),
        };
        Grid.SetRow(meta, 1);
        footer.Children.Add(meta);
        Grid.SetRow(footer, 1);
        grid.Children.Add(footer);

        card.Child = grid;
        card.MouseEnter += (_, _) => card.BorderBrush = new SolidColorBrush(accent);
        card.MouseLeave += (_, _) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDF, 0xEA, 0xF4));
        card.MouseLeftButtonUp += (_, _) => Play(entry);

        entry.CoverHost = coverHost;
        entry.Meta = meta;
        return card;
    }

    private void Play(Entry entry)
    {
        if (_playRequested is null) return;
        ShowToast($"正在打开《{entry.Title}》…");
        _playRequested(entry.Url, entry.Title);
    }

    private async Task LoadCoversAsync(CancellationToken ct)
    {
        var gate = new SemaphoreSlim(4);
        var tasks = Catalog.Select(async entry =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var info = await _bilibili.GetMediaInfoAsync(entry.Url, entry.Title, ct);
                if (ct.IsCancellationRequested) return;
                var brush = info is null ? null : await CreateImageBrushAsync(info.Cover);
                await Dispatcher.InvokeAsync(() =>
                {
                    if (info is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(info.Meta)) entry.Meta.Text = info.Meta;
                        if (brush is not null)
                        {
                            entry.CoverHost.Background = brush;
                        }
                    }
                    TxtStatus.Text = "封面加载中…";
                });
            }
            catch
            {
                // 单条封面读取失败时保留渐变占位，不影响其他卡片。
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);
        if (!ct.IsCancellationRequested)
            TxtStatus.Text = "";
    }

    private async Task<Brush?> CreateImageBrushAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            using var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 760;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            return brush;
        }
        catch
        {
            return null;
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            _followMode = false;
            BtnFollowed.Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
            BtnFollowed.Foreground = new SolidColorBrush(Color.FromRgb(0x24, 0x38, 0x4F));
            BtnFollowed.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
            ContentScroll.Visibility = Visibility.Visible;
            FollowScroll.Visibility = Visibility.Collapsed;
            ApplyFilter(key);
            ContentScroll.ScrollToTop();
        }
    }

    private void Followed_Click(object sender, RoutedEventArgs e)
    {
        _followMode = !_followMode;
        BtnFollowed.Background = new SolidColorBrush(_followMode
            ? Color.FromRgb(0x4B, 0x7B, 0xC8)
            : Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
        BtnFollowed.Foreground = _followMode ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x24, 0x38, 0x4F));
        BtnFollowed.BorderBrush = new SolidColorBrush(_followMode
            ? Color.FromRgb(0x4B, 0x7B, 0xC8)
            : Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
        ContentScroll.Visibility = _followMode ? Visibility.Collapsed : Visibility.Visible;
        FollowScroll.Visibility = _followMode ? Visibility.Visible : Visibility.Collapsed;
        if (_followMode) BuildFollowed();
        else ApplyFilter(_filter);
        if (_followMode) FollowScroll.ScrollToTop();
        else ContentScroll.ScrollToTop();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = _query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        BtnClearSearch.Visibility = _query.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_followMode) BuildFollowed();
        else ApplyCardVisibility();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    private void ApplyFilter(string key)
    {
        _filter = key;
        _bangumiSection.Visibility = key == "concert" ? Visibility.Collapsed : Visibility.Visible;
        _concertSection.Visibility = key == "bangumi" ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in new[] { BtnAll, BtnBangumi, BtnConcert })
        {
            var selected = (string)button.Tag == key;
            button.Background = new SolidColorBrush(selected ? Color.FromRgb(0x4B, 0x7B, 0xC8) : Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
            button.Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x24, 0x38, 0x4F));
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.SemiBold;
            button.BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(0x4B, 0x7B, 0xC8) : Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
        }
        ApplyCardVisibility();
        ContentScroll.ScrollToTop();
    }

    private void ApplyCardVisibility()
    {
        foreach (var entry in Catalog)
        {
            var matches = _query.Length == 0 ||
                          entry.Title.Contains(_query, StringComparison.OrdinalIgnoreCase) ||
                          entry.Note.Contains(_query, StringComparison.OrdinalIgnoreCase);
            entry.Card.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async Task ApplyFollowPosterAsync(Entry entry, string url)
    {
        var brush = await CreateImageBrushAsync(url);
        if (brush is not null) entry.CoverHost.Background = brush;
    }

    private void OnFollowsChanged()
    {
        if (_followMode) Dispatcher.BeginInvoke(new Action(BuildFollowed));
    }

    private void BuildFollowed()
    {
        if (_follows is null) return;
        FollowPanel.Children.Clear();
        var items = _follows.Items.Where(i => _query.Length == 0 ||
                                             i.Title.Contains(_query, StringComparison.OrdinalIgnoreCase) ||
                                             i.Note.Contains(_query, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var item in items)
        {
            var entry = new Entry
            {
                Title = item.Title,
                Url = string.IsNullOrWhiteSpace(item.PlayUrl) ? item.Url : item.PlayUrl,
                Kind = item.Kind,
                Note = string.IsNullOrWhiteSpace(item.Note) ? item.Source : item.Note,
            };
            var card = BuildCard(entry);
            if (!string.IsNullOrWhiteSpace(item.Poster))
                _ = ApplyFollowPosterAsync(entry, item.Poster);
            FollowPanel.Children.Add(card);
        }
        FollowEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FollowEmpty.Text = _query.Length == 0
            ? "还没有追番作品。打开任意作品，在播放页点击「追番」就会同步到这里。"
            : $"没有找到与「{_query}」相关的追番作品。";
    }

    public void ShowToast(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        ToastText.Text = text;
        ToastPanel.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public void Suspend()
    {
        _toastTimer.Stop();
        ToastPanel.Visibility = Visibility.Collapsed;
        ContentScroll.IsHitTestVisible = false;
    }

    public void Resume()
    {
        ContentScroll.IsHitTestVisible = true;
    }

    public void Shutdown()
    {
        _cts?.Cancel();
        _toastTimer.Stop();
    }

    public void ResetScroll() => ContentScroll.ScrollToTop();

    /// <summary>截图工具兼容入口；原生卡片不依赖画布坐标点击。</summary>
    public void DebugTap(double x, double y) { }
}
