using System.Windows;
namespace MikoMiko.Controls;

/// <summary>按可用宽度自动计算列数的卡片面板（保证卡片最小宽度）。</summary>
public sealed class AdaptiveGridPanel : System.Windows.Controls.Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(AdaptiveGridPanel), new FrameworkPropertyMetadata(250.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(AdaptiveGridPanel), new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    private double[] _rowHeights = Array.Empty<double>();

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = InternalChildren.Count;
        if (count == 0) return new Size(0, 0);

        var width = availableSize.Width;
        if (double.IsInfinity(width) || width <= 0) width = MinItemWidth;
        var columns = Math.Max(1, (int)Math.Floor((width + Gap) / (MinItemWidth + Gap)));
        columns = Math.Min(columns, count);

        var itemWidth = Math.Max(1, (width - Gap * (columns - 1)) / columns);
        var rows = (int)Math.Ceiling(count / (double)columns);
        _rowHeights = new double[rows];

        var childConstraint = new Size(itemWidth, double.PositiveInfinity);
        double total = 0;

        for (var i = 0; i < count; i++)
        {
            var child = InternalChildren[i];
            child.Measure(childConstraint);
            var row = i / columns;
            _rowHeights[row] = Math.Max(_rowHeights[row], child.DesiredSize.Height);
        }

        for (var r = 0; r < rows; r++) total += _rowHeights[r];
        total += Gap * Math.Max(0, rows - 1);
        return new Size(width, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = InternalChildren.Count;
        if (count == 0) return finalSize;

        var width = finalSize.Width <= 0 ? MinItemWidth : finalSize.Width;
        var columns = Math.Max(1, (int)Math.Floor((width + Gap) / (MinItemWidth + Gap)));
        columns = Math.Min(columns, Math.Max(1, count));
        var itemWidth = Math.Max(1, (width - Gap * (columns - 1)) / columns);

        double y = 0;
        var rowHeight = _rowHeights.Length > 0 ? _rowHeights[0] : 0;

        for (var i = 0; i < count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            if (row < _rowHeights.Length) rowHeight = _rowHeights[row];
            var x = column * (itemWidth + Gap);
            InternalChildren[i].Arrange(new Rect(x, y, itemWidth, rowHeight));
            if (column == columns - 1 || i == count - 1) y += rowHeight + Gap;
        }

        return finalSize;
    }
}
