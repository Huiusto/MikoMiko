using MikoMiko.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace MikoMiko.Controls;

/// <summary>只读富文本：保留帖子正文的字体样式与链接，同时支持鼠标选词和 Ctrl+C 复制。</summary>
public sealed class SelectableRichText : System.Windows.Controls.RichTextBox
{
    public static readonly DependencyProperty RunsProperty = DependencyProperty.Register(
        nameof(Runs), typeof(IEnumerable<PostRun>), typeof(SelectableRichText),
        new PropertyMetadata(null, OnRunsChanged));

    public IEnumerable<PostRun>? Runs
    {
        get => (IEnumerable<PostRun>?)GetValue(RunsProperty);
        set => SetValue(RunsProperty, value);
    }

    public SelectableRichText()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsDocumentEnabled = true;
        IsUndoEnabled = false;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        Background = System.Windows.Media.Brushes.Transparent;
        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        Cursor = System.Windows.Input.Cursors.IBeam;
        Focusable = true;
        IsTabStop = false;
        Document.PagePadding = new Thickness(0);
    }

    private static void OnRunsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SelectableRichText)d).Rebuild();

    private void Rebuild()
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            TextIndent = 0,
            LineHeight = 24,
        };
        paragraph.Inlines.Clear();
        if (Runs is not null)
        {
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
                    var hyperlink = new Hyperlink(run)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xA4, 0xDD)),
                        TextDecorations = null,
                        Cursor = System.Windows.Input.Cursors.Hand,
                    };
                    var link = postRun.Link;
                    hyperlink.Click += (_, _) => LinkTextBlock.LinkRequested?.Invoke(link);
                    paragraph.Inlines.Add(hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(run);
                }
            }
        }

        Document.Blocks.Clear();
        Document.Blocks.Add(paragraph);
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
