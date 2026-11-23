using MikoMiko.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MikoMiko.Controls;

/// <summary>把一组 PostRun 渲染成 TextBlock，保留字号、颜色、粗斜体、删除线与链接。</summary>
public sealed class RichTextBlock : TextBlock
{
    public static readonly DependencyProperty RunsProperty = DependencyProperty.Register(
        nameof(Runs), typeof(IEnumerable<PostRun>), typeof(RichTextBlock),
        new PropertyMetadata(null, OnRunsChanged));

    public IEnumerable<PostRun>? Runs
    {
        get => (IEnumerable<PostRun>?)GetValue(RunsProperty);
        set => SetValue(RunsProperty, value);
    }

    private static void OnRunsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((RichTextBlock)d).Rebuild();

    private void Rebuild()
    {
        Inlines.Clear();
        if (Runs is null) return;

        foreach (var postRun in Runs)
        {
            if (postRun.Text.Length == 0) continue;
            var run = new Run(postRun.Text);
            if (postRun.FontSize > 0) run.FontSize = postRun.FontSize;
            if (postRun.Bold) run.FontWeight = FontWeights.SemiBold;
            if (postRun.Italic) run.FontStyle = FontStyles.Italic;

            var decorations = new TextDecorationCollection();
            if (postRun.Underline) decorations.Add(System.Windows.TextDecorations.Underline[0]);
            if (postRun.Strike) decorations.Add(System.Windows.TextDecorations.Strikethrough[0]);
            if (decorations.Count > 0) run.TextDecorations = decorations;

            var color = ParseColor(postRun.Color);
            if (color is not null) run.Foreground = new SolidColorBrush(color.Value);

            if (!string.IsNullOrWhiteSpace(postRun.Link))
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(run);
                hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xA4, 0xDD));
                hyperlink.TextDecorations = null;
                hyperlink.Cursor = System.Windows.Input.Cursors.Hand;
                hyperlink.Click += (_, _) => LinkTextBlock.LinkRequested?.Invoke(postRun.Link);
                Inlines.Add(hyperlink);
            }
            else
            {
                Inlines.Add(run);
            }
        }
    }

    private static Color? ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var digits = new string(text.Where(ch => char.IsDigit(ch) || ch == ',').ToArray());
            var parts = digits.Split(',');
            if (parts.Length >= 3 && byte.TryParse(parts[0], out var r)
                                  && byte.TryParse(parts[1], out var g)
                                  && byte.TryParse(parts[2], out var b))
                return Color.FromRgb(r, g, b);
            return null;
        }

        try
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(text);
        }
        catch
        {
            return null;
        }
    }
}
