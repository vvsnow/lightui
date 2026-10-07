namespace LightUi.Controls;

/// <summary>
/// 承载 <see cref="MenuFlyout"/> 内容的宿主。
/// 继承自 <see cref="ContextMenu"/>（即 <see cref="System.Windows.Controls.Primitives.MenuBase"/>），
/// 从而让 WPF 原生 MenuBase 接管：
///   1) 点 MenuItem 后自动关闭弹层
///   2) LightDismiss（点击 Popup 外部自动关闭，<see cref="StaysOpen"/>=false 时）
///   3) 键盘方向键导航 / 高亮态
/// 参考实现：ModernWpf.Controls.MenuFlyoutPresenter。
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
}