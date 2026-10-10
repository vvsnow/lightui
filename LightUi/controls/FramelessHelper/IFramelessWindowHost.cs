using System.Windows;

namespace LightUi.Controls.Frameless;

/// <summary>
/// 窗口过程与宿主窗口之间的接口：窗口过程要从宿主问的、以及要告知宿主的东西。
/// 由 <c>FramelessWindowAdapter</c>（包住 WPF 的 <see cref="Window"/>）实现。
///
/// 需要它是因为窗口过程看不懂 WPF 视觉树：「这点落在哪个按钮上」「这点上有没有可交互元素」
/// 只能反过来问宿主。
/// </summary>
internal interface IFramelessWindowHost
{
    double DpiScale { get; }

    bool IsMinimizable { get; }

    bool IsMaximizable { get; }

    bool IsClosable { get; }

    bool CanResize { get; }

    /// <summary>自定义 caption 高度（DIP）。0 = 按系统度量算。</summary>
    double CaptionHeight { get; }

    /// <summary>
    /// 给 DWM 的 frame insets，本工程全 0（不扩展玻璃）。
    /// **全 0 不等于不调用** —— 这条调用本身把窗口接进 DWM frame 通道，少了它 WM_NCHITTEST 一次都不会来。
    /// </summary>
    NativeMethods.MARGINS GetDwmFrameInsetsInPixels();

    /// <summary>
    /// 这点（**屏幕物理像素**）落在哪个标题栏按钮上，没落上返回 <see cref="CaptionButton.None"/>。
    /// 实现按模板里三个 Button 的实测尺寸判（见 <c>FramelessWindowAdapter</c>）。
    /// </summary>
    CaptionButton HitTestCaptionButtonAt(Point screenPointPx);

    /// <summary>
    /// 这个**客户区坐标**（DIP）点上有没有"标了 <c>WindowChrome.IsHitTestVisibleInChrome</c>"的元素。
    /// 为真返回 HTCLIENT 交给 WPF，否则 caption 条带按 HTCAPTION 当拖动区 ——
    /// 不放行的话，放进标题栏的控件会点不动，一点就变成拖窗口。
    /// </summary>
    bool IsInteractiveCaptionElementAt(Point clientPointDip);

    void SetHoveredCaptionButton(CaptionButton button);

    void SetPressedCaptionButton(CaptionButton button);

    /// <summary>
    /// 点击标题栏按钮时要执行的动作（最小化 / 最大化 / 还原 / 关闭）。
    /// 由我们自己执行、不交给系统：那块在系统看来是客户区，DefWindowProc 不会替我们做。
    /// </summary>
    void PerformCaptionButtonAction(CaptionButton button);

}
