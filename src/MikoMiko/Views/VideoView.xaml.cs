using MikoMiko.Models;
using MikoMiko.Services;
using MikoMiko.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace MikoMiko.Views;

public partial class VideoView : UserControl
{
    private readonly VideoViewModel _vm;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _positionTimer;
    private bool _updatingSlider;
    private double _speed = 1.0;
    private bool _autoPlay;
    private bool _retriedSource;
    private DateTime _lastSwipe = DateTime.MinValue;
    private string? _preparedPath;
    private bool _playRequested;
    private bool _updatePositionLogged;
    private string? _remoteUrl;
    private string? _proxyUrl;
    private bool _retriedRemote;
    private double _pendingSeekSeconds = -1;
    private readonly DanmakuService _danmaku = new();
    private double _lastDanmakuSecond = -10;

    public VideoView(VideoViewModel viewModel, AppSettings settings)
    {
        InitializeComponent();
        _vm = viewModel;
        _settings = settings;
        DataContext = viewModel;

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _positionTimer.Tick += (_, _) => UpdatePosition();

        ApplyPlayerSettings();
        _vm.PropertyChanged += OnViewModelChanged;
        Media.MediaOpened += OnMediaOpened;
        Media.MediaEnded += OnMediaEnded;
        Media.MediaFailed += OnMediaFailed;
        Loaded += (_, _) =>
        {
            ApplyPlayerSettings();
            SyncSelected();
            _positionTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            PausePlayback();
            Media.Source = null;
        };
    }

    /// <summary>离开视频页 / 切到别的页面时暂停播放与进度刷新。</summary>
    public void PausePlayback()
    {
        _vm.IsPlaying = false;
        _positionTimer.Stop();
        try
        {
            Media.Pause();
            Media.IsMuted = true;
            Media.Volume = 0;
        }
        catch { }
        UpdatePlayIcon();
    }

    /// <summary>上下滑动（滚轮 / 触摸板）切换随机视频，像刷短视频一样。</summary>
    private async void PlayerCard_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DateTime.Now - _lastSwipe < TimeSpan.FromMilliseconds(280)) return;
        if (Math.Abs(e.Delta) < 40) return;

        _lastSwipe = DateTime.Now;
        e.Handled = true;

        if (e.Delta < 0)
        {
            await _vm.NextAsync();
        }
        else
        {
            _vm.Previous();
        }

        if (_settings.AutoPlayOnSwipe)
        {
            _playRequested = true;
            _vm.IsPlaying = true;
        }
    }

    /// <summary>点击画面切换播放 / 暂停；点按钮、滑块时不触发。</summary>
    private void PlayerCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (FindAncestor<Slider>(e.OriginalSource as DependencyObject) is not null) return;
        if (FindAncestor<Thumb>(e.OriginalSource as DependencyObject) is not null) return;
        if (FindAncestor<System.Windows.Controls.TextBox>(e.OriginalSource as DependencyObject) is not null) return;
        TogglePlay();
    }

    private void TogglePlay()
    {
        if (!_vm.HasSource)
        {
            _playRequested = true;
            SyncSelected();
        }

        if (_vm.TogglePlayCommand.CanExecute(null)) _vm.TogglePlayCommand.Execute(null);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void ApplyPlayerSettings()
    {
        if (Media is null) return;
        Media.Volume = Math.Clamp(_vm.Volume / 100.0, 0, 1);
        Media.IsMuted = _vm.IsMuted;
        VolumeSlider.Value = _vm.Volume;
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VideoViewModel.Selected):
                _retriedSource = false;
                SyncSelected();
                break;
            case nameof(VideoViewModel.HasSource):
                if (_vm.HasSource && Media.Source is null) SyncSelected();
                break;
            case nameof(VideoViewModel.IsPlaying):
                if (_vm.IsPlaying) PlayMedia();
                else
                {
                    Media.Pause();
                    // 0.1.4：暂停必须同时静音并把音量为 0，修复「画面停住但声音还在播」；
                    // 保留当前画面帧，不再盖回封面。
                    Media.IsMuted = true;
                    Media.Volume = 0;
                }
                UpdatePlayIcon();
                break;
            case nameof(VideoViewModel.IsMuted):
                Media.IsMuted = _vm.IsMuted;
                UpdateMuteIcon();
                break;
            case nameof(VideoViewModel.Volume):
                Media.Volume = Math.Clamp(_vm.Volume / 100.0, 0, 1);
                break;
        }
    }

    /// <summary>
    /// 换视频时先把直链下载到本地缓存，再交给 MediaElement。
    /// WPF 的 Media Foundation 直接播米游社带签名的 HTTPS 长直链容易失败，落盘播放最稳。
    /// </summary>
    private void SyncSelected()
    {
        Media.Pause();
        Media.Source = null;
        VideoStreamProxy.Instance.Release(_proxyUrl);
        _proxyUrl = null;
        _preparedPath = null;
        CoverHost.Visibility = Visibility.Visible;
        ProgressSlider.Value = 0;
        _updatingSlider = true;
        TimeText.Text = "00:00 / 00:00";
        _updatingSlider = false;
        if (PlayedBar is not null) PlayedBar.Width = 0;
        if (BufferBar is not null) BufferBar.Width = 0;
        DanmakuHost.Children.Clear();
        _lastDanmakuSecond = -10;

        var local = _vm.Selected?.VideoUrl;
        if (string.IsNullOrWhiteSpace(local))
        {
            Media.Source = null;
            return;
        }

        var url = local!;
        _retriedRemote = false;

        // 已经缓存过就直接本地播放，最稳
        var cached = VideoCacheService.TryGetCached(url);
        if (cached is not null && File.Exists(cached))
        {
            ApplySource(cached);
            if (_vm.IsPlaying || _playRequested) PlayMedia();
            return;
        }

        // 0.1.4：通过本机 Range 代理边缓冲边播，播放器发多少字节请求就取多少，
        // 不再等整段落盘，拖动进度条也会触发新的 Range 请求。
        _remoteUrl = url;
        _preparedPath = null;
        var proxy = VideoStreamProxy.Instance.GetProxyUrl(url);
        if (proxy is null)
        {
            Media.Source = new Uri(url, UriKind.Absolute);
        }
        else
        {
            _proxyUrl = proxy;
            Media.Source = new Uri(proxy, UriKind.Absolute);
        }
        MediaLog("proxy source " + (_proxyUrl ?? url));
        if (_vm.IsPlaying || _playRequested) PlayMedia();
    }

    private void ApplySource(string path)
    {
        VideoStreamProxy.Instance.Release(_proxyUrl);
        _proxyUrl = null;
        _remoteUrl = null;
        _preparedPath = path;
        Media.Source = new Uri(path, UriKind.Absolute);
        MediaLog($"local source {path}");
        _vm.HasSource = true;
    }

    private async Task PrepareAsync(string url, CancellationToken ct)
    {
        _vm.StatusText = "正在缓冲视频…";
        var postId = _vm.Selected?.PostId;
        var progress = new Progress<double>(p =>
        {
            if (_vm.Selected?.PostId == postId) _vm.StatusText = $"正在缓冲视频 {p:P0}";
        });

        var path = await VideoCacheService.GetLocalAsync(url, progress, ct);
        if (ct.IsCancellationRequested || _vm.Selected?.PostId != postId) return;

        if (path is null)
        {
            _vm.StatusText = Localization.Loc.Instance["Video.NoSource"];
            _vm.IsPlaying = false;
            return;
        }

        ApplySource(path);
        _vm.StatusText = string.Empty;
        if (_vm.IsPlaying || _playRequested) PlayMedia();
    }

    private void PlayMedia()
    {
        if (Media.Source is null)
        {
            if (!string.IsNullOrWhiteSpace(_vm.Selected?.VideoUrl))
            {
                // 直链还在下载：记住这次播放意图，缓存好之后自动开播
                _playRequested = true;
                _vm.StatusText = "正在缓冲视频…";
                SyncSelected();
            }
            else
            {
                _vm.StatusText = Localization.Loc.Instance["Video.NoSource"];
                _vm.IsPlaying = false;
            }
            return;
        }

        _playRequested = false;
        _vm.StatusText = string.Empty;
        _updatePositionLogged = false;
        Media.IsMuted = _vm.IsMuted;
        Media.Volume = Math.Clamp(_vm.Volume / 100.0, 0, 1);
        Media.Play();
        CoverHost.Visibility = Visibility.Collapsed;
        Media.SpeedRatio = _speed;
        Media.Volume = Math.Clamp(_vm.Volume / 100.0, 0, 1);
        Media.IsMuted = _vm.IsMuted;
        _positionTimer.Start();
    }

    private static void MediaLog(string message)
    {
        _ = message;
#if DEBUG
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "miko-media.log");
            System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
        }
        catch
        {
            // 忽略日志失败
        }
#endif
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        _retriedRemote = false;
        MediaLog($"opened duration={Media.NaturalDuration.TimeSpan} size={Media.NaturalVideoWidth}x{Media.NaturalVideoHeight}");
        CoverHost.Visibility = Visibility.Collapsed;
        _vm.StatusText = string.Empty;
        if (Media.NaturalDuration.HasTimeSpan)
        {
            ProgressSlider.Maximum = Math.Max(1, Media.NaturalDuration.TimeSpan.TotalSeconds);
        }
        if (_pendingSeekSeconds >= 0)
        {
            try
            {
                Media.Position = TimeSpan.FromSeconds(Math.Min(_pendingSeekSeconds,
                    Media.NaturalDuration.HasTimeSpan ? Media.NaturalDuration.TimeSpan.TotalSeconds : _pendingSeekSeconds));
            }
            catch
            {
                // 忽略
            }
            _pendingSeekSeconds = -1;
        }

        UpdatePosition();
    }

    private async void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        _vm.IsPlaying = false;
        CoverHost.Visibility = Visibility.Visible;
        UpdatePlayIcon();

        // 播完自动下一个，可在「设置 → 播放」关闭
        if (_settings.AutoNextVideo)
        {
            await _vm.NextAsync();
            if (_settings.AutoPlayOnSwipe)
            {
                _playRequested = true;
                _vm.IsPlaying = true;
            }
        }
    }

    private async void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        MediaLog("failed " + e.ErrorException?.Message);
        var wasPlaying = _vm.IsPlaying;
        _vm.IsPlaying = false;
        CoverHost.Visibility = Visibility.Visible;
        UpdatePlayIcon();

        // 在线直链播不了：下载到本地缓存后重试
        if (!_retriedRemote && _remoteUrl is not null)
        {
            _retriedRemote = true;
            var remote = _remoteUrl;
            _vm.StatusText = "正在缓冲视频…";
            var progress = new Progress<double>(p => _vm.StatusText = $"正在缓冲视频 {p:P0}");
            var path = await VideoCacheService.GetLocalAsync(remote, progress);
            if (path is not null)
            {
                ApplySource(path);
                _vm.StatusText = string.Empty;
                if (wasPlaying || _playRequested)
                {
                    _vm.IsPlaying = true;
                    PlayMedia();
                }
                return;
            }
        }

        // 直链带时效签名，过期的地址再向详情接口要一次
        if (!_retriedSource)
        {
            _retriedSource = true;
            if (await _vm.RefreshSourceAsync())
            {
                SyncSelected();
                return;
            }
        }

        _vm.StatusText = Localization.Loc.Instance["Video.NoSource"];
    }

    /// <summary>开发期截图用：走真实的“点击播放”流程。</summary>
    public void PlayForCapture()
    {
        _vm.TogglePlayCommand.Execute(null);
    }

    private void UpdatePosition()
    {
        if (Media.Source is null) return;

        if (!_updatePositionLogged && Media.Position.TotalMilliseconds > 0)
        {
            _updatePositionLogged = true;
            MediaLog($"progress pos={Media.Position} duration={Media.NaturalDuration.TimeSpan} w={Media.NaturalVideoWidth} h={Media.NaturalVideoHeight}");
        }
        _updatingSlider = true;
        ProgressSlider.Value = Math.Clamp(Media.Position.TotalSeconds, 0, ProgressSlider.Maximum);
        _updatingSlider = false;
        UpdateProgressBars();
        UpdateTimeText();

        // 弹幕：按播放时间从本地库捞取当前秒附近的弹幕并滚动展示
        if (_vm.ShowDanmaku && _vm.Selected is not null)
        {
            var second = Math.Floor(Media.Position.TotalSeconds);
            if (Math.Abs(second - _lastDanmakuSecond) >= 1)
            {
                _lastDanmakuSecond = second;
                foreach (var entry in _danmaku.Get(_vm.Selected.PostId, second, 1))
                {
                    ShowDanmakuText(entry.Text, entry.Author);
                }
            }
        }
    }

    /// <summary>白色=未加载，灰色=已缓冲，米蓝=已播放。</summary>
    private void UpdateProgressBars()
    {
        if (ProgressTrack is null) return;
        var width = ProgressTrack.ActualWidth;
        if (width <= 0) return;

        var duration = Media.NaturalDuration.HasTimeSpan ? Media.NaturalDuration.TimeSpan.TotalSeconds : 0;
        var played = duration > 0 ? Math.Clamp(Media.Position.TotalSeconds / duration, 0, 1) : 0;
        var buffered = Math.Clamp(Media.BufferingProgress, 0, 1);
        if (buffered < played) buffered = played;

        PlayedBar.Width = width * played;
        BufferBar.Width = width * buffered;
    }

    private void UpdateTimeText()
    {
        var duration = Media.NaturalDuration.HasTimeSpan ? Media.NaturalDuration.TimeSpan : TimeSpan.Zero;
        TimeText.Text = $"{Format(Media.Position)} / {Format(duration)}";
    }

    private static string Format(TimeSpan span)
        => span.TotalHours >= 1
            ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";

    private void UpdatePlayIcon()
    {
        PlayPauseIcon.Data = System.Windows.Media.Geometry.Parse(
            _vm.IsPlaying ? "M0 0 H4 V14 H0 Z M7 0 H11 V14 H7 Z" : "M0 0 L11 7 L0 14 Z");
    }

    private void UpdateMuteIcon()
    {
        MuteIcon.Data = System.Windows.Media.Geometry.Parse(
            _vm.IsMuted
                ? "M0 4 H4 L8 0.5 V13.5 L4 10 H0 Z M10.5 4 L15.5 9 M15.5 4 L10.5 9"
                : "M0 4 H4 L8 0.5 V13.5 L4 10 H0 Z M10 2.5 A5.5 5.5 0 0 1 10 11.5 M12 0 A8.5 8.5 0 0 1 12 14");
    }

    // ---------------------------------------------------------------- 事件

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.TogglePlayCommand.CanExecute(null)) _vm.TogglePlayCommand.Execute(null);
    }

    private void ReturnCover_Click(object sender, RoutedEventArgs e)
    {
        Media.Pause();
        _vm.IsPlaying = false;
        UpdatePlayIcon();
    }

    private void Progress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingSlider || Media is null || Media.Source is null) return;
        try
        {
            Media.Position = TimeSpan.FromSeconds(e.NewValue);
        }
        catch
        {
            // 视频未就绪时忽略
        }
    }

    private void Volume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Media is null) return;
        Media.Volume = Math.Clamp(e.NewValue / 100.0, 0, 1);
        if (_vm is not null && Math.Abs(_vm.Volume - e.NewValue) > 0.5) _vm.Volume = e.NewValue;
    }

    private void Mute_Click(object sender, RoutedEventArgs e) => _vm.IsMuted = !_vm.IsMuted;

    private void Speed_Click(object sender, RoutedEventArgs e)
    {
        _speed = _speed switch
        {
            < 1.25 => 1.25,
            < 1.5 => 1.5,
            < 2.0 => 2.0,
            _ => 1.0,
        };
        _vm.SpeedText = _speed <= 1.0 ? "倍速" : $"{_speed:0.##}×";
        Media.SpeedRatio = _speed;
    }

    private void Danmaku_Click(object sender, RoutedEventArgs e) => _vm.ShowDanmaku = !_vm.ShowDanmaku;

    private async void Auto_Click(object sender, RoutedEventArgs e)
    {
        // 0.1.4：真正切换清晰度并重新取直链，保持当前播放位置。
        _autoPlay = !_autoPlay;
        var wasPlaying = _vm.IsPlaying;
        var position = Media.Position.TotalSeconds;
        _pendingSeekSeconds = position;
        await _vm.ChangeQualityAsync();
        if (wasPlaying) _vm.IsPlaying = true;
    }

    private void FullScreen_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null) return;
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void SendDanmaku_Click(object sender, RoutedEventArgs e)
    {
        var text = DanmakuInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text) || _vm.Selected is null) return;

        var entry = new DanmakuService.Entry
        {
            PostId = _vm.Selected.PostId,
            Text = text,
            Time = Media.Position.TotalSeconds,
            Author = string.IsNullOrWhiteSpace(_settings.Nickname) ? "我" : _settings.Nickname,
            CreatedAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
        };
        _danmaku.Add(entry);
        ShowDanmakuText(entry.Text, entry.Author);
        DanmakuInput.Clear();
    }

    private void ShowDanmakuText(string text, string author)
    {
        if (DanmakuHost is null || string.IsNullOrWhiteSpace(text)) return;

        var block = new TextBlock
        {
            Text = author.Length > 0 ? $"{author}：{text}" : text,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                ShadowDepth = 1,
                BlurRadius = 3,
                Opacity = 0.85,
            },
        };
        DanmakuHost.Children.Add(block);
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var top = 12 + Random.Shared.Next(0, 220);
        Canvas.SetTop(block, top);
        Canvas.SetLeft(block, DanmakuHost.ActualWidth > 0 ? DanmakuHost.ActualWidth : 900);

        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = DanmakuHost.ActualWidth > 0 ? DanmakuHost.ActualWidth : 900,
            To = -block.DesiredSize.Width - 20,
            Duration = TimeSpan.FromSeconds(8),
        };
        animation.Completed += (_, _) => DanmakuHost.Children.Remove(block);
        block.BeginAnimation(Canvas.LeftProperty, animation);
    }
}
