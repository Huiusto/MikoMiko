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
/// 追番 / 影视原生目录页：只展示用户指定的五部崩坏3 动画，
/// 点击卡片后在软件内解析樱花TV 的公开播放线路；无法取流时再回退到站点网页播放器。
/// </summary>
public partial class AnimeHubView : UserControl
{
    private sealed class Entry
    {
        public required string Title;
        public required string Url;
        public required string Kind;
        public required string Note;
        public string Cover = "";
        public Border Card = null!;
        public Border CoverHost = null!;
        public TextBlock Meta = null!;
    }

    private static readonly Entry[] Catalog =
    {
        new() { Title = "女武神的餐桌 第一季", Url = "https://www.yhdmtv.cc/v/298947/1", Kind = "推荐", Note = "崩坏3 · 美食日常" },
        new() { Title = "女武神的餐桌 第二季", Url = "https://www.yhdmtv.cc/v/298945/1", Kind = "推荐", Note = "崩坏3 · 第二季" },
        new() { Title = "人偶学园", Url = "https://www.yhdmtv.cc/v/77911/1", Kind = "推荐", Note = "崩坏3 · 学园日常" },
        new() { Title = "黄金庭院：冬日里的新年愿望", Url = "https://www.yhdmtv.cc/v/76871/1", Kind = "推荐", Note = "崩坏3 · 冬日特别篇", Cover = "pack://application:,,,/Assets/Anime/golden-courtyard.jpg" },
        new() { Title = "旅途危机！圣芙蕾雅号特别事件", Url = "https://www.yhdmtv.cc/v/683596/1", Kind = "推荐", Note = "崩坏3 · 特别动画", Cover = "pack://application:,,,/Assets/Anime/travel-crisis.jpg" },
        new() { Title = "《原神》长期项目启动·概念PV", Url = "https://www.bilibili.com/video/BV15B4y1n7dg", Kind = "PV", Note = "原神 · 官方概念 PV", Cover = "pack://application:,,,/Assets/Anime/genshin-concept-pv.jpg" },
        new() { Title = "《崩坏：星穹铁道》原创动画•概念PV——「死于午后」", Url = "https://www.bilibili.com/video/BV1YXDfBUETP", Kind = "PV", Note = "星穹铁道 · 原创动画概念 PV", Cover = "pack://application:,,,/Assets/Anime/starrail-concept-pv.jpg" },
    };

    private static readonly HttpClient Http = CreateHttpClient();
    private readonly Action<string, string> _playRequested;
    private readonly BilibiliAnimeService _bilibili = new();
    private readonly AnimeFollowService? _follows;
    private readonly DispatcherTimer _toastTimer;
    private CancellationTokenSource? _cts;
    private WrapPanel _recommendPanel = null!;
    private Border _recommendSection = null!;
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
        _recommendSection = CreateSection("推荐", out _recommendPanel);
        Sections.Children.Add(_recommendSection);

        foreach (var entry in Catalog)
        {
            var card = BuildCard(entry);
            entry.Card = card;
            _recommendPanel.Children.Add(card);
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
            Background = new SolidColorBrush(Color.FromRgb(0x4B, 0x7B, 0xC8)),
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
        var accent = Color.FromRgb(0x4B, 0x7B, 0xC8);
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
                string? cover;
                string? meta = null;
                if (!string.IsNullOrWhiteSpace(entry.Cover))
                {
                    cover = entry.Cover;
                }
                else if (entry.Url.Contains("yhdmtv", StringComparison.OrdinalIgnoreCase))
                {
                    var info = await GetYhdmtvInfoAsync(entry.Url, ct);
                    cover = info.Cover;
                    meta = info.Meta;
                }
                else
                {
                    var info = await _bilibili.GetMediaInfoAsync(entry.Url, entry.Title, ct);
                    cover = info?.Cover;
                    meta = info?.Meta;
                }
                if (ct.IsCancellationRequested) return;
                var brush = string.IsNullOrWhiteSpace(cover)
                    ? null
                    : await CreateImageBrushAsync(cover, "https://www.yhdmtv.cc/");
                await Dispatcher.InvokeAsync(() =>
                {
                    if (!string.IsNullOrWhiteSpace(meta)) entry.Meta.Text = meta;
                    if (brush is not null) entry.CoverHost.Background = brush;
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

    private async Task<(string Cover, string Meta)> GetYhdmtvInfoAsync(string pageUrl, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
        request.Headers.Referrer = new Uri("https://www.yhdmtv.cc/");
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return ("", "");
        var html = await response.Content.ReadAsStringAsync(ct);

        var posterMatch = System.Text.RegularExpressions.Regex.Match(html,
            @"<div\s+class=""module-info-poster"">.*?data-original=""(?<url>https?://[^""]+)""",
            System.Text.RegularExpressions.RegexOptions.Singleline |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var cover = posterMatch.Success ? posterMatch.Groups["url"].Value : string.Empty;

        var metaMatch = System.Text.RegularExpressions.Regex.Match(html,
            @"<meta\s+name=""description""\s+content=""《[^》]+》(?<meta>[^。]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var meta = metaMatch.Success ? metaMatch.Groups["meta"].Value.Trim() : string.Empty;
        return (cover, meta);
    }

    private async Task<Brush?> CreateImageBrushAsync(string url, string? referer = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            BitmapImage image;
            if (url.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
            {
                image = new BitmapImage(new Uri(url, UriKind.Absolute));
            }
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(referer))
                    request.Headers.Referrer = new Uri(referer, UriKind.Absolute);
                using var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                var bytes = await response.Content.ReadAsByteArrayAsync();
                using var stream = new MemoryStream(bytes);
                image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 760;
                image.StreamSource = stream;
                image.EndInit();
            }
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
        _filter = "recommend";
        _recommendSection.Visibility = Visibility.Visible;
        BtnRecommend.Background = new SolidColorBrush(Color.FromRgb(0x4B, 0x7B, 0xC8));
        BtnRecommend.Foreground = Brushes.White;
        BtnRecommend.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x7B, 0xC8));
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
