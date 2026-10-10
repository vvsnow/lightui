namespace LightUi.Controls;

/// <summary>
/// 比原生 StackPanel 多一个 <see cref="Spacing"/> 的最小面板：只做堆叠 + 等距，不处理 Stretch / 对齐。
///
/// 用法：
/// <code>
/// &lt;ui:SimpleStackPanel Spacing="8"&gt;
///     &lt;Button Content="A" /&gt;
///     &lt;Button Content="B" /&gt;
/// &lt;/ui:SimpleStackPanel&gt;
/// </code>
/// 横向堆叠用 Orientation="Horizontal"；间距按可见性算，Collapsed 的元素不占位。
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
        var spacing = Spacing;
        var size = new Size();
        var count = Children.Count;
        var hasVisibleChild = false;

        // 堆叠轴要放开成无限再量子元素：否则会被调用方的有限约束压扁（拿不到自然尺寸），
        // 面板报的 DesiredSize 偏小、内容被切 —— 放进固定高的 Grid 行、或嵌套同类面板时就会踩到。
        var slot = availableSize;
        if (stack)
        {
            slot.Height = double.PositiveInfinity;
        }
        else
        {
            slot.Width = double.PositiveInfinity;
        }

        for (int i = 0; i < count; i++)
        {
            var child = Children[i];
            if (child is null)
            {
                continue;
            }

            // 间距按「可见性」算，不是按下标：Collapsed 的元素不该占位。
            var isVisible = child.Visibility != Visibility.Collapsed;
            if (isVisible)
            {
                hasVisibleChild = true;
            }

            child.Measure(slot);
            var ds = child.DesiredSize;

            if (stack)
            {
                size.Width = Math.Max(size.Width, ds.Width);
                size.Height += ( isVisible ? spacing : 0 ) + ds.Height;
            }
            else
            {
                size.Width += ( isVisible ? spacing : 0 ) + ds.Width;
                size.Height = Math.Max(size.Height, ds.Height);
            }
        }

        // 上面给每个可见元素都在自己前面加了一份间距，最后一个的多算了，减掉
        if (hasVisibleChild)
        {
            if (stack)
            {
                size.Height -= spacing;
            }
            else
            {
                size.Width -= spacing;
            }
        }

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var stack = Orientation == Orientation.Vertical;
        var spacing = Spacing;
        var rect = new Rect(finalSize);
        var previousChildSize = 0.0;
        var count = Children.Count;

        for (int i = 0; i < count; i++)
        {
            var child = Children[i];
            if (child is null)
            {
                continue;
            }

            var ds = child.DesiredSize;

            if (stack)
            {
                rect.Y += previousChildSize;
                previousChildSize = ds.Height;
                rect.Height = previousChildSize;
                // 交叉轴取 max：子元素比分配到的空间大时让它溢出，而不是硬压到 finalSize
                rect.Width = Math.Max(finalSize.Width, ds.Width);
            }
            else
            {
                rect.X += previousChildSize;
                previousChildSize = ds.Width;
                rect.Width = previousChildSize;
                rect.Height = Math.Max(finalSize.Height, ds.Height);
            }

            // 与测量保持一致：只有可见元素才推进间距
            if (child.Visibility != Visibility.Collapsed)
            {
                previousChildSize += spacing;
            }

            child.Arrange(rect);
        }

        return finalSize;
    }
}
