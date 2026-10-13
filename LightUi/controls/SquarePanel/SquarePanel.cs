namespace LightUi.Controls;

/// <summary>
/// 把子元素摆进一个正方形：边长取子元素自然尺寸的较大者，子元素在正方形里居中。
/// 圆形按钮靠它做到「不写尺寸时，圆正好包住内容」（一个图标、一行文字都行）。
///
/// 用法：
/// <code>
/// &lt;ui:SquarePanel&gt;
///     &lt;Border CornerRadius="9999"&gt;内容&lt;/Border&gt;
/// &lt;/ui:SquarePanel&gt;
/// </code>
/// 为什么要一个面板：XAML 里算不出 max(宽, 高)，这个「取较大者」只能由布局代码来做。
/// 尺寸被外部定死时（按钮上写了 Width / Height）取短边做边长 —— 宁可圆比盒子小、居中留白，
/// 也不要溢出到相邻控件上。
/// </summary>
public class SquarePanel : Decorator
{
    protected override Size MeasureOverride(Size constraint)
    {
        var child = Child;
        if (child is null)
        {
            return new Size();
        }

        // 放开量：要的是内容的自然尺寸。拿父级约束过的尺寸当边长，圆会忽大忽小
        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var desired = child.DesiredSize;
        var side = Math.Max(desired.Width, desired.Height);
        return new Size(side, side);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var child = Child;
        if (child is not null)
        {
            var side = Math.Min(arrangeSize.Width, arrangeSize.Height);
            var x = ( arrangeSize.Width - side ) / 2;
            var y = ( arrangeSize.Height - side ) / 2;
            child.Arrange(new Rect(x, y, side, side));
        }

        return arrangeSize;
    }
}
