using LightUi.Controls.Primitives;

using System.Collections;
using System.Windows.Markup;

namespace LightUi.Controls;

/// <summary>
/// 弹出菜单：条目是 MenuItem 风格，分隔线用 Separator。
///
/// 用法：
/// <code>
/// &lt;Button ui:FlyoutService.Flyout&gt;
///     &lt;ui:MenuFlyout&gt;
///         &lt;MenuItem Header="打开" /&gt;
///         &lt;Separator /&gt;
///         &lt;MenuItem Header="退出" /&gt;
///     &lt;/ui:MenuFlyout&gt;
/// &lt;/Button&gt;
/// </code>
/// 条目走 <see cref="Items"/>（XAML 直写）或 <see cref="ItemsSource"/>（数据绑定）。
/// </summary>
[ContentProperty(nameof(Items))]
public class MenuFlyout : FlyoutBase
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(MenuFlyout),
            new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>XAML 里直接嵌入的子条目（MenuItem / Separator）。</summary>
    public List<object> Items { get; } = new();

    protected override FrameworkElement CreatePresenter()
    {
        // 用 MenuFlyoutPresenter（继承 ContextMenu）让 WPF 原生 MenuBase 接管
        //「点条目自动关 + LightDismiss + 键盘导航」；这里只赋 ItemsSource 与 Style。
        var presenter = new MenuFlyoutPresenter
        {
            ItemsSource = ItemsSource ?? (IEnumerable)Items,
            StaysOpen = false,
            Focusable = true,
        };
        presenter.Style = (Style)Application.Current.TryFindResource("DefaultMenuFlyoutPresenterStyle")
                          ?? new Style(typeof(MenuFlyoutPresenter));
        return presenter;
    }
}