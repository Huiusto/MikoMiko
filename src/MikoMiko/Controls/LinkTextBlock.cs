using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace MikoMiko.Controls;

/// <summary>把正文文本中的 http(s) 链接渲染为可点击的 Hyperlink，保留原有排版。</summary>
public sealed class LinkTextBlock : TextBlock
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s，。！？；、<>""）】]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly DependencyProperty SourceTextProperty = DependencyProperty.Register(
        nameof(SourceText), typeof(string), typeof(LinkTextBlock),
        new PropertyMetadata(string.Empty, OnSourceTextChanged));

    public static readonly DependencyProperty SourceUrlProperty = DependencyProperty.Register(
        nameof(SourceUrl), typeof(string), typeof(LinkTextBlock),
        new PropertyMetadata(string.Empty, OnSourceTextChanged));

    public string SourceText
    {
        get => (string)GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    /// <summary>整段文本对应的链接（Quill attributes.link）。</summary>
    public string SourceUrl
    {
        get => (string)GetValue(SourceUrlProperty);
        set => SetValue(SourceUrlProperty, value);
    }

    private static void OnSourceTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LinkTextBlock)d).Rebuild();

    private void Rebuild()
    {
        Inlines.Clear();
        var text = SourceText ?? string.Empty;
        var explicitUrl = SourceUrl ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(explicitUrl))
        {
            AddHyperlink(string.IsNullOrWhiteSpace(text) ? explicitUrl : text, explicitUrl);
            return;
        }

        var index = 0;
        foreach (Match match in UrlRegex.Matches(text))
        {
            if (match.Index > index) Inlines.Add(new Run(text[index..match.Index]));
            AddHyperlink(match.Value, match.Value);
            index = match.Index + match.Length;
        }

        if (index < text.Length) Inlines.Add(new Run(text[index..]));
    }

    private void AddHyperlink(string display, string url)
    {
        var link = new Hyperlink(new Run(display)) { NavigateUri = new Uri(url, UriKind.Absolute) };
        link.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Accent");
        link.TextDecorations = System.Windows.TextDecorations.Underline;
        link.RequestNavigate += (_, args) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri.AbsoluteUri)
                {
                    UseShellExecute = true,
                });
                args.Handled = true;
            }
            catch
            {
                // 浏览器打开失败时保留链接文本
            }
        };
        Inlines.Add(link);
    }
}
