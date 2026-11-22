using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>视频页：随机视频流，上下滑动切换。</summary>
public sealed partial class VideoViewModel : ObservableObject
{
    private readonly ContentService _content;
    private List<FeedItem> _all = new();
    private readonly HashSet<string> _seen = new();

    public VideoViewModel(ContentService content)
    {
        _content = content;
    }

    public ObservableCollection<FeedItem> Videos { get; } = new();
    public ObservableCollection<FeedItem> Related { get; } = new();
    public ObservableCollection<CommentItem> Comments { get; } = new();

    [ObservableProperty] private FeedItem? _selected;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingMore;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private bool _hasSource;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private double _volume = 60;
    [ObservableProperty] private bool _showDanmaku = true;
    [ObservableProperty] private string _danmakuText = string.Empty;
    [ObservableProperty] private string _speedText = "倍速";
    [ObservableProperty] private string _filter = string.Empty;
    [ObservableProperty] private string _indexText = string.Empty;
    [ObservableProperty] private bool _isLiked;
    [ObservableProperty] private bool _isFavorited;
    [ObservableProperty] private string _toast = string.Empty;
    [ObservableProperty] private bool _isCommentsOpen;
    [ObservableProperty] private string _commentInput = string.Empty;
    [ObservableProperty] private string _commentStatus = string.Empty;
    [ObservableProperty] private bool _canComment;
    [ObservableProperty] private string _qualityText = "自动";
    [ObservableProperty] private bool _isBuffering;

    public async Task LoadAsync()
    {
        if (_all.Count > 0)
        {
            return;
        }

        await ReloadAsync();
    }

    /// <summary>点「视频」导航时刷新一批新的随机视频（覆盖所有用户 / 所有板块）。</summary>
    public async Task RefreshAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IsLoading = true;
        IsPlaying = false;
        StatusText = Localization.Loc.Instance["Common.Loading"];
        try
        {
            _seen.Clear();
            _all = (await _content.GetRandomVideosAsync(12, _seen)).ToList();
            IsDemo = _content.IsDemo;
            Videos.Clear();
            foreach (var item in _all) Videos.Add(item);
            if (Videos.Count > 0) Select(Videos[0]);
            StatusText = string.Empty;
        }
        catch
        {
            StatusText = Localization.Loc.Instance["Common.Fail"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>离开视频页时暂停，不保留声音与播放状态。</summary>
    public void Pause()
    {
        IsPlaying = false;
    }

    public void Select(FeedItem item)
    {
        Selected = item;
        HasSource = !string.IsNullOrWhiteSpace(item.VideoUrl);
        IsPlaying = false;
        IsLiked = false;
        IsFavorited = false;
        Toast = string.Empty;
        StatusText = string.Empty;
        Related.Clear();
        foreach (var other in _all.Where(v => v.PostId != item.PostId).Take(8)) Related.Add(other);

        // 0.1.4 去掉「第 n/N 个」序号；在播放数前展示作者关注数。
        IndexText = string.Empty;
        CanComment = _content.Api.HasCookie;
        CommentStatus = string.Empty;
        Comments.Clear();
        _ = LoadCommentsAsync(item);
        if (string.IsNullOrWhiteSpace(item.AuthorFansText))
        {
            _ = LoadAuthorFansAsync(item);
        }

        if (!HasSource) _ = ResolveSourceAsync(item);
        _ = PreloadNextAsync();
    }

    private async Task LoadAuthorFansAsync(FeedItem item)
    {
        var fans = await _content.Api.GetAuthorFansAsync(item.AuthorUid);
        if (string.IsNullOrEmpty(fans) || Selected?.PostId != item.PostId) return;
        item.AuthorFansText = fans;
        if (Selected?.PostId == item.PostId) OnPropertyChanged(nameof(Selected));
    }

    /// <summary>右侧评论：同步米游社该视频帖的一级热评。</summary>
    private async Task LoadCommentsAsync(FeedItem item)
    {
        try
        {
            var replies = await _content.Api.GetRepliesAsync(item.PostId);
            if (Selected?.PostId != item.PostId) return;
            Comments.Clear();
            foreach (var reply in replies) Comments.Add(reply);
        }
        catch
        {
            // 评论区加载失败不影响播放
        }
    }

    [RelayCommand]
    private void ToggleComments() => IsCommentsOpen = !IsCommentsOpen;

    [RelayCommand]
    private async Task SendCommentAsync()
    {
        if (Selected is null) return;
        if (!CanComment)
        {
            CommentStatus = "请先在「我的」扫码登录后再评论";
            return;
        }

        var text = CommentInput.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        CommentStatus = "正在发送…";
        var result = await _content.Api.ReleaseReplyAsync(Selected.PostId, text);
        if (!result.Ok)
        {
            CommentStatus = result.Message;
            return;
        }

        CommentInput = string.Empty;
        CommentStatus = "评论成功";
        await LoadCommentsAsync(Selected);
    }

    /// <summary>「自动」按钮真实切清晰度：自动 → 1080P → 720P → 480P 循环。</summary>
    public async Task ChangeQualityAsync()
    {
        var next = QualityText switch
        {
            "自动" => "1080P",
            "1080P" => "720P",
            "720P" => "480P",
            _ => "自动",
        };
        QualityText = next;
        _content.Api.Quality = next == "自动" ? "1080P" : next;
        await RefreshSourceAsync();
    }

    /// <summary>预加载后三个视频的详情与播放直链，下滑时无需再等接口。</summary>
    private async Task PreloadNextAsync()
    {
        if (Selected is null) return;
        var index = Videos.IndexOf(Selected);
        for (var i = 1; i <= 3 && index + i < Videos.Count; i++)
        {
            var item = Videos[index + i];
            try
            {
                if (string.IsNullOrWhiteSpace(item.VideoUrl))
                {
                    var detail = await _content.GetDetailAsync(item);
                    if (!string.IsNullOrWhiteSpace(detail.VideoUrl)) item.VideoUrl = detail.VideoUrl;
                }

                if (string.IsNullOrWhiteSpace(item.AuthorFansText))
                {
                    item.AuthorFansText = await _content.Api.GetAuthorFansAsync(item.AuthorUid);
                }
            }
            catch
            {
                // 预加载失败下次进入再补
            }
        }
    }

    /// <summary>向下滑：切到下一个随机视频，接近末尾时自动补充。</summary>
    public async Task NextAsync()
    {
        if (Videos.Count == 0) return;

        var index = Selected is null ? 0 : Videos.IndexOf(Selected);
        if (index + 4 >= Videos.Count - 1) await LoadMoreAsync();
        if (index >= Videos.Count - 1) return;

        var wasPlaying = IsPlaying;
        Select(Videos[index + 1]);
        if (wasPlaying) IsPlaying = true;
    }

    /// <summary>向上滑：回到上一个视频。</summary>
    public void Previous()
    {
        if (Selected is null) return;
        var index = Videos.IndexOf(Selected);
        if (index <= 0) return;

        var wasPlaying = IsPlaying;
        Select(Videos[index - 1]);
        if (wasPlaying) IsPlaying = true;
    }

    /// <summary>追加一批随机视频。</summary>
    public async Task LoadMoreAsync()
    {
        if (IsLoadingMore || IsLoading) return;

        IsLoadingMore = true;
        try
        {
            var more = await _content.GetRandomVideosAsync(8, _seen);
            foreach (var item in more)
            {
                if (!_seen.Add(item.PostId)) continue;
                _all.Add(item);
                Videos.Add(item);
            }

            if (Selected is not null)
            {
                IndexText = string.Empty;
            }
        }
        catch
        {
            // 忽略
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <summary>播放失败时重新向详情接口要一次带签名的直链（列表里的地址常已过期）。</summary>
    public async Task<bool> RefreshSourceAsync()
    {
        if (Selected is null) return false;
        try
        {
            var detail = await _content.GetDetailAsync(Selected);
            if (string.IsNullOrWhiteSpace(detail.VideoUrl)) return false;
            Selected.VideoUrl = detail.VideoUrl;
            HasSource = true;
            OnPropertyChanged(nameof(HasSource));
            OnPropertyChanged(nameof(Selected));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int _skipAttempts;

    /// <summary>列表页可能只给出封面，点开后再补一次详情以拿到可播放直链。</summary>
    private async Task ResolveSourceAsync(FeedItem item)
    {
        try
        {
            var detail = await _content.GetDetailAsync(item);
            if (!string.IsNullOrWhiteSpace(detail.VideoUrl))
            {
                _skipAttempts = 0;
                item.VideoUrl = detail.VideoUrl;
                if (Selected?.PostId == item.PostId)
                {
                    HasSource = true;
                    OnPropertyChanged(nameof(Selected));
                }
                return;
            }
        }
        catch
        {
            // 保持无源状态
        }

        // 这条内容确实没有视频（图文帖混进了视频流），自动滑到下一条
        if (Selected?.PostId == item.PostId && _skipAttempts < 4)
        {
            _skipAttempts++;
            await NextAsync();
        }
    }

    [RelayCommand]
    private void Play(FeedItem? item)
    {
        if (item is not null) Select(item);
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (Selected is null) return;
        if (!HasSource)
        {
            StatusText = "正在缓冲视频…";
            _ = ResolveAndPlayAsync(Selected);
            return;
        }
        IsPlaying = !IsPlaying;
    }

    private async Task ResolveAndPlayAsync(FeedItem item)
    {
        await ResolveSourceAsync(item);
        if (Selected?.PostId == item.PostId && HasSource) IsPlaying = true;
    }

    [RelayCommand]
    private Task Next() => NextAsync();

    [RelayCommand]
    private void PreviousVideo() => Previous();

    /// <summary>点赞：本地立即反馈，绑定 Cookie 时同步到米游社账号。</summary>
    [RelayCommand]
    private async Task Like()
    {
        if (Selected is null || IsLiked) return;
        IsLiked = true;
        var ok = await _content.Api.LikePostAsync(Selected.PostId);
        Toast = ok
            ? Localization.Loc.Instance["Video.Sync"]
            : Localization.Loc.Instance["Video.SyncFail"];
    }

    /// <summary>收藏：同上。</summary>
    [RelayCommand]
    private async Task Favorite()
    {
        if (Selected is null || IsFavorited) return;
        IsFavorited = true;
        var ok = await _content.Api.FavoritePostAsync(Selected.PostId);
        Toast = ok
            ? Localization.Loc.Instance["Video.Sync"]
            : Localization.Loc.Instance["Video.SyncFail"];
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (Selected is null || string.IsNullOrWhiteSpace(Selected.WebUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Selected.WebUrl)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // 忽略打开失败
        }
    }

    public void ApplySearch(string text)
    {
        Filter = text ?? string.Empty;
        var keyword = Filter.Trim();
        Videos.Clear();
        var list = string.IsNullOrEmpty(keyword)
            ? _all
            : _all.Where(v => v.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                              || v.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        foreach (var item in list) Videos.Add(item);
    }
}
