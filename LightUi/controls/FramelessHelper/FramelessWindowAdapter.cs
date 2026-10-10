using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace LightUi.Controls.Frameless;

/// <summary>
/// 把 WPF <see cref="Window"/> 接到这套窗口过程上（实现 <see cref="IFramelessWindowHost"/>）：
/// 解析模板部件、按元素<b>实测尺寸</b>做按钮命中、逃生出口、执行按钮动作，
/// 并把悬停/按下状态与 caption 高度写回窗口上的附加属性。
///
/// 做成"包一层"而不是窗口基类：逻辑走附加属性，任何 <see cref="Window"/>（含派生窗口）都能用，
/// 窗口也不必在自己的 XAML 里写死部件。
/// </summary>
internal sealed class FramelessWindowAdapter : IFramelessWindowHost
{
    private readonly Window _window;

    // 模板部件（ControlTemplate 里的 PART_*）—— 解析成功前每次调用都再试一次，
    // 因为模板是首次布局时才套上的（窗口刚建出来那会儿还找不到）。
    private Button? _minimizeButton;
    private Button? _maximizeButton;
    private Button? _closeButton;
    private bool _partsResolved;

    public FramelessWindowAdapter(Window window)
    {
        _window = window;
    }

    public Window Window => _window;

    #region IFramelessWindowHost

    public double DpiScale => VisualTreeHelper.GetDpi(_window).DpiScaleX;

    public bool IsMinimizable =>
        _window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip or ResizeMode.CanMinimize;

    public bool IsMaximizable =>
        _window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

    public bool IsClosable => true;

    public bool CanResize =>
        _window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

    public double CaptionHeight => FramelessHelper.GetCaptionHeight(_window);

    /// <summary>
    /// 全 0：不扩展玻璃 —— 这条调用本身才是关键，见 <c>HwndMessageHandler.UpdateDwmFrame</c> 的说明。
    /// </summary>
    public NativeMethods.MARGINS GetDwmFrameInsetsInPixels() => default;

    /// <summary>
    /// 这点（屏幕物理像素）落在哪个标题栏按钮上。
    ///
    /// 判定用每个按钮元素的 <c>PointFromScreen</c> + <see cref="UIElement.RenderSize"/>：
    /// 元素自己的位置/尺寸/变换（右对齐的 StackPanel、模板里的偏移）全都由 WPF 算好，
    /// 我们不再自己算矩形 —— 这正是「按钮尺寸归样式」之后命中该有的样子。
    ///
    /// 模板还没套上 / 还没布局（<c>ActualWidth == 0</c>）/ 元素被样式收起来（<c>IsVisible == false</c>）
    /// 都返回 <see cref="CaptionButton.None"/>：那一点会落到 caption 条带，表现为可拖动 —— 启动瞬间
    /// 点在按钮位置上最多是"拖了一下窗口"，不会误触发动作。
    /// </summary>
    public CaptionButton HitTestCaptionButtonAt(Point screenPointPx)
    {
        ResolveParts();

        if (IsOver(_minimizeButton, screenPointPx))
        {
            return CaptionButton.Minimize;
        }

        if (IsOver(_maximizeButton, screenPointPx))
        {
            // 最大化与还原是**同一个元素**（样式按 WindowState 换字形与命令），所以这里必须按当前状态
            // 报身份：已最大化 = 还原键、否则 = 最大化键。
            //
            // ★ 踩过的坑：一度为了让样式少写两条触发器而统一报 Maximize，结果"最大化后点还原没反应"——
            //   动作是**按身份派发**的，已经最大化时再 MaximizeWindow 是个空操作。
            //   （两颗独立按钮的写法不会有这问题：露出来的那颗身份本来就是 Restore。）
            //
            // 查原生 IsZoomed，不读 WPF 的 WindowState：命中判定跑在 WPF 处理消息之前。
            return FrameView.IsMaximized(new WindowInteropHelper(_window).Handle)
                ? CaptionButton.Restore
                : CaptionButton.Maximize;
        }

        if (IsOver(_closeButton, screenPointPx))
        {
            return CaptionButton.Close;
        }

        return CaptionButton.None;
    }

    /// <summary>
    /// 逃生出口：这点（客户区 DIP）上有没有标了 <c>WindowChrome.IsHitTestVisibleInChrome</c> 的元素。
    ///
    /// 读 WPF 自带那个附加属性就行，**不需要给窗口挂 WindowChrome**。它是 Inherits 的，
    /// 标在容器上子元素全生效；下面再爬一遍祖先链，让"不继承"的实现也成立。
    ///
    /// 传入坐标是**客户区 DIP**，而 WPF 根视觉元素正好铺满客户区，可以直接用。
    /// </summary>
    public bool IsInteractiveCaptionElementAt(Point clientPointDip)
    {
        HitTestResult? hit = VisualTreeHelper.HitTest(_window, clientPointDip);

        for (DependencyObject? node = hit?.VisualHit; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is IInputElement input && WindowChrome.GetIsHitTestVisibleInChrome(input))
            {
                return true;
            }
        }

        return false;
    }

    public void SetHoveredCaptionButton(CaptionButton button) =>
        FramelessHelper.SetHoveredCaptionButtonValue(_window, button);

    public void SetPressedCaptionButton(CaptionButton button) =>
        FramelessHelper.SetPressedCaptionButtonValue(_window, button);

    /// <summary>
    /// 执行按钮动作。走 <see cref="SystemCommands"/>：它内部是 <c>PostMessage(WM_SYSCOMMAND)</c>，
    /// 异步执行，不会在钩子里重入消息循环。
    /// </summary>
    public void PerformCaptionButtonAction(CaptionButton button)
    {
        switch (button)
        {
            case CaptionButton.Minimize:
                SystemCommands.MinimizeWindow(_window);
                break;

            case CaptionButton.Maximize:
                SystemCommands.MaximizeWindow(_window);
                break;

            case CaptionButton.Restore:
                SystemCommands.RestoreWindow(_window);
                break;

            case CaptionButton.Close:
                SystemCommands.CloseWindow(_window);
                break;
        }
    }

    #endregion

    #region 部件解析与命中

    private void ResolveParts()
    {
        if (_partsResolved)
        {
            return;
        }

        // 三颗按钮住在样式模板里，所以必须走 Template.FindName（带 templatedParent），
        // 不能走 Window.FindName —— 后者只在窗口自己的 XAML 名字域里找。
        _window.ApplyTemplate();
        ControlTemplate? template = _window.Template;

        _minimizeButton = template?.FindName("PART_MinimizeButton", _window) as Button;
        _maximizeButton = template?.FindName("PART_MaximizeButton", _window) as Button;
        _closeButton = template?.FindName("PART_CloseButton", _window) as Button;

        // 只有三颗按钮都找到才算解析完；否则下次再试（模板是首次布局时才套上的）
        _partsResolved = _minimizeButton is not null
                         && _maximizeButton is not null
                         && _closeButton is not null;
    }

    private static bool IsOver(Button? button, Point screenPointPx)
    {
        if (button is null
            || !button.IsVisible
            || button.ActualWidth <= 0
            || button.ActualHeight <= 0)
        {
            return false;
        }

        Point local = button.PointFromScreen(screenPointPx);
        return new Rect(button.RenderSize).Contains(local);
    }

    #endregion
}
