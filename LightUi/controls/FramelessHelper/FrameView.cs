using System.Windows;

namespace LightUi.Controls.Frameless;

/// <summary>
/// 窗口框架：边框厚度、客户区内缩、caption 高度、非客户区命中判定。
/// 只算几何、只回答命中，不碰任何 WPF 元素。
///
/// <para>单位：布局与命中判定用 DIP；给 WM_NCCALCSIZE 的客户区内缩用物理像素。</para>
/// </summary>
internal sealed class FrameView
{
    /// <summary>顶部那条内部缩放带的宽度（DIP）：没有原生标题栏，顶部得自己给一条能拖的窄带。</summary>
    private const int ResizeInsideBoundsSize = 5;

    /// <summary>顶部缩放带两端"角"的宽度（DIP）：落在角上给 HTTOPLEFT/HTTOPRIGHT，中间才是 HTTOP。</summary>
    private const int ResizeAreaCornerSize = 16;

    /// <summary>
    /// 宿主窗口。本类看不到 WPF 视觉树，「这点落在哪个按钮上」「这点上有没有可交互元素」
    /// 只能问宿主。
    /// </summary>
    private readonly IFramelessWindowHost _window;

    public FrameView(IFramelessWindowHost window)
    {
        _window = window;
    }

    #region 窗口状态

    public double DpiScale { get; set; } = 1.0;

    /// <summary>
    /// 是否已最大化 —— **问操作系统**（<c>IsZoomed</c>），不读 WPF 的 <c>WindowState</c>。
    ///
    /// ★ 必须这样：窗口过程比 WPF 的消息处理先跑，处理 WM_SIZE 时 WPF 还没更新 WindowState，
    /// 用同步来的字段就会出现"最大化后按钮不变成还原"。问原生状态则任何时候都对。
    /// </summary>
    public static bool IsMaximized(IntPtr hwnd) => NativeMethods.IsZoomed(hwnd);

    /// <summary>本工程不支持全屏，恒 false（留这个入口是因为下面几处都要判它）。</summary>
    public static bool IsFullscreen(IntPtr hwnd) => false;

    /// <summary>自定义 caption 高度（DIP）。0 = 按系统度量算。**与系统按钮高度无关** —— 按钮尺寸归样式。</summary>
    public double CaptionHeight { get; set; }

    /// <summary>窗口能不能缩放。不能缩放时顶部那条缩放带也不该响应。</summary>
    public bool CanResize { get; set; } = true;

    #endregion

    #region 度量（DIP）

    /// <summary>
    /// 边框厚度（DIP）：全屏 / 最大化时是 0，否则取 <c>SM_CXSIZEFRAME</c>。
    /// 只看 <c>SM_CXSIZEFRAME</c>，<b>不含</b> <c>SM_CXPADDEDBORDER</c>。
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
    /// 顶部边框厚度（物理像素）：最大化 / 全屏且不要求"还原态尺寸"时返回 0，
    /// 否则取窗口那圈不可见边框的厚度（<c>SM_CXSIZEFRAME + SM_CXPADDEDBORDER</c>，本机 125% 下 9px）。
    /// </summary>
    public int FrameTopBorderThicknessPx(IntPtr hwnd, bool restored)
    {
        bool needsNoBorder = IsMaximized(hwnd) || IsFullscreen(hwnd);
        if (needsNoBorder && !restored)
        {
            return 0;
        }

        return GetFrameThicknessFromWindowPx(hwnd);
    }

    /// <summary>顶部边框厚度（DIP）= 物理像素 ÷ DPI，向下取整。</summary>
    public int FrameTopBorderThickness(IntPtr hwnd, bool restored) =>
        (int)Math.Floor(FrameTopBorderThicknessPx(hwnd, restored) / DpiScale);

    /// <summary>标题栏在最大化时的视觉高度（DIP）= 系统 caption 高度。</summary>
    public int TitlebarMaximizedVisualHeight(IntPtr hwnd) => GetSystemMetricsDip(hwnd, NativeMethods.SM_CYCAPTION);

    /// <summary>
    /// caption 条带高度（DIP）—— 只决定「多高算标题栏 / 拖动区」。
    ///
    /// 与系统按钮高度<b>无关</b>：按钮尺寸由样式给，这里不参与摆位、也不往下传高度。
    ///
    /// <see cref="CaptionHeight"/> 给了正数就<b>原样采用</b>（不做任何修正）；为 0 才回到系统度量算：
    /// <c>SM_CYCAPTION + 顶部边框厚度 − WindowTopY</c>（本机 125% 下 23 + 7 − 1 = 29 DIP）。
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
    /// 窗口顶边到客户区顶边的距离（DIP）：最大化时等于顶部边框厚度，否则是 1 ——
    /// Windows 会在窗口顶边画一条 1px 的视觉边框。
    /// </summary>
    public int WindowTopY(IntPtr hwnd) => IsMaximized(hwnd) ? FrameTopBorderThickness(hwnd, false) : 1;

    /// <summary>
    /// 还原态客户区内缩（DIP）：<b>顶部 0、左右下为边框厚度</b>。
    ///
    /// 顶部归零所以没有原生标题栏（有一点非客户区，Windows 就会在客户区外画原生标题栏）；
    /// 左右下留厚度所以那圈不可见边框还在 —— 系统的 Snap 热区才和自绘按钮对得上。
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
    /// 非最大化：顶部 0（有非客户区的话 Windows 会在客户区外画原生标题栏），左右下为边框厚度。
    /// 最大化：四边都要厚度 —— 系统会把窗口矩形相对屏幕各边外扩一个边框厚度，
    /// 四边都留才能让客户区不溢到相邻显示器上。
    /// </summary>
    public Thickness GetClientAreaInsetsPx(IntPtr hwnd)
    {
        double thickness = FrameTopBorderThicknessPx(hwnd, restored: true);
        double top = IsMaximized(hwnd) || IsFullscreen(hwnd) ? thickness : 0;
        return new Thickness(thickness, top, thickness, thickness);
    }

    #endregion

    // 按钮的摆位与尺寸不在这里：按钮是样式模板里的元素，本类只管几何与命中。

    #region 非客户区命中判定

    /// <summary>
    /// 分支顺序：**先标题栏按钮 → 再排除系统按钮区 → 再缩放边 → 最后 caption 条带**。
    /// 返回 <see cref="NativeMethods.HTNotHandled"/> 表示不接管。
    ///
    /// 缩放边只处理**顶部**：左右下那三边的 resize 目标落在客户区之外的真实非客户区里，
    /// 系统自己会回 HTLEFT/HTBOTTOM/…；顶部没有原生标题栏，只能我们自己给一条内部窄带。
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

        // ② 客户区之外（左/右/下那圈不可见边框）**不归我们判**，把意见交回系统，
        //    由 DefWindowProc 按窗口原生边框回 HTLEFT/HTBOTTOM/HTBOTTOMRIGHT…
        //
        // ★ 踩过的坑：对着整个窗口都回话（那三边回 HTCLIENT/HTCAPTION）会把系统的回答权抢掉，
        // 表现就是"只有顶部能拖边框缩放，其它三边拖不动"。
        if (!IsInsideClientArea(hwnd, windowPointDip, windowSizeDip))
        {
            return NativeMethods.HTNotHandled;
        }

        // ③ 系统按钮区上不显示缩放光标：Win8 之后系统按钮几乎顶在右上角，
        //    不排除的话悬停在按钮上会变成缩放光标。
        //    顶部再内缩 1px（物理像素），因为按钮正上方有一条 1px 的缝会让缩放边框"露出来"。
        //
        // ★ 踩过的坑：这里必须回 HTCAPTION，不能像原来那样交回系统。
        //   系统按「三颗键」预留按钮区，比我们实际画的宽（CanMinimize 只画两颗、NoResize 只画一颗），
        //   多出来那格交回系统会拿到 HTCLIENT —— 表现就是「收起的那颗键左边一格拖不动窗口」（实测死带宽度正好一个按钮）。
        //   HTCAPTION 同样是箭头，本步骤「别在系统按钮上露缩放光标」的目的照样满足。
        if (TryGetSystemCaptionButtonBoundsDip(hwnd, out Rect systemButtons)
            && systemButtons.Contains(windowPointDip))
        {
            // return NativeMethods.HTNotHandled;
            // ★ 预留区比我们画的宽出来的那一条，同样要先问逃生出口：
            //   用户放进标题栏的控件（如主题按钮）贴着系统按钮放时正好压在这条上，
            //   一律回 HTCAPTION 会把它变成非客户区 —— 表现是"贴近系统按钮那一小条移上去没反应"。
            return _window.IsInteractiveCaptionElementAt(ToClientPoint(hwnd, windowPointDip))
                ? NativeMethods.HTCLIENT
                : NativeMethods.HTCAPTION;
        }

        // ④ 顶部那条内部缩放带。左右下已经在 ② 交出去了，**只有顶部需要我们自己判** ——
        // 没有原生标题栏，顶部就没有系统给的 resize 边。
        // 宽度用 ResizeInsideBoundsSize，两端用 ResizeAreaCornerSize 切角。
        int component = GetHtComponentForFrame(
            windowPointDip,
            windowSizeDip,
            ResizeInsideBoundsSize,
            ResizeAreaCornerSize,
            CanResize);
        if (component != NativeMethods.HTNotHandled)
        {
            // 逃生出口同样优先：控件顶到标题栏最上面这 5 DIP 时，别把它的鼠标消息当成缩放边吃掉
            // —— 否则那条控件"移上去变缩放光标、按下去变成拉窗口"。
            return _window.IsInteractiveCaptionElementAt(ToClientPoint(hwnd, windowPointDip))
                ? NativeMethods.HTCLIENT
                : component;
        }

        // ⑤ caption 条带。
        //    clientPoint 就是客户区坐标（客户区原点即 WPF 根的坐标原点），所以这里可以直接用它判高度。
        Point clientPoint = ToClientPoint(hwnd, windowPointDip);
        if (clientPoint.Y < CaptionStripHeight(hwnd))
        {
            // 这点上有"标了逃生出口"的 WPF 元素就交回 WPF（HTCLIENT），
            // 否则当拖动区（HTCAPTION）。
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

    /// <summary>顶部缩放带的分段：两端 <c>cornerSize</c> 之内算角，中间算 HTTOP；不接管时返回 HTNotHandled。</summary>
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
    /// 系统认为的标题栏按钮区（DIP、窗口坐标系）。拿不到就不排除（返回 false）。
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

        // 顶部再内缩 1px：按钮正上方那条 1px 的缝会让缩放边框露出来
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

    /// <summary>窗口那圈不可见边框的厚度（物理像素）= <c>SM_CXSIZEFRAME + SM_CXPADDEDBORDER</c>。</summary>
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
