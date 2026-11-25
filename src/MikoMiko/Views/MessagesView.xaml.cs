using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MikoMiko.Services;

namespace MikoMiko.Views;

public partial class MessagesView : UserControl
{
    private readonly MiyousheMessageService _service;
    private readonly ObservableCollection<NotificationRow> _rows = new();
    private string _category = "reply";
    private CancellationTokenSource? _loadCts;
    private bool _loadedOnce;

    public MessagesView(string cookie)
    {
        InitializeComponent();
        _service = new MiyousheMessageService(cookie);
        NotificationList.ItemsSource = _rows;
        Loaded += OnLoaded;
    }

    /// <summary>由 Shell 接管：用软件内 WebView2 打开米游社页面。</summary>
    public Action<string, string>? OpenWebRequested { get; set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        ApplyCategoryStyle();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        _rows.Clear();
        EmptyPanel.Visibility = Visibility.Collapsed;
        AllReadButton.IsEnabled = false;

        if (!_service.HasCookie)
        {
            StatusText.Text = "尚未登录米游社，请先在「我的」页扫码登录后回来刷新。";
            ListHint.Text = string.Empty;
            ShowEmpty("未登录米游社", "登录后，回复、@ 我、收到的赞与系统通知会自动同步到这里。");
            return;
        }

        StatusText.Text = "正在同步米游社消息…";
        var counts = await _service.GetUnreadCountsAsync(ct);
        if (ct.IsCancellationRequested) return;
        ReplyCount.Text = counts.GetValueOrDefault("reply", 0).ToString();
        AtCount.Text = counts.GetValueOrDefault("active_mention", 0).ToString();
        LikeCount.Text = counts.GetValueOrDefault("comment", 0).ToString();
        SystemCount.Text = counts.GetValueOrDefault("system", 0).ToString();

        var (items, error) = await _service.GetListAsync(_category, 20, ct);
        if (ct.IsCancellationRequested) return;

        if (!string.IsNullOrWhiteSpace(error))
        {
            StatusText.Text = error;
            ShowEmpty("消息加载失败", error);
            AllReadButton.IsEnabled = true;
            return;
        }

        foreach (var item in items)
            _rows.Add(NotificationRow.From(item));

        LoadAvatars(items);
        StatusText.Text = items.Count > 0
            ? $"已同步 {items.Count} 条米游社消息 · 与网页端实时一致"
            : string.Empty;
        ListHint.Text = items.Count > 0 ? $"共 {items.Count} 条" : string.Empty;
        AllReadButton.IsEnabled = true;

        if (items.Count == 0)
        {
            ShowEmpty("这里还没有消息", _category switch
            {
                "active_mention" => "还没有人在帖子或评论里 @ 你。",
                "comment" => "还没有收到新的点赞。",
                "system" => "米游社暂时没有新的系统通知。",
                _ => "还没有新的回复，去米游社发帖互动吧。",
            });
        }
    }

    private async void LoadAvatars(IReadOnlyList<MiyousheNotification> items)
    {
        foreach (var row in _rows)
        {
            var item = items.FirstOrDefault(x => x.Id == row.Id);
            if (item is null || string.IsNullOrWhiteSpace(item.AvatarUrl)) continue;
            var image = await ImageLoader.LoadAsync(item.AvatarUrl, "https://www.miyoushe.com/");
            if (image is not null) row.Avatar = image;
        }
    }

    private void ShowEmpty(string title, string hint)
    {
        EmptyTitle.Text = title;
        EmptyHint.Text = hint;
        EmptyPanel.Visibility = Visibility.Visible;
    }

    private async void Category_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string ?? "reply";
        if (tag == _category && _rows.Count > 0) return;
        _category = tag;
        ApplyCategoryStyle();
        ListTitle.Text = CategoryTitle(tag);
        await ReloadAsync();
    }

    private void ApplyCategoryStyle()
    {
        var cards = new[] { ("reply", ReplyCard), ("active_mention", AtCard), ("comment", LikeCard), ("system", SystemCard) };
        foreach (var (key, card) in cards)
        {
            var selected = key == _category;
            card.Background = (Brush)FindResource(selected ? "Brush.AccentSoft" : "Brush.Surface");
            card.BorderBrush = (Brush)FindResource(selected ? "Brush.Accent" : "Brush.Border");
        }
        ListTitle.Text = CategoryTitle(_category);
    }

    private static string CategoryTitle(string tag) => tag switch
    {
        "active_mention" => "主动 @ 我",
        "comment" => "收到的赞",
        "system" => "系统通知",
        _ => "回复我的",
    };

    private async void AllRead_Click(object sender, RoutedEventArgs e)
    {
        if (!_service.HasCookie) return;
        AllReadButton.IsEnabled = false;
        foreach (var category in new[] { "reply", "active_mention", "comment", "system" })
            await _service.ClearUnreadAsync(category);
        ReplyCount.Text = "0";
        AtCount.Text = "0";
        LikeCount.Text = "0";
        SystemCount.Text = "0";
        ListHint.Text = "全部已读";
        await ReloadAsync();
    }

    private void OpenCenter_Click(object sender, RoutedEventArgs e)
        => OpenWebRequested?.Invoke("https://www.miyoushe.com/ys/notifications/reply", "米游社消息中心");

    private void Notification_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var url = (sender as FrameworkElement)?.Tag as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url)) return;
        OpenWebRequested?.Invoke(url, "米游社 · " + CategoryTitle(_category));
    }
}

public sealed class NotificationRow : INotifyPropertyChanged
{
    private BitmapImage? _avatar;

    public string Id { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string Initial => string.IsNullOrEmpty(Nickname) ? "米" : Nickname[..1].ToUpperInvariant();
    public string Content { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string ActionText { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public string ActionUrl { get; init; } = string.Empty;
    public bool Unread { get; init; }
    public Visibility UnreadVisibility => Unread ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OriginVisibility => string.IsNullOrWhiteSpace(Origin) ? Visibility.Collapsed : Visibility.Visible;

    public BitmapImage? Avatar
    {
        get => _avatar;
        set
        {
            if (_avatar == value) return;
            _avatar = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Avatar)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static NotificationRow From(MiyousheNotification item) => new()
    {
        Id = item.Id,
        Nickname = item.Nickname,
        Content = string.IsNullOrWhiteSpace(item.Content) ? "（无正文）" : item.Content,
        Origin = item.Origin,
        ActionText = item.Category switch
        {
            "active_mention" => "在帖子中提到了你",
            "comment" => "赞了你的内容",
            "system" => "系统通知",
            _ => "回复了你的内容",
        },
        TimeText = TimeAgo(item.CreatedAt),
        ActionUrl = item.ActionUrl,
        Unread = item.Unread,
    };

    private static string TimeAgo(long unixSeconds)
    {
        if (unixSeconds <= 0) return string.Empty;
        var time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        var diff = DateTimeOffset.Now - time;
        if (diff.TotalMinutes < 1) return "刚刚";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} 分钟前";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} 小时前";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} 天前";
        return time.ToString("yyyy-MM-dd");
    }
}
