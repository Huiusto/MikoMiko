using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MikoMiko.Models;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace MikoMiko.Views;

/// <summary>0.1.50：软件内合集阅读器。读取合集页里的帖子列表，点击后交给帖子阅读器。</summary>
public partial class CollectionReaderView : UserControl
{
    private sealed record Entry(string PostId, string Title, string Summary);

    private static readonly Regex CardRegex = new(
        "href=\"(?<href>[^\"]*?/article/(?<id>\\d+))\"[^>]*>[\\s\\S]{0,3200}?<h3[^>]*title=\"(?<title>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex SummaryRegex = new(
        "class=\"mhy-article-card__content\">(?<body>[\\s\\S]{0,1600}?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private readonly UserCollectionItem _collection;
    private readonly Action<FeedItem> _openPost;
    private readonly Action _close;
    private bool _loading;

    public CollectionReaderView(UserCollectionItem collection, Action<FeedItem> openPost, Action close)
    {
        InitializeComponent();
        _collection = collection;
        _openPost = openPost;
        _close = close;
        TxtTitle.Text = string.IsNullOrWhiteSpace(collection.Title) ? "合集" : collection.Title;
        TxtSub.Text = collection.PostCount > 0 ? $"共 {collection.PostCount} 篇作品 · 正在读取…" : "正在读取合集内容…";
    }

    public async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        LoadingPanel.Visibility = Visibility.Visible;
        EmptyPanel.Visibility = Visibility.Collapsed;
        ListScroll.Visibility = Visibility.Collapsed;
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true,
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(35) };
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");
            var url = $"https://www.miyoushe.com/ys/collection/{Uri.EscapeDataString(_collection.Id)}?crawler=bingbot";
            var html = await http.GetStringAsync(url);

            var entries = new List<Entry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in CardRegex.Matches(html))
            {
                var id = match.Groups["id"].Value;
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
                var title = CleanText(match.Groups["title"].Value);
                if (string.IsNullOrWhiteSpace(title)) continue;
                var tail = html.Substring(match.Index, Math.Min(2600, html.Length - match.Index));
                var bodyMatch = SummaryRegex.Match(tail);
                var summary = bodyMatch.Success ? CleanText(bodyMatch.Groups["body"].Value) : string.Empty;
                entries.Add(new Entry(id, title, summary));
            }

            BuildList(entries);
            TxtSub.Text = entries.Count > 0
                ? $"共 {entries.Count} 篇作品 · 点击标题开始阅读"
                : "暂无可阅读的作品";
            ListScroll.Visibility = entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyPanel.Visibility = entries.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            TxtSub.Text = "合集读取失败：" + ex.Message;
            EmptyPanel.Visibility = Visibility.Visible;
            ListScroll.Visibility = Visibility.Collapsed;
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            _loading = false;
        }
    }

    private void BuildList(List<Entry> entries)
    {
        List.Children.Clear();
        for (var i = 0; i < entries.Count; i++)
            List.Children.Add(BuildCard(entries[i], i + 1));
    }

    private Border BuildCard(Entry entry, int index)
    {
        var card = new Border
        {
            Height = 68,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(14, 9, 14, 9),
            CornerRadius = new CornerRadius(10),
            Background = Theme("Brush.Card"),
            BorderBrush = Theme("Brush.BorderSoft"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = Theme("Brush.AccentSoft"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = index.ToString(),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme("Brush.Accent"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            },
        };
        grid.Children.Add(badge);

        var text = new StackPanel
        {
            Margin = new Thickness(2, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.Children.Add(new TextBlock
        {
            Text = entry.Title,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme("Brush.TextPrimary"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            text.Children.Add(new TextBlock
            {
                Text = entry.Summary,
                FontSize = 9.8,
                Foreground = Theme("Brush.TextTertiary"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 3, 0, 0),
            });
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var arrow = new TextBlock
        {
            Text = "›",
            FontSize = 18,
            Foreground = Theme("Brush.TextTertiary"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(arrow, 2);
        grid.Children.Add(arrow);

        card.Child = grid;
        card.MouseEnter += (_, _) =>
        {
            card.Background = Theme("Brush.CardHover");
            card.BorderBrush = Theme("Brush.Accent");
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = Theme("Brush.Card");
            card.BorderBrush = Theme("Brush.BorderSoft");
        };
        card.MouseLeftButtonUp += (_, _) => _openPost(new FeedItem
        {
            PostId = entry.PostId,
            Id = entry.PostId,
            Title = entry.Title,
            Summary = entry.Summary,
            WebUrl = $"https://www.miyoushe.com/ys/article/{entry.PostId}",
        });
        return card;
    }

    private Brush Theme(string key) => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private static string CleanText(string input)
    {
        var noTags = Regex.Replace(input, "<[^>]+>", " ");
        noTags = WebUtility.HtmlDecode(noTags).Replace("[图片]", " ");
        return Regex.Replace(noTags, "\\s+", " ").Trim();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _close();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();
}
