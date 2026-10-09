using System.Windows;

namespace LightUi.VsCodeFrameTest.Native;

/// <summary>
/// 窗口框架：边框厚度、客户区内缩、标题栏按钮摆位、非客户区命中判定。
///
/// 逐条对应 Electron <c>shell/browser/ui/views/win_frame_view.cc</c>（以及它自己注明抄自的
/// Chrome <c>glass_browser_frame_view.cc</c> / <c>browser_desktop_window_tree_host_win.cc</c>）。
/// 每个方法上方都标了对应的源码位置。
///
/// <para>单位：布局与命中判定用 DIP（和 views 一致）；给 WM_NCCALCSIZE 的客户区内缩用物理像素。</para>
/// </summary>
internal sealed class WinFrameView
{
    /// <summary>Electron <c>FramelessView::kResizeInsideBoundsSize</c>（frameless_view.h:33）：顶部那条内部缩放带的宽度（DIP）。</summary>
    private const int ResizeInsideBoundsSize = 5;

    /// <summary>Electron <c>kResizeAreaCornerSize</c>（frameless_view.cc:19）。</summary>
    private const int ResizeAreaCornerSize = 16;

    private readonly WinCaptionButtonContainer _container;

    /// <summary>
    /// 宿主窗口（对应参考里的 <c>window_</c>）。frame view 看不到 WPF 视觉树，
    /// "这点上有没有可交互元素"只能问宿主 —— 对应 <c>window_-&gt;NonClientHitTest(point)</c> 那一步。
    /// </summary>
    private readonly IHwndMessageHandlerDelegate _window;

    public WinFrameView(WinCaptionButtonContainer container, IHwndMessageHandlerDelegate window)
    {
        _container = container;
        _window = window;
    }

    public WinCaptionButtonContainer CaptionButtons => _container;

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

    /// <summary>Electron: <c>window()->titlebar_overlay_height()</c>（本工程留 0 = 用默认标题栏高度）</summary>
    public int TitlebarOverlayHeight { get; set; }

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
    /// Electron <c>WinFrameView::TitlebarHeight(int custom_height)</c>：
    /// <code>if (frame()->IsFullscreen() &amp;&amp; !IsMaximized()) return 0;
    /// int height = TitlebarMaximizedVisualHeight() + FrameTopBorderThickness(false) - WindowTopY();
    /// if (custom_height > TitlebarMaximizedVisualHeight()) height = custom_height - WindowTopY();
    /// return height;</code>
    /// </summary>
    public int TitlebarHeight(IntPtr hwnd)
    {
        if (IsFullscreen(hwnd) && !IsMaximized(hwnd))
        {
            return 0;
        }

        int height = TitlebarMaximizedVisualHeight(hwnd)
                     + FrameTopBorderThickness(hwnd, false)
                     - WindowTopY(hwnd);

        if (TitlebarOverlayHeight > TitlebarMaximizedVisualHeight(hwnd))
        {
            height = TitlebarOverlayHeight - WindowTopY(hwnd);
        }

        return height;
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

    #region 布局

    /// <summary>
    /// Electron <c>WinFrameView::LayoutCaptionButtons()</c>：
    /// <code>caption_button_container_->SetBounds(width() - preferred_size.width(), WindowTopY(),
    ///     variable_width, height);   // variable_width 非最大化时 = preferred - 1
    /// caption_button_container_->SetButtonSize(gfx::Size(0, height));</code>
    /// 那句 <c>- 1</c> 的注释："Without insets, the resize hit targets are inside the frame,
    /// and a 1px margin is needed to click and drag next to the button container."
    ///
    /// <c>width()</c> 这边取**客户区宽度**：客户区右缘就是可见边框内沿，按钮贴它才和系统槽位对齐。
    /// </summary>
    public void LayoutCaptionButtons(IntPtr hwnd, double clientWidthDip)
    {
        Size preferred = _container.CalculatePreferredSize();
        int height = TitlebarHeight(hwnd);

        bool maximized = IsMaximized(hwnd);
        double width = maximized ? preferred.Width : preferred.Width - 1;

        var bounds = new Rect(clientWidthDip - preferred.Width, WindowTopY(hwnd), width, height);
        _container.Layout(bounds);

        _container.SetButtonSize(new Size(0, height));
        _container.Layout(bounds);
    }

    #endregion

    #region 非客户区命中判定（对应 WinFrameView::NonClientHitTest）

    /// <summary>
    /// 分支顺序照源码：**先按钮容器 → 再排除系统按钮区 → 再缩放边 → 最后基类**。
    /// 返回 <see cref="NativeMethods.HTNotHandled"/> 表示不接管。
    ///
    /// 注意源码里缩放边只处理**顶部**：<c>GetHTComponentForFrame(point,
    /// gfx::Insets::TLBR(top_border_thickness, 0, 0, 0), top_border_thickness,
    /// kResizeCornerWidth - FrameBorderThickness(), canResize)</c> —— 左右下之所以不管，
    /// 是因为那边的 resize 目标本来就落在客户区之外的真实非客户区里，由系统自己回
    /// HTLEFT/HTBOTTOM/HTBOTTOMLEFT…（见 <c>ResizingBorderHitTest</c> 的注释：
    /// "With insets, side/bottom resize targets sit outside the frame and are handled by WM_NCHITTEST"）。
    /// </summary>
    public int NonClientHitTest(IntPtr hwnd, Point windowPointDip, Size windowSizeDip)
    {
        // ① 按钮容器
        Point containerPoint = ToContainerPoint(hwnd, windowPointDip);
        if (_container.HitTestPoint(containerPoint))
        {
            int result = _container.NonClientHitTest(containerPoint);
            if (result != NativeMethods.HTNotHandled)
            {
                return result;
            }
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
        //    containerPoint 就是客户区坐标（容器的坐标原点与客户区一致），所以这里可以直接用它判高度。
        if (containerPoint.Y < TitlebarHeight(hwnd))
        {
            // 标题栏条带：这点上有"标了逃生出口"的 WPF 元素就交回 WPF（HTCLIENT），
            // 否则当拖动区（HTCAPTION）—— 对应参考里 NonClientHitTest 开头那一步
            // window_->NonClientHitTest(point)：先问内容区，内容说是它的就返回 HTCLIENT。
            // 不这么做的话，放进标题栏的控件会"点不动"，一点就变成拖窗口。
            return _window.IsInteractiveCaptionElementAt(containerPoint)
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

    /// <summary>窗口坐标 → 容器坐标：减掉客户区相对窗口矩形的那圈内缩（顶部内缩为 0）。</summary>
    public Point ToContainerPoint(IntPtr hwnd, Point windowPointDip)
    {
        Thickness insets = GetClientAreaInsetsPx(hwnd);
        return new Point(
            windowPointDip.X - (insets.Left / DpiScale),
            windowPointDip.Y - (insets.Top / DpiScale));
    }

    /// <summary>点落在哪个按钮上（驱动悬停视觉）。传窗口坐标（DIP）。</summary>
    public CaptionButton HitTestCaptionButton(IntPtr hwnd, Point windowPointDip)
        => _container.HitTestButton(ToContainerPoint(hwnd, windowPointDip));

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
