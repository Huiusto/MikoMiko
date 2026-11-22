using MikoMiko.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace MikoMiko.Views;

/// <summary>软件内帖子阅读器（含内联视频与评论）。</summary>
public partial class PostReaderView : UserControl
{
    private readonly PostReaderViewModel _vm;
    private bool _localApplied;

    public PostReaderView(PostReaderViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

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

    /// <summary>开发期截图用：滚到评论区。</summary>
    public void ScrollToComments()
    {
        BodyScroll.ScrollToEnd();
    }
}
