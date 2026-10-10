using System.Windows;

namespace LightUi.Controls.Frameless;

/// <summary>
/// 窗口过程需要从宿主窗口问到、以及要告知宿主的东西。对应 Chromium 的
/// <c>HWNDMessageHandlerDelegate</c>，由 <c>FramelessWindowAdapter</c>（包住 WPF 的 <see cref="Window"/>）实现。
///
/// 与移植前那份接口的两处差别：
/// <list type="bullet">
/// <item>去掉了诊断成员（<c>RecordHitTest</c>）与按钮宿主 Canvas —— 按钮不再是自绘的。</item>
/// <item>新增 <see cref="HitTestCaptionButtonAt"/>：标题栏按钮改成样式模板里的 WPF 元素，
/// 「这点落在哪个按钮上」只能问宿主（按元素**实测尺寸**算）。</item>
/// </list>
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

    /// <summary>对应 Electron 的 <c>GetDwmFrameInsetsInPixels</c>；本工程返回全 0（同 Electron）。</summary>
    NativeMethods.MARGINS GetDwmFrameInsetsInPixels();

    /// <summary>
    /// 这点（**屏幕物理像素**）落在哪个标题栏按钮上，没落上返回 <see cref="CaptionButton.None"/>。
    /// 实现按模板里三个 Button 的实测尺寸判（见 <c>FramelessWindowAdapter</c>）。
    /// </summary>
    CaptionButton HitTestCaptionButtonAt(Point screenPointPx);

    /// <summary>
    /// 这个**客户区坐标**（DIP）点上是不是有"标了逃生出口"的元素（对应参考里
    /// <c>window_-&gt;NonClientHitTest(point)</c> 那一步：先问内容区）。
    /// 为真则返回 HTCLIENT（交给 WPF），否则 caption 条带按 HTCAPTION 当拖动区。
    /// </summary>
    bool IsInteractiveCaptionElementAt(Point clientPointDip);

    void SetHoveredCaptionButton(CaptionButton button);

    void SetPressedCaptionButton(CaptionButton button);

    /// <summary>
    /// 点击标题栏按钮时要执行的动作。对应 Electron 里按钮回调直接调的
    /// <c>NativeWindow::Minimize() / Maximize() / Restore() / Close()</c>。
    /// </summary>
    void PerformCaptionButtonAction(CaptionButton button);

}
