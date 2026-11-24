using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MikoMiko.Controls;

/// <summary>只读文本框：外观等同普通正文，但支持鼠标拖选文字并用 Ctrl+C 复制。</summary>
public sealed class SelectableText : System.Windows.Controls.TextBox
{
    public SelectableText()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        AcceptsReturn = true;
        TextWrapping = System.Windows.TextWrapping.Wrap;
        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        Background = System.Windows.Media.Brushes.Transparent;
        VerticalContentAlignment = VerticalAlignment.Top;
        HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
        Cursor = System.Windows.Input.Cursors.IBeam;
        Focusable = true;
        IsTabStop = false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        base.OnMouseLeftButtonDown(e);
    }
}
