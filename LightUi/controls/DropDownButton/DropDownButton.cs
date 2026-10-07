using LightUi.Controls.Primitives;

namespace LightUi.Controls;

/// <summary>
/// 点击后弹出附带 Flyout 的按钮（替代原生 Button + 手写 Popup）。
/// 用法：&lt;ui:DropDownButton&gt;&lt;ui:DropDownButton.Flyout&gt;...&lt;/ui:DropDownButton.Flyout&gt;&lt;/ui:DropDownButton&gt;
/// </summary>
public class DropDownButton : Button
{
    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.Register(
            nameof(Flyout),
            typeof(FlyoutBase),
            typeof(DropDownButton),
            new PropertyMetadata(null));

    public FlyoutBase? Flyout
    {
        get => (FlyoutBase?)GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Flyout is null)
            return;

        Flyout.Placement = FlyoutPlacementMode.Bottom;
        Flyout.ShowAt(this);
    }
}