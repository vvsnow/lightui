namespace LightUi.Controls;

/// <summary>
/// 承载 <see cref="MenuFlyout"/> 内容的宿主。继承 <see cref="ContextMenu"/>
/// （即 <see cref="System.Windows.Controls.Primitives.MenuBase"/>），让 WPF 原生接管：
/// 点 MenuItem 自动关闭、LightDismiss（<see cref="StaysOpen"/>=false 时点外部关闭）、
/// 键盘方向键导航与高亮态。参考实现：ModernWpf.Controls.MenuFlyoutPresenter。
/// </summary>
public class MenuFlyoutPresenter : ContextMenu
{
    static MenuFlyoutPresenter()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(MenuFlyoutPresenter),
            new FrameworkPropertyMetadata(typeof(MenuFlyoutPresenter)));
    }

    public MenuFlyoutPresenter()
    {
        // ContextMenu 默认可见但不可聚焦；设为 true 以便接收键盘焦点、维持 LightDismiss 状态。
        Focusable = true;
    }

    /// <summary>
    /// 菜单被"落在 anchor 上的那一次点击"关掉时触发。ContextMenu 的 LightDismiss 会把这次点击吃掉
    /// （普通 Popup 不会），导致点 anchor 变成"关掉菜单"；外层收到后重新弹出，手感与 Flyout 一致。
    /// </summary>
    internal event EventHandler? DismissedByAnchorClick;

    private Popup? _parentPopup;

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        HookParentPopup();
    }

    protected override void OnOpened(RoutedEventArgs e)
    {
        base.OnOpened(e);
        HookParentPopup();
    }

    /// <summary>
    /// ContextMenu 的内部 Popup 在打开期间捕获鼠标，所以"点在外面"的那一下也能在这里看到。
    /// 菜单已经关着却仍收到鼠标事件 ⇒ 这一下正是关掉它的那一下；落点又在 anchor 上 ⇒ 请求重新弹出。
    /// </summary>
    private void OnParentPopupPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsOpen || PlacementTarget is not FrameworkElement anchor)
            return;

        var point = Mouse.GetPosition(anchor);
        if (point.X < 0 || point.Y < 0 || point.X > anchor.ActualWidth || point.Y > anchor.ActualHeight)
            return;

        DismissedByAnchorClick?.Invoke(this, EventArgs.Empty);
    }

    private void HookParentPopup()
    {
        if (_parentPopup is not null)
            return;

        _parentPopup = Parent as Popup;
        if (_parentPopup is null)
            return;

        _parentPopup.PreviewMouseLeftButtonDown += OnParentPopupPreviewMouseLeftButtonDown;
    }
}
