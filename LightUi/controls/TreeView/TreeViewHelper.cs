namespace LightUi.Controls;

/// <summary>
/// TreeView 的附加属性。
///
/// 都在 TreeView 上写一次即可，值会继承到树里的每个 TreeViewItem：
/// <code>
/// &lt;TreeView ui:TreeViewHelper.ItemHeight="30" ui:TreeViewHelper.Indent="16" /&gt;
/// </code>
/// <see cref="ItemHeightProperty"/>：每行高度；<see cref="IndentProperty"/>：每级内容缩进。
/// 行本身始终是全宽，缩进只影响内容。
/// </summary>
public static class TreeViewHelper
{
    /// <summary>
    /// 每行的高度（DIP）。默认 24；展开箭头的高度跟着它走。
    /// </summary>
    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.RegisterAttached(
            "ItemHeight",
            typeof(double),
            typeof(TreeViewHelper),
            new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.Inherits),
            static value => value is double height && height >= 0);

    /// <summary>
    /// 每级内容的缩进量（DIP）。默认 8；调大后子级内容离左边更远，行宽与 hover / 选中高亮不受影响。
    /// </summary>
    public static readonly DependencyProperty IndentProperty =
        DependencyProperty.RegisterAttached(
            "Indent",
            typeof(double),
            typeof(TreeViewHelper),
            new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.Inherits),
            static value => value is double indent && indent >= 0);

    public static double GetIndent(DependencyObject element) => (double)element.GetValue(IndentProperty);

    public static void SetIndent(DependencyObject element, double value) => element.SetValue(IndentProperty, value);

    public static double GetItemHeight(DependencyObject element) => (double)element.GetValue(ItemHeightProperty);

    public static void SetItemHeight(DependencyObject element, double value) => element.SetValue(ItemHeightProperty, value);
}
