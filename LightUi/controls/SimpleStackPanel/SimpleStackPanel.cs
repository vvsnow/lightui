namespace LightUi.Controls;

/// <summary>
/// 简单 StackPanel：比原生 StackPanel 多一个 <see cref="Spacing"/>（相邻元素间距）。
/// 最小版——不处理 Stretch / 对齐，只做"垂直堆叠 + 等距"。
/// </summary>
public class SimpleStackPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty =
        StackPanel.OrientationProperty.AddOwner(
            typeof(SimpleStackPanel),
            new FrameworkPropertyMetadata(
                Orientation.Vertical,
                FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(
            nameof(Spacing),
            typeof(double),
            typeof(SimpleStackPanel),
            new FrameworkPropertyMetadata(
                    0.0,
                    FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var stack = Orientation == Orientation.Vertical;
        var size = new Size();
        var count = Children.Count;

        for (int i = 0; i < count; i++)
        {
            var child = Children[i];
            child.Measure(availableSize);
            var ds = child.DesiredSize;
            if (stack)
            {
                size.Width = Math.Max(size.Width, ds.Width);
                size.Height += ds.Height + ( i > 0 ? Spacing : 0 );
            }
            else
            {
                size.Width += ds.Width + ( i > 0 ? Spacing : 0 );
                size.Height = Math.Max(size.Height, ds.Height);
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var stack = Orientation == Orientation.Vertical;
        var offset = 0.0;
        var count = Children.Count;

        for (int i = 0; i < count; i++)
        {
            var child = Children[i];
            var ds = child.DesiredSize;
            if (stack)
            {
                child.Arrange(new Rect(0, offset, finalSize.Width, ds.Height));
                offset += ds.Height + Spacing;
            }
            else
            {
                child.Arrange(new Rect(offset, 0, ds.Width, finalSize.Height));
                offset += ds.Width + Spacing;
            }
        }
        return finalSize;
    }
}