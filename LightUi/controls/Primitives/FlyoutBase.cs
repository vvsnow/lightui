namespace LightUi.Controls.Primitives;

/// <summary>
/// Flyout / MenuFlyout 的抽象基类：管 Placement / Offset / IsOpen 与 ShowAt / Hide。
/// 一般不直接用（用 Flyout 或 MenuFlyout）；要自定义弹层时继承它并覆写 <see cref="CreatePresenter"/>。
///
/// 两种承载策略：Flyout 的 presenter 作 <see cref="Popup.Child"/>，由外层 Popup 管显示与 LightDismiss；
/// MenuFlyout 的 presenter 自己是 ContextMenu（自带 Popup、不能有父级），所以绕开外层 Popup 直接操作它。
/// </summary>
public abstract class FlyoutBase : DependencyObject
{
    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(
            nameof(Placement),
            typeof(FlyoutPlacementMode),
            typeof(FlyoutBase),
            new PropertyMetadata(FlyoutPlacementMode.Bottom));

    /// <summary>
    /// 关闭弹层的命令，给**弹层内容里**的按钮用：
    /// <code>
    /// &lt;Button Content="关闭" Command="{x:Static ui:FlyoutBase.CloseCommand}" /&gt;
    /// </code>
    /// 内容里拿不到 Flyout 实例（presenter 与 Popup 都是代码 new 的、不在逻辑树上，DataContext 也不继承），
    /// 所以命令绑定挂在 presenter 上，按钮的路由会一路冒到它 —— 不用 code-behind、也不用 x:Name。
    /// </summary>
    public static readonly RoutedCommand CloseCommand = new("CloseFlyout", typeof(FlyoutBase));

    public FlyoutPlacementMode Placement
    {
        get => (FlyoutPlacementMode)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    /// 额外的纵向偏移（DIP，正 = 往下），与 Placement 无关。
    /// 默认 0 —— 与宿主之间那条缝由 ShowAt 按 Placement 补，见 PlacementGap。
    /// </summary>
    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(
            nameof(VerticalOffset),
            typeof(double),
            typeof(FlyoutBase),
            new PropertyMetadata(0.0));

    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>
    /// 额外的横向偏移（DIP，正 = 往右），与 Placement 无关。
    /// 默认 0 —— 与宿主之间那条缝由 ShowAt 按 Placement 补，见 PlacementGap。
    /// </summary>
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

        if (!(bool)e.NewValue)
        {
            self.DetachEscHook();
        }

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
    private Window? _escHookWindow;

    /// <summary>弹层与宿主之间沿放置方向的固定缝（DIP）。想调间距就改这一个数。</summary>
    private const double PlacementGap = 2;

    /// <summary>在指定 anchor 控件旁弹出 Flyout。</summary>
    public void ShowAt(FrameworkElement anchor)
    {
        // WPF 的 VerticalOffset / HorizontalOffset 只认屏幕方向（正 = 下 / 右），
        // 所以「沿放置方向的缝」要在这里换算：Top / Left 取负，否则不是留缝而是压住宿主。
        (double offsetV, double offsetH) = GetPopupOffsets();

        // 一次性：创建 presenter 与（如果是 Flyout 的）外层 Popup
        if (_presenter is null)
        {
            _presenter = CreatePresenter();

            // 「关闭」命令：绑定挂在 presenter 上（Flyout 与 MenuFlyout 共用这条）
            // CanExecute 必须显式给 true —— 不给的话命令默认 CanExecute=false，按钮会渲染成灰的
            _presenter.CommandBindings.Add(new CommandBinding(
                CloseCommand,
                (_, _) => Hide(),
                (_, e) => e.CanExecute = true));

            if (_presenter is Controls.MenuFlyoutPresenter cm)
            {
                // MenuFlyout：ContextMenu 自带内部 Popup，绝对不能塞进 Popup.Child。
                // 监听 ContextMenu.Closed，把 IsOpen 同步回来。
                cm.Closed += OnMenuFlyoutClosed;
                cm.DismissedByAnchorClick += OnMenuFlyoutDismissedByAnchorClick;
            }
            else
            {
                _popup = new Popup
                {
                    Child = _presenter,
                    PlacementTarget = anchor,
                    Placement = (PlacementMode)MapPlacement(Placement),
                    VerticalOffset = offsetV,
                    HorizontalOffset = offsetH,
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
            cm2.VerticalOffset = offsetV;
            cm2.HorizontalOffset = offsetH;
            IsOpen = true;
            cm2.IsOpen = true;
        }
        else if (_popup is { } p)
        {
            p.PlacementTarget = anchor;
            p.Placement = (PlacementMode)MapPlacement(Placement);
            p.VerticalOffset = offsetV;
            p.HorizontalOffset = offsetH;
            IsOpen = true;
            p.IsOpen = true;
        }

        AttachEscHook(anchor);
    }

    /// <summary>
    /// Esc 关闭。Esc 是发给**焦点元素**的，弹层自己收不到键盘事件（它的 Popup 只捕获鼠标），
    /// 所以挂在锚点所在的窗口上：打开时挂、关掉时摘。
    /// MenuFlyout 那条走 ContextMenu（自带 Esc 处理），这里挂上也无害。
    /// </summary>
    private void AttachEscHook(FrameworkElement anchor)
    {
        Window? window = Window.GetWindow(anchor);
        if (window is null || ReferenceEquals(window, _escHookWindow))
        {
            return;
        }

        DetachEscHook();
        _escHookWindow = window;
        window.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void DetachEscHook()
    {
        if (_escHookWindow is null)
        {
            return;
        }

        _escHookWindow.PreviewKeyDown -= OnPreviewKeyDown;
        _escHookWindow = null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Hide();
        e.Handled = true;   // 别让 Esc 继续往外冒（比如顺手把宿主对话框也关了）
    }

    public void Hide() => IsOpen = false;

    private void OnPopupMouseLeave(object sender, MouseEventArgs e) { /* 预留 API 兼容扩展点 */ }

    /// <summary>Popup 因 StaysOpen=false 被点外部关掉时把 IsOpen 同步回 false，
    /// 否则它会一直停在 true，依赖它的地方（如 DropDownButton 的弹层状态）会失真。</summary>
    private void OnPopupClosed(object? sender, EventArgs e) => IsOpen = false;

    /// <summary>MenuFlyout 的 ContextMenu 自动关闭时（点 MenuItem / LightDismiss）把 IsOpen 同步回 false。</summary>
    private void OnMenuFlyoutClosed(object? sender, RoutedEventArgs e) => IsOpen = false;

    /// <summary>
    /// 菜单是被"点在 anchor 上"的那一次点击关掉时，重新弹出。
    /// ContextMenu 的 LightDismiss 会吃掉这次点击（普通 Popup 会把点击交还给按钮，于是 Button.Click 又弹出一次），
    /// 不补这一步，点 anchor 的语义就从 Flyout 的"保持弹出"变成"关掉"。
    /// 用 Background 优先级：等这一下的 MouseUp 走完再弹，免得弹层正好垫在光标底下被 MouseUp 误触。
    /// </summary>
    private void OnMenuFlyoutDismissedByAnchorClick(object? sender, EventArgs e)
    {
        if (sender is not Controls.MenuFlyoutPresenter { PlacementTarget: FrameworkElement anchor } presenter)
            return;

        presenter.Dispatcher.BeginInvoke(
            new Action(() => ShowAt(anchor)),
            DispatcherPriority.Background);
    }

    /// <summary>子类返回承载 Flyout 内容的根 FrameworkElement（Flyout = 任意 Content；MenuFlyout = ContextMenu）。</summary>
    protected abstract FrameworkElement CreatePresenter();

    /// <summary>
    /// 合成本次弹出该用的 Popup 偏移 = 沿放置方向的固定缝 + 用户设的屏幕方向偏移。
    /// </summary>
    private (double Vertical, double Horizontal) GetPopupOffsets() => Placement switch
    {
        FlyoutPlacementMode.Top => (-PlacementGap + VerticalOffset, HorizontalOffset),
        FlyoutPlacementMode.Left => (VerticalOffset, -PlacementGap + HorizontalOffset),
        FlyoutPlacementMode.Right => (VerticalOffset, PlacementGap + HorizontalOffset),
        _ => (PlacementGap + VerticalOffset, HorizontalOffset),   // Bottom
    };

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