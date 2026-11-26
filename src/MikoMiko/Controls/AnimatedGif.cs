using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MikoMiko.Controls;

/// <summary>
/// 播放包资源里的 GIF。WPF 原生 Image 只显示首帧，这里按 50ms 逐帧切换，
/// 用于启动页的 MikoMiko 加载动画。
/// </summary>
public sealed class AnimatedGif : Image
{
    public static readonly DependencyProperty GifPathProperty = DependencyProperty.Register(
        nameof(GifPath), typeof(string), typeof(AnimatedGif),
        new PropertyMetadata(null, OnGifPathChanged));

    private readonly DispatcherTimer _timer;
    private BitmapFrame[] _frames = Array.Empty<BitmapFrame>();
    private int _index;

    public AnimatedGif()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += (_, _) => NextFrame();
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    public string? GifPath
    {
        get => (string?)GetValue(GifPathProperty);
        set => SetValue(GifPathProperty, value);
    }

    private static void OnGifPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedGif gif) gif.LoadGif(e.NewValue as string);
    }

    private void LoadGif(string? path)
    {
        _timer.Stop();
        _frames = Array.Empty<BitmapFrame>();
        _index = 0;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var info = Application.GetResourceStream(new Uri(path, UriKind.RelativeOrAbsolute));
            if (info is null) return;
            using var stream = info.Stream;
            var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return;
            _frames = decoder.Frames.ToArray();
            Source = _frames[0];
            if (IsLoaded) Start();
        }
        catch
        {
            // 资源缺失时保持空白，不阻塞启动。
        }
    }

    private void Start()
    {
        if (_frames.Length <= 1) return;
        _timer.Start();
    }

    private void NextFrame()
    {
        if (_frames.Length == 0) return;
        _index = (_index + 1) % _frames.Length;
        Source = _frames[_index];
    }
}
