using MikoMiko.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MikoMiko.Views;

/// <summary>软件内帖子阅读器（含内联视频与评论）。</summary>
public partial class PostReaderView : UserControl
{
    private readonly PostReaderViewModel _vm;
    private readonly DispatcherTimer _inlineTimer;
    private bool _localApplied;
    private bool _updatingInlineSlider;
    private bool _draggingInline;
    private bool _lightboxPanning;
    private Point _lightboxLastPoint;

    public PostReaderView(PostReaderViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        _inlineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _inlineTimer.Tick += (_, _) => UpdateInlineProgress();

        Media.MediaOpened += (_, _) => { };
        Media.MediaEnded += (_, _) => ShowCover();
        Media.MediaFailed += (_, _) =>
        {
            ShowCover();
            _vm.BufferText = "视频播放失败，可点击重试";
        };

        _vm.PropertyChanged += OnViewModelChanged;
        Loaded += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_vm.VideoLocalPath)) ApplyLocal();
        };
        Unloaded += (_, _) =>
        {
            Media.Pause();
            Media.Source = null;
            _inlineTimer.Stop();
            InlineMedia.Pause();
            InlineMedia.Source = null;
        };
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PostReaderViewModel.VideoLocalPath))
        {
            _localApplied = false;
            if (!string.IsNullOrWhiteSpace(_vm.VideoLocalPath)) ApplyLocal();
        }
    }

    private void ApplyLocal()
    {
        if (_localApplied || string.IsNullOrWhiteSpace(_vm.VideoLocalPath)) return;
        _localApplied = true;
        Media.Source = new Uri(_vm.VideoLocalPath!, UriKind.Absolute);
        Media.Volume = 0.6;
    }

    private void VideoPlay_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.VideoLocalPath))
        {
            if (_vm.RetryVideoCommand.CanExecute(null)) _vm.RetryVideoCommand.Execute(null);
            return;
        }

        ApplyLocal();
        Media.Play();
        VideoCover.Visibility = Visibility.Collapsed;
        VideoPauseButton.Visibility = Visibility.Visible;
    }

    private void VideoPause_Click(object sender, RoutedEventArgs e)
    {
        Media.Pause();
        ShowCover();
    }

    private void ShowCover()
    {
        Media.Pause();
        VideoCover.Visibility = Visibility.Visible;
        VideoPauseButton.Visibility = Visibility.Collapsed;
    }

    /// <summary>正文内嵌视频：在软件内播放，带可拖动进度条。</summary>
    private void InlineVideo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string url } || string.IsNullOrWhiteSpace(url)) return;
        try
        {
            VideoOverlay.Visibility = Visibility.Visible;
            InlineMedia.Source = new Uri(url, UriKind.Absolute);
            InlineMedia.Volume = 0.7;
            InlineMedia.Play();
            _inlineTimer.Start();
        }
        catch
        {
            _vm.BufferText = "视频打开失败";
        }
    }

    private void InlineProgress_DragStart(object sender, MouseButtonEventArgs e) => _draggingInline = true;

    private void InlineProgress_DragEnd(object sender, MouseButtonEventArgs e)
    {
        _draggingInline = false;
        TrySeekInline();
    }

    private void InlineProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingInlineSlider) return;
        if (_draggingInline || Mouse.LeftButton == MouseButtonState.Released) TrySeekInline();
    }

    private void TrySeekInline()
    {
        try
        {
            if (!InlineMedia.NaturalDuration.HasTimeSpan) return;
            InlineMedia.Position = TimeSpan.FromSeconds(Math.Clamp(InlineProgress.Value, 0,
                InlineMedia.NaturalDuration.TimeSpan.TotalSeconds));
            InlineTime.Text = $"{Format(InlineMedia.Position)} / {Format(InlineMedia.NaturalDuration.TimeSpan)}";
        }
        catch
        {
            // 拖动时视频尚未打开
        }
    }

    private void UpdateInlineProgress()
    {
        try
        {
            if (!InlineMedia.NaturalDuration.HasTimeSpan) return;
            var duration = InlineMedia.NaturalDuration.TimeSpan.TotalSeconds;
            if (!_draggingInline)
            {
                _updatingInlineSlider = true;
                InlineProgress.Maximum = Math.Max(1, duration);
                InlineProgress.Value = Math.Clamp(InlineMedia.Position.TotalSeconds, 0, InlineProgress.Maximum);
                _updatingInlineSlider = false;
            }

            InlineTime.Text = $"{Format(InlineMedia.Position)} / {Format(TimeSpan.FromSeconds(duration))}";
        }
        catch
        {
            // 忽略
        }
    }

    private void CloseInlineVideo_Click(object sender, RoutedEventArgs e)
    {
        _inlineTimer.Stop();
        InlineMedia.Pause();
        InlineMedia.Source = null;
        VideoOverlay.Visibility = Visibility.Collapsed;
    }

    private static string Format(TimeSpan value)
        => $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";

    /// <summary>点击正文图片，用应用内灯箱放大查看。</summary>
    private void InlineImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: string url } || string.IsNullOrWhiteSpace(url)) return;
        LightboxImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(url, UriKind.Absolute));
        LightboxScale.ScaleX = LightboxScale.ScaleY = 1;
        LightboxTranslate.X = LightboxTranslate.Y = 0;
        ImageLightbox.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    /// <summary>灯箱左键拖动：按住图片平移，不依赖滚动条。</summary>
    private void Lightbox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ImageLightbox.Visibility != Visibility.Visible) return;
        _lightboxPanning = true;
        _lightboxLastPoint = e.GetPosition(LightboxScroll);
        LightboxScroll.CaptureMouse();
        LightboxScroll.Cursor = System.Windows.Input.Cursors.SizeAll;
        e.Handled = true;
    }

    private void Lightbox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_lightboxPanning || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(LightboxScroll);
        LightboxTranslate.X += point.X - _lightboxLastPoint.X;
        LightboxTranslate.Y += point.Y - _lightboxLastPoint.Y;
        _lightboxLastPoint = point;
        e.Handled = true;
    }

    private void Lightbox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_lightboxPanning) return;
        _lightboxPanning = false;
        LightboxScroll.ReleaseMouseCapture();
        LightboxScroll.Cursor = System.Windows.Input.Cursors.Hand;
        e.Handled = true;
    }

    /// <summary>评论内回复框出现时自动聚焦。</summary>
    private void InlineReplyBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox box && box.IsVisible)
            Dispatcher.BeginInvoke(new Action(() => box.Focus()), DispatcherPriority.Input);
    }

    /// <summary>灯箱滚轮缩放（0.5×–6×），放大后可拖动滚动查看。</summary>
    private void Lightbox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        var scale = Math.Clamp(LightboxScale.ScaleX * factor, 0.5, 6);
        LightboxScale.ScaleX = scale;
        LightboxScale.ScaleY = scale;
        e.Handled = true;
    }

    private void CloseImageLightbox_Click(object sender, MouseButtonEventArgs e)
    {
        ImageLightbox.Visibility = Visibility.Collapsed;
        LightboxImage.Source = null;
    }

    private void FloatingRefresh_Click(object sender, RoutedEventArgs e)
    {
        BodyScroll.ScrollToTop();
        _vm.ReloadCommand.Execute(null);
    }

    private void CloseImageLightbox_Click(object sender, RoutedEventArgs e)
    {
        ImageLightbox.Visibility = Visibility.Collapsed;
        LightboxImage.Source = null;
    }

    private void BackToTop_Click(object sender, RoutedEventArgs e) => BodyScroll.ScrollToTop();

    private void GoComments_Click(object sender, RoutedEventArgs e) => CommentSection.BringIntoView();

    /// <summary>开发期截图用：滚到评论区。</summary>
    public void ScrollToComments()
    {
        CommentSection.BringIntoView();
    }

    /// <summary>开发期截图用：把评论输入框滚到顶部，检查多行输入是否完整可见。</summary>
    public void ScrollToCommentBox()
    {
        var offset = CommentComposer.TransformToAncestor(BodyScroll).Transform(new Point(0, 0)).Y
                     + BodyScroll.VerticalOffset;
        BodyScroll.ScrollToVerticalOffset(Math.Max(0, offset - 24));
    }
}
