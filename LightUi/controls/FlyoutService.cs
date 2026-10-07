using System.Windows.Controls.Primitives;

using LightUi.Controls.Primitives;

namespace LightUi.Controls;

/// <summary>
/// 给任意 <see cref="ButtonBase"/> 附加 Flyout 的支持（附着属性）。
/// 用法：<Button ui:FlyoutService.Flyout><ui:MenuFlyout>...</ui:MenuFlyout></ui:FlyoutService.Flyout></Button>
/// 点击按钮自动弹出 Flyout，Placement / VerticalOffset / HorizontalOffset 在 Flyout 上设。
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
        if (d is not ButtonBase button) return;

        // 解除旧的 handler
        var old = (RoutedEventHandler?)button.GetValue(ClickHandlerProperty);
        if (old != null) button.Click -= old;

        // 安装新的 handler：点击时弹出 Flyout
        if (e.NewValue is FlyoutBase flyout)
        {
            RoutedEventHandler handler = (_, _) => flyout.ShowAt(button);
            button.SetValue(ClickHandlerProperty, handler);
            button.Click += handler;
        }
    }
}
