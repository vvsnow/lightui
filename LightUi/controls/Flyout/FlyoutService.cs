using LightUi.Controls.Primitives;

namespace LightUi.Controls;

/// <summary>
/// 给任意 <see cref="ButtonBase"/> 挂 Flyout 的附着属性：点击按钮即弹出。
///
/// 用法：
/// <code>
/// &lt;Button ui:FlyoutService.Flyout&gt;
///     &lt;ui:MenuFlyout&gt;…&lt;/ui:MenuFlyout&gt;
/// &lt;/Button&gt;
/// </code>
/// Placement / VerticalOffset / HorizontalOffset 在 Flyout 上设。
/// </summary>
public static class FlyoutService
{
    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.RegisterAttached(
            "Flyout",
            typeof(FlyoutBase),
            typeof(FlyoutService),
            new PropertyMetadata(null, OnFlyoutChanged));

    public static FlyoutBase? GetFlyout(DependencyObject d) => (FlyoutBase?)d.GetValue(FlyoutProperty);
    public static void SetFlyout(DependencyObject d, FlyoutBase? value) => d.SetValue(FlyoutProperty, value);

    // 缓存 click handler，便于切换 Flyout 时正确反注册
    static readonly DependencyProperty ClickHandlerProperty =
        DependencyProperty.RegisterAttached(
            "ClickHandler", typeof(RoutedEventHandler), typeof(FlyoutService));

    static void OnFlyoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button)
            return;

        // 换 Flyout 时先摘掉旧 handler，再挂新的
        var old = (RoutedEventHandler?)button.GetValue(ClickHandlerProperty);
        if (old != null)
            button.Click -= old;

        if (e.NewValue is FlyoutBase flyout)
        {
            RoutedEventHandler handler = (_, _) => flyout.ShowAt(button);
            button.SetValue(ClickHandlerProperty, handler);
            button.Click += handler;
        }
    }
}
