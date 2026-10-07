using LightUi.Controls.Primitives;

using System.Collections;
using System.Windows.Markup;

namespace LightUi.Controls;

/// <summary>
/// 弹出菜单：内部是 ItemsControl，每个 Item 渲染为一个 MenuItem 风格条目。
/// 既支持 XAML 里直接嵌入 &lt;MenuItem /&gt; / &lt;Separator /&gt; 子元素（走 <see cref="Items"/>），
/// 也支持数据驱动绑定（走 <see cref="ItemsSource"/>）。
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
        // Presenter 改为 MenuFlyoutPresenter（继承自 ContextMenu），
        // 由 WPF 原生 MenuBase 接管"点 MenuItem 自动关弹层 + LightDismiss + 键盘导航"。
        // 这里只是赋值 ItemsSource 与 Style，具体 ShowAt/Hide 由 FlyoutBase 驱动 _popup.IsOpen。
        var presenter = new MenuFlyoutPresenter
        {
            ItemsSource = ItemsSource ?? (IEnumerable)Items,
            StaysOpen = false,
            Focusable = true,
        };
        presenter.Style = (Style)Application.Current.TryFindResource("LightUiMenuFlyoutPresenterStyle")
                          ?? new Style(typeof(MenuFlyoutPresenter));
        return presenter;
    }
}