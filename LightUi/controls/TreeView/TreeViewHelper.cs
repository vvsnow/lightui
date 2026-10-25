namespace LightUi.Controls;

/// <summary>
/// TreeView 的附加属性。
///
/// 都在 TreeView 上写一次即可，值会继承到树里的每个 TreeViewItem：
/// <code>
/// &lt;TreeView ui:TreeViewHelper.ItemHeight="30" ui:TreeViewHelper.Indent="16" /&gt;
/// </code>
/// <see cref="ItemHeightProperty"/>：每行高度；<see cref="IndentProperty"/>：每级内容缩进；
/// <see cref="ToggleExpandedOnEnterProperty"/>：回车切换展开（默认开）。
/// 行本身始终是全宽，缩进只影响内容。
/// </summary>
public static class TreeViewHelper
{
    /// <summary>
    /// 每行的高度（DIP）。默认 28；展开箭头的高度跟着它走。
    /// </summary>
    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.RegisterAttached(
            "ItemHeight",
            typeof(double),
            typeof(TreeViewHelper),
            new FrameworkPropertyMetadata(28.0, FrameworkPropertyMetadataOptions.Inherits),
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

    /// <summary>
    /// 按回车切换当前行的展开状态。写在 TreeView 上；隐式 TreeView 样式默认打开，
    /// 不想用就在自己的 TreeView 上写 False。叶子（没有子项）不响应，也不吞掉按键。
    /// </summary>
    public static readonly DependencyProperty ToggleExpandedOnEnterProperty =
        DependencyProperty.RegisterAttached(
            "ToggleExpandedOnEnter",
            typeof(bool),
            typeof(TreeViewHelper),
            new PropertyMetadata(false, OnToggleExpandedOnEnterChanged));

    public static bool GetToggleExpandedOnEnter(DependencyObject element) =>
        (bool)element.GetValue(ToggleExpandedOnEnterProperty);

    public static void SetToggleExpandedOnEnter(DependencyObject element, bool value) =>
        element.SetValue(ToggleExpandedOnEnterProperty, value);

    private static void OnToggleExpandedOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeView treeView)
            return;

        treeView.KeyDown -= OnTreeKeyDown;

        if (e.NewValue is true)
            treeView.KeyDown += OnTreeKeyDown;
    }

    private static void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        // 键盘事件从焦点元素冒泡上来；焦点在某个 TreeViewItem 上时 OriginalSource 就是它
        if (e.Key != Key.Enter || e.OriginalSource is not TreeViewItem item || !item.HasItems)
            return;

        item.IsExpanded = !item.IsExpanded;
        e.Handled = true;
    }
}
