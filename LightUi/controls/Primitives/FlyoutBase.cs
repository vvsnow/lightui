namespace LightUi.Controls.Primitives;

/// <summary>
/// Flyout / MenuFlyout 的抽象基类；子类覆写 <see cref="CreatePresenter"/> 自定义呈现。
/// 两种承载策略：
///   - Flyout：presenter 作 <see cref="Popup.Child"/>，由外层 Popup 控制显示 / LightDismiss。
///   - MenuFlyout：presenter 是 ContextMenu（自带 Popup，不能有父级），
///     所以绕开外层 Popup，直接操作 ContextMenu.PlacementTarget / IsOpen。
/// </summary>
public abstract class FlyoutBase : DependencyObject
{
    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(
            nameof(Placement),
            typeof(FlyoutPlacementMode),
            typeof(FlyoutBase),
            new PropertyMetadata(FlyoutPlacementMode.Bottom));

    public FlyoutPlacementMode Placement
    {
        get => (FlyoutPlacementMode)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>沿放置方向的偏移（px）。默认 2，让弹层和 anchor 之间留一点呼吸。</summary>
    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(double),
            typeof(FlyoutBase),
            new PropertyMetadata(2.0));

    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>垂直于放置方向的偏移（px）。默认 0。</summary>
    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(HorizontalOffset),
            typeof(double),
            typeof(FlyoutBase),
            new PropertyMetadata(0.0));

    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(FlyoutBase),
            new PropertyMetadata(false, OnIsOpenChanged));

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FlyoutBase self)
            return;

        // MenuFlyout：presenter 是 ContextMenu（MenuBase），直接同步到 ContextMenu.IsOpen
        // （它自带 Popup + OnMenuItemClicked 自动关闭 + LightDismiss）
        if (self._presenter is Controls.MenuFlyoutPresenter cm)
        {
            cm.IsOpen = (bool)e.NewValue;
            return;
        }

        // Flyout：外层 Popup
        if (self._popup is { } p)
        {
            p.IsOpen = (bool)e.NewValue;
        }
    }

    private Popup? _popup;
    private FrameworkElement? _presenter;

    /// <summary>在指定 anchor 控件旁弹出 Flyout。</summary>
    public void ShowAt(FrameworkElement anchor)
    {
        // 一次性：创建 presenter 与（如果是 Flyout 的）外层 Popup
        if (_presenter is null)
        {
            _presenter = CreatePresenter();

            if (_presenter is Controls.MenuFlyoutPresenter cm)
            {
                // MenuFlyout：ContextMenu 自带内部 Popup，绝对不能塞进 Popup.Child。
                // 监听 ContextMenu.Closed，把 IsOpen 同步回来。
                cm.Closed += OnMenuFlyoutClosed;
            }
            else
            {
                _popup = new Popup
                {
                    Child = _presenter,
                    PlacementTarget = anchor,
                    Placement = (PlacementMode)MapPlacement(Placement),
                    VerticalOffset = VerticalOffset,
                    HorizontalOffset = HorizontalOffset,
                    StaysOpen = false,
                    AllowsTransparency = true,
                };
                _popup.AddHandler(Popup.MouseLeaveEvent, new MouseEventHandler(OnPopupMouseLeave));
                _popup.Closed += OnPopupClosed;
            }
        }

        // 每次 ShowAt：更新 Placement / Offset 后打开
        if (_presenter is Controls.MenuFlyoutPresenter cm2)
        {
            cm2.PlacementTarget = anchor;
            cm2.Placement = (PlacementMode)MapPlacement(Placement);
            cm2.VerticalOffset = VerticalOffset;
            cm2.HorizontalOffset = HorizontalOffset;
            IsOpen = true;
            cm2.IsOpen = true;
        }
        else if (_popup is { } p)
        {
            p.PlacementTarget = anchor;
            p.Placement = (PlacementMode)MapPlacement(Placement);
            p.VerticalOffset = VerticalOffset;
            p.HorizontalOffset = HorizontalOffset;
            IsOpen = true;
            p.IsOpen = true;
        }
    }

    public void Hide() => IsOpen = false;

    private void OnPopupMouseLeave(object sender, MouseEventArgs e) { /* 预留 API 兼容扩展点 */ }

    /// <summary>Popup 因 StaysOpen=false 被点外部关掉时把 IsOpen 同步回 false，
    /// 否则它会一直停在 true，依赖它的地方（如 DropDownButton 的弹层状态）会失真。</summary>
    private void OnPopupClosed(object? sender, EventArgs e) => IsOpen = false;

    /// <summary>MenuFlyout 的 ContextMenu 自动关闭时（点 MenuItem / LightDismiss）把 IsOpen 同步回 false。</summary>
    private void OnMenuFlyoutClosed(object? sender, RoutedEventArgs e) => IsOpen = false;

    /// <summary>子类返回承载 Flyout 内容的根 FrameworkElement（Flyout = 任意 Content；MenuFlyout = ContextMenu）。</summary>
    protected abstract FrameworkElement CreatePresenter();

    private static PlacementMode MapPlacement(FlyoutPlacementMode m) => m switch
    {
        FlyoutPlacementMode.Top => PlacementMode.Top,
        FlyoutPlacementMode.Left => PlacementMode.Left,
        FlyoutPlacementMode.Right => PlacementMode.Right,
        _ => PlacementMode.Bottom,
    };
}

public enum FlyoutPlacementMode
{
    Top,
    Bottom,
    Left,
    Right,
}