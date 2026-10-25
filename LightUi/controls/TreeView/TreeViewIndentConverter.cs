using System.Globalization;
using System.Windows.Data;

namespace LightUi.Controls;

/// <summary>
/// 按层级计算 TreeViewItem 行内容的左边距：<c>TreeViewHelper.Indent × 层级</c>。
///
/// 行本身不缩进（ItemsHost 不设 Margin），hover / 选中的底色从控件最左边铺满整行；
/// 只有内容（箭头 + 文本）跟着层级右移 —— VS Code 的树就是这个效果。
/// 用法见 styles/TreeView.xaml。
/// </summary>
public sealed class TreeViewIndentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DependencyObject item)
            return new Thickness(0);

        var depth = 0;

        ItemsControl? parent = ItemsControl.ItemsControlFromItemContainer(item);
        while (parent is TreeViewItem)
        {
            depth++;
            parent = ItemsControl.ItemsControlFromItemContainer(parent);
        }

        return new Thickness(depth * TreeViewHelper.GetIndent(item), 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
