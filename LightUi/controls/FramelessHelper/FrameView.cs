using System.Windows;

namespace LightUi.Controls.Frameless;

/// <summary>
/// 窗口框架：边框厚度、客户区内缩、caption 高度、非客户区命中判定。
///
/// 逐条对应 Electron <c>shell/browser/ui/views/win_frame_view.cc</c>（以及它自己注明抄自的
/// Chrome <c>glass_browser_frame_view.cc</c> / <c>browser_desktop_window_tree_host_win.cc</c>）。
/// 每个方法上方都标了对应的源码位置。
///
/// <para>单位：布局与命中判定用 DIP（和 views 一致）；给 WM_NCCALCSIZE 的客户区内缩用物理像素。</para>
/// </summary>
internal sealed class FrameView
{
    /// <summary>Electron <c>FramelessView::kResizeInsideBoundsSize</c>（frameless_view.h:33）：顶部那条内部缩放带的宽度（DIP）。</summary>
    private const int ResizeInsideBoundsSize = 5;

    /// <summary>Electron <c>kResizeAreaCornerSize</c>（frameless_view.cc:19）。</summary>
    private const int ResizeAreaCornerSize = 16;

    /// <summary>
    /// 宿主窗口（对应参考里的 <c>window_</c>）。frame view 看不到 WPF 视觉树，
    /// "这点落在哪个按钮上""这点上有没有可交互元素"只能问宿主 —— 对应
    /// <c>window_-&gt;NonClientHitTest(point)</c> 那一步。
    /// </summary>
    private readonly IFramelessWindowHost _window;

    public FrameView(IFramelessWindowHost window)
    {
        _window = window;
    }

    #region 窗口状态

    public double DpiScale { get; set; } = 1.0;

    /// <summary>
    /// Electron <c>WinFrameView::IsMaximized()</c> 原文就一行：
    /// <code>return frame()->IsMaximized();</code>
    /// 那里的 <c>Widget::IsMaximized()</c> 也就是**问操作系统**（<c>IsZoomed(hwnd)</c>）。
    ///
    /// ★ 这里必须查原生状态，不能用 WPF 的 <c>Window.StateChanged</c>/<c>WindowState</c> 去同步一个字段：
    /// 窗口过程比 WPF 自己的消息处理先跑，等我们处理 WM_SIZE 时 WPF 还没更新 WindowState，
    /// 于是"最大化后按钮不变成还原"这种 bug 就来了。查原生状态则任何时候都是对的。
    /// </summary>
    public static bool IsMaximized(IntPtr hwnd) => NativeMethods.IsZoomed(hwnd);

    /// <summary>Electron: <c>frame()->IsFullscreen()</c>。本工程不支持全屏，恒 false。</summary>
    public static bool IsFullscreen(IntPtr hwnd) => false;

    /// <summary>自定义 caption 高度（DIP）。0 = 按系统度量算。**与系统按钮高度无关** —— 按钮尺寸归样式。</summary>
    public double CaptionHeight { get; set; }

    /// <summary>Electron: <c>frame()->widget_delegate()->CanResize()</c></summary>
    public bool CanResize { get; set; } = true;

    #endregion

    #region 度量（DIP），对应 win_frame_view.cc

    /// <summary>
    /// Electron <c>WinFrameView::FrameBorderThickness()</c>：
    /// <code>if (frame()->IsFullscreen() || IsMaximized()) return 0;
    /// return GetSystemMetricsInDIP(SM_CXSIZEFRAME);</code>
    /// 注意只看 <c>SM_CXSIZEFRAME</c>，<b>不含</b> <c>SM_CXPADDEDBORDER</c>。
    /// </summary>
    public int FrameBorderThickness(IntPtr hwnd)
    {
        if (IsFullscreen(hwnd) || IsMaximized(hwnd))
        {
            return 0;
        }

        return GetSystemMetricsDip(hwnd, NativeMethods.SM_CXSIZEFRAME);
    }

    /// <summary>
    /// Electron <c>WinFrameView::FrameTopBorderThicknessPx(bool restored)</c>：
    /// <code>const bool needs_no_border =
    ///     (window()->IsWindowControlsOverlayEnabled() &amp;&amp; frame()->IsMaximized()) || frame()->IsFullscreen();
    /// if (needs_no_border &amp;&amp; !restored) return 0;
    /// return ui::GetFrameThicknessFromWindow(HWNDForView(this), MONITOR_DEFAULTTONEAREST);</code>
    /// 本工程就是 WCO 模式，所以最大化时（且不要求 restored）返回 0，否则取窗口的边框厚度。
    /// </summary>
    public int FrameTopBorderThicknessPx(IntPtr hwnd, bool restored)
    {
        bool needsNoBorder = IsMaximized(hwnd) || IsFullscreen(hwnd);
        if (needsNoBorder && !restored)
        {
            return 0;
        }

        // ui::GetFrameThicknessFromWindow() 的等价物：SM_CXSIZEFRAME + SM_CXPADDEDBORDER
        // （实测这个值就是原生窗口那圈不可见边框，本机 125% 下 9px = 7.2 DIP）
        return GetFrameThicknessFromWindowPx(hwnd);
    }

    /// <summary>Electron <c>WinFrameView::FrameTopBorderThickness()</c>：<c>floor(px / scale)</c>。</summary>
    public int FrameTopBorderThickness(IntPtr hwnd, bool restored) =>
        (int)Math.Floor(FrameTopBorderThicknessPx(hwnd, restored) / DpiScale);

    /// <summary>Electron <c>WinFrameView::TitlebarMaximizedVisualHeight()</c> → <c>GetSystemMetricsInDIP(SM_CYCAPTION)</c>。</summary>
    public int TitlebarMaximizedVisualHeight(IntPtr hwnd) => GetSystemMetricsDip(hwnd, NativeMethods.SM_CYCAPTION);

    /// <summary>
    /// caption 条带高度（DIP）—— 只决定「多高算标题栏 / 拖动区」。
    ///
    /// 与移植前相比有<b>两处不同，这就是解耦</b>：
    /// <list type="bullet">
    /// <item>与系统按钮高度<b>无关</b>：按钮尺寸由样式给，这里不再参与摆位、也不再往下传任何高度；</item>
    /// <item><see cref="CaptionHeight"/> 显式给了正数就<b>原样采用</b>，不再做 Electron 那句
    /// <c>−WindowTopY</c> 修正，也没有「必须比 SM_CYCAPTION 高」的限制。</item>
    /// </list>
    ///
    /// 默认（<see cref="CaptionHeight"/> = 0）仍按 Electron <c>WinFrameView::TitlebarHeight(int)</c> 的公式算：
    /// <code>SM_CYCAPTION + FrameTopBorderThickness(false) - WindowTopY()</code>（本机 125% 下 23 + 7 − 1 = 29 DIP）。
    /// </summary>
    public int CaptionStripHeight(IntPtr hwnd)
    {
        if (IsFullscreen(hwnd) && !IsMaximized(hwnd))
        {
            return 0;
        }

        if (CaptionHeight > 0)
        {
            return (int)Math.Round(CaptionHeight);
        }

        return TitlebarMaximizedVisualHeight(hwnd)
               + FrameTopBorderThickness(hwnd, false)
               - WindowTopY(hwnd);
    }

    /// <summary>
    /// Electron <c>WinFrameView::WindowTopY()</c>：
    /// <code>if (IsMaximized()) return FrameTopBorderThickness(false);
    /// return 1;</code>
    /// 那个 1 是给 Windows 在窗口顶边画的那条视觉边框留的位置。
    /// </summary>
    public int WindowTopY(IntPtr hwnd) => IsMaximized(hwnd) ? FrameTopBorderThickness(hwnd, false) : 1;

    /// <summary>
    /// Electron <c>WinFrameView::RestoredFrameBorderInsets()</c>：
    /// <code>const int thickness = FrameTopBorderThickness(true);
    /// return gfx::Insets::TLBR(0, thickness, thickness, thickness);</code>
    /// ＝ <b>顶部 0、左右下 thickness</b>（源码注释："resize insets go on sides but not top"）。
    /// 这正是"无边框但保住不可见边框"的那一步：顶部归零所以没有原生标题栏，
    /// 左右下留厚度所以不可见边框还在，系统的 Snap 热区就和自绘按钮对得上。
    /// 返回值是 WPF 的 Thickness(左, 上, 右, 下)。
    /// </summary>
    public Thickness RestoredFrameBorderInsets(IntPtr hwnd)
    {
        int thickness = FrameTopBorderThickness(hwnd, restored: true);
        return new Thickness(thickness, 0, thickness, thickness);
    }

    /// <summary>
    /// 给 WM_NCCALCSIZE 用的客户区内缩（物理像素）。
    ///
    /// 非最大化：就是 <see cref="RestoredFrameBorderInsets"/>（顶 0，左右下厚度）——
    /// 对应 Chrome <c>GetClientAreaInsets</c> 的注释："For ordinary windows, the top inset must be zero,
    /// since if there is any nonclient area, Windows draws a native titlebar outside the client region"。
    ///
    /// 最大化：四边都要厚度 —— 同一处的注释："Windows outdents the window rect from the screen's client rect
    /// by |frame_thickness| on each edge, meaning |insets| must contain |frame_thickness| on all sides
    /// (including the top) to avoid the client area extending onto adjacent monitors."
    /// </summary>
    public Thickness GetClientAreaInsetsPx(IntPtr hwnd)
    {
        double thickness = FrameTopBorderThicknessPx(hwnd, restored: true);
        double top = IsMaximized(hwnd) || IsFullscreen(hwnd) ? thickness : 0;
        return new Thickness(thickness, top, thickness, thickness);
    }

    #endregion

    // 这里原先有一个「布局」区（LayoutCaptionButtons）：自己算按钮矩形、按客户区宽度贴右摆位、
    // 再把同一个高度塞给每个按钮当高度（就是那个「caption 高度 = 按钮高度」的耦合）。
    // 现在**整块没有了** —— 按钮是样式模板里的元素，位置与尺寸归样式；本类只管几何与命中。

    #region 非客户区命中判定（对应 WinFrameView::NonClientHitTest）

    /// <summary>
    /// 分支顺序：**先标题栏按钮 → 再排除系统按钮区 → 再缩放边 → 最后基类**（第 ① 步的实现换成「问元素」，顺序不变）。
    /// 返回 <see cref="NativeMethods.HTNotHandled"/> 表示不接管。
    ///
    /// 注意源码里缩放边只处理**顶部**：<c>GetHTComponentForFrame(point,
    /// gfx::Insets::TLBR(top_border_thickness, 0, 0, 0), top_border_thickness,
    /// kResizeCornerWidth - FrameBorderThickness(), canResize)</c> —— 左右下之所以不管，
    /// 是因为那边的 resize 目标本来就落在客户区之外的真实非客户区里，由系统自己回
    /// HTLEFT/HTBOTTOM/HTBOTTOMLEFT…（见 <c>ResizingBorderHitTest</c> 的注释：
    /// "With insets, side/bottom resize targets sit outside the frame and are handled by WM_NCHITTEST"）。
    /// </summary>
    public int NonClientHitTest(IntPtr hwnd, Point screenPointPx, Point windowPointDip, Size windowSizeDip)
    {
        // ① 标题栏按钮。**必须最先判**：右上角那块同时落在「系统按钮区」与「顶部缩放带」里，
        //    让按钮先答，否则贴右的按钮会被当成缩放边框。
        //    按钮是样式模板里的元素，命中按它们的**实测尺寸**算 —— 这里不再自己算矩形。
        CaptionButton hitButton = _window.HitTestCaptionButtonAt(screenPointPx);
        switch (hitButton)
        {
            case CaptionButton.Minimize:
                return NativeMethods.HTMINBUTTON;

            case CaptionButton.Maximize:
            case CaptionButton.Restore:
                // 恢复键与最大化键在系统看来是同一个槽位
                return NativeMethods.HTMAXBUTTON;

            case CaptionButton.Close:
                return NativeMethods.HTCLOSE;
        }

        // ② 客户区之外（左/右/下那圈不可见边框）**不归我们判**，把意见交回给系统。
        //
        // 对应 Electron <c>WinFrameView::ResizingBorderHitTest</c> 的注释：
        // "With insets, side/bottom resize targets sit outside the frame and are handled by
        //  WM_NCHITTEST" —— 那边的"WM_NCHITTEST"指的是**系统**（DefWindowProc）按窗口原生边框回
        //  HTLEFT/HTBOTTOM/HTBOTTOMRIGHT…
        //
        // ★ 踩过的坑：对着整个窗口都回话（那三边回 HTCLIENT/HTCAPTION）会把系统的回答权抢掉，
        // 表现就是"只有顶部能拖边框缩放，其它三边拖不动"。
        if (!IsInsideClientArea(hwnd, windowPointDip, windowSizeDip))
        {
            return NativeMethods.HTNotHandled;
        }

        // ③ 系统按钮区上不显示缩放光标 —— "On Windows 8+, the caption buttons are almost butted up to
        // the top right corner of the window. This code ensures the mouse isn't set to a size cursor
        // while hovering over the caption buttons"。顶部再内缩 1px（物理像素，转换前内缩），
        // 因为按钮正上方有一条 1px 的缝会让缩放边框"露出来"。
        if (TryGetSystemCaptionButtonBoundsDip(hwnd, out Rect systemButtons)
            && systemButtons.Contains(windowPointDip))
        {
            return NativeMethods.HTNotHandled;
        }

        // ④ 顶部那条内部缩放带。左右下已经在 ② 交出去了，**只有顶部需要我们自己判** ——
        // 对应 WinFrameView::ResizingBorderHitTest 里那句
        // "The exception is the top edge for frameless windows, which still need an inner resize band
        //  since there is no non-client titlebar to provide one."
        // 宽度用 kResizeInsideBoundsSize(5)，角用 kResizeAreaCornerSize(16)。
        int component = GetHtComponentForFrame(
            windowPointDip,
            windowSizeDip,
            ResizeInsideBoundsSize,
            ResizeAreaCornerSize,
            CanResize);
        if (component != NativeMethods.HTNotHandled)
        {
            return component;
        }

        // ⑤ 基类 FramelessView::NonClientHitTest 的等价物。
        //    clientPoint 就是客户区坐标（客户区原点即 WPF 根的坐标原点），所以这里可以直接用它判高度。
        Point clientPoint = ToClientPoint(hwnd, windowPointDip);
        if (clientPoint.Y < CaptionStripHeight(hwnd))
        {
            // caption 条带：这点上有"标了逃生出口"的 WPF 元素就交回 WPF（HTCLIENT），
            // 否则当拖动区（HTCAPTION）—— 对应参考里 NonClientHitTest 开头那一步
            // window_->NonClientHitTest(point)：先问内容区，内容说是它的就返回 HTCLIENT。
            // 不这么做的话，放进标题栏的控件会"点不动"，一点就变成拖窗口。
            return _window.IsInteractiveCaptionElementAt(clientPoint)
                ? NativeMethods.HTCLIENT
                : NativeMethods.HTCAPTION;
        }

        return NativeMethods.HTCLIENT;
    }

    /// <summary>点是否落在客户区之内（客户区 = 窗口矩形按 insets 内缩；非最大化时顶部内缩为 0）。</summary>
    private bool IsInsideClientArea(IntPtr hwnd, Point windowPointDip, Size windowSizeDip)
    {
        Thickness insets = GetClientAreaInsetsPx(hwnd);
        double left = insets.Left / DpiScale;
        double top = insets.Top / DpiScale;
        double right = insets.Right / DpiScale;
        double bottom = insets.Bottom / DpiScale;

        return windowPointDip.X >= left
               && windowPointDip.X <= windowSizeDip.Width - right
               && windowPointDip.Y >= top
               && windowPointDip.Y <= windowSizeDip.Height - bottom;
    }

    /// <summary>Chromium <c>GetHTComponentForFrame()</c> 的等价物。本工程左右下 insets 为 0，所以只有顶部。</summary>
    private static int GetHtComponentForFrame(
        Point point,
        Size windowSizeDip,
        double topBorderThickness,
        double cornerSize,
        bool canResize)
    {
        if (!canResize)
        {
            return NativeMethods.HTNotHandled;
        }

        if (point.Y > topBorderThickness)
        {
            return NativeMethods.HTNotHandled;
        }

        if (point.X <= cornerSize)
        {
            return NativeMethods.HTTOPLEFT;
        }

        if (point.X >= windowSizeDip.Width - cornerSize)
        {
            return NativeMethods.HTTOPRIGHT;
        }

        return NativeMethods.HTTOP;
    }

    /// <summary>
    /// Electron <c>WinFrameView::NonClientHitTest</c> 里读 <c>DWMWA_CAPTION_BUTTON_BOUNDS</c> 那一段，
    /// 结果是 DIP、窗口坐标系。拿不到就不排除。
    /// </summary>
    private bool TryGetSystemCaptionButtonBoundsDip(IntPtr hwnd, out Rect bounds)
    {
        bounds = default;

        if (NativeMethods.DwmGetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_CAPTION_BUTTON_BOUNDS,
                out NativeMethods.RECT rect,
                System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) != 0)
        {
            return false;
        }

        // button_bounds_px.Inset(gfx::Insets::TLBR(1, 0, 0, 0));
        rect.Top += 1;

        double scale = DpiScale;
        bounds = new Rect(
            rect.Left / scale,
            rect.Top / scale,
            (rect.Right - rect.Left) / scale,
            (rect.Bottom - rect.Top) / scale);

        return true;
    }

    /// <summary>窗口坐标 → 客户区坐标：减掉客户区相对窗口矩形的那圈内缩（顶部内缩为 0）。</summary>
    public Point ToClientPoint(IntPtr hwnd, Point windowPointDip)
    {
        Thickness insets = GetClientAreaInsetsPx(hwnd);
        return new Point(
            windowPointDip.X - (insets.Left / DpiScale),
            windowPointDip.Y - (insets.Top / DpiScale));
    }

    #endregion

    #region 度量取值

    private int GetSystemMetricsDip(IntPtr hwnd, int index)
    {
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        if (dpi == 0)
        {
            dpi = 96;
        }

        return (int)Math.Round(NativeMethods.GetSystemMetricsForDpi(index, dpi) / DpiScale);
    }

    /// <summary><c>ui::GetFrameThicknessFromWindow()</c> 的等价物：<c>SM_CXSIZEFRAME + SM_CXPADDEDBORDER</c>（物理像素）。</summary>
    public static int GetFrameThicknessFromWindowPx(IntPtr hwnd)
    {
        uint dpi = NativeMethods.GetDpiForWindow(hwnd);
        if (dpi == 0)
        {
            dpi = 96;
        }

        int thickness = NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXSIZEFRAME, dpi)
                      + NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXPADDEDBORDER, dpi);

        if (thickness <= 0)
        {
            thickness = (int)Math.Round(
                (NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSIZEFRAME)
                 + NativeMethods.GetSystemMetrics(NativeMethods.SM_CXPADDEDBORDER)) * dpi / 96.0);
        }

        return thickness;
    }

    #endregion
}
