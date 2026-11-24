using MikoMiko.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MikoMiko.Controls;

/// <summary><see cref="Image"/> 的异步网络图加载附加属性。</summary>
public static class AsyncImage
{
    public static readonly DependencyProperty UrlProperty = DependencyProperty.RegisterAttached(
        "Url", typeof(string), typeof(AsyncImage), new PropertyMetadata(null, OnUrlChanged));

    /// <summary>加载失败或地址为空时使用的兜底图（pack 路径）。</summary>
    public static readonly DependencyProperty FallbackProperty = DependencyProperty.RegisterAttached(
        "Fallback", typeof(string), typeof(AsyncImage), new PropertyMetadata(null));

    /// <summary>图片请求需要携带的 Referer（部分追番站图床限制外链）。</summary>
    public static readonly DependencyProperty RefererProperty = DependencyProperty.RegisterAttached(
        "Referer", typeof(string), typeof(AsyncImage), new PropertyMetadata(null, OnRefererChanged));

    public static void SetReferer(DependencyObject element, string? value) => element.SetValue(RefererProperty, value);

    public static string? GetReferer(DependencyObject element) => (string?)element.GetValue(RefererProperty);

    public static void SetFallback(DependencyObject element, string? value) => element.SetValue(FallbackProperty, value);

    public static string? GetFallback(DependencyObject element) => (string?)element.GetValue(FallbackProperty);

    public static void SetUrl(DependencyObject element, string? value) => element.SetValue(UrlProperty, value);

    public static string? GetUrl(DependencyObject element) => (string?)element.GetValue(UrlProperty);

    private static void OnRefererChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;
        var url = GetUrl(image);
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("pack:", StringComparison.OrdinalIgnoreCase)) return;
        // Referer 通常晚于 Url 绑定到达，拿到后按正确来源重新请求一次。
        OnUrlChanged(image, new DependencyPropertyChangedEventArgs(UrlProperty, null, url));
    }

    private static async void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;

        var url = e.NewValue as string;
        image.Source = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            ApplyFallback(image);
            return;
        }

        BitmapImage? bitmap;
        if (url.StartsWith("pack:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase))
        {
            bitmap = LoadPacked(url);
        }
        else
        {
            bitmap = await ImageLoader.LoadAsync(url, GetReferer(image));
        }

        if (bitmap is not null && string.Equals(GetUrl(image), url, StringComparison.Ordinal))
        {
            image.Source = bitmap;
            return;
        }

        if (string.Equals(GetUrl(image), url, StringComparison.Ordinal)) ApplyFallback(image);
    }

    private static void ApplyFallback(Image image)
    {
        var fallback = GetFallback(image);
        if (string.IsNullOrWhiteSpace(fallback)) return;

        var bitmap = LoadPacked(fallback!);
        if (bitmap is not null) image.Source = bitmap;
    }

    private static BitmapImage? LoadPacked(string url)
    {
        try
        {
            var uri = url.StartsWith("pack:", StringComparison.OrdinalIgnoreCase)
                ? new Uri(url, UriKind.Absolute)
                : new Uri("pack://application:,,,/" + url.TrimStart('/'), UriKind.Absolute);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
