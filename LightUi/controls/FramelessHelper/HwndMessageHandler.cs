using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LightUi.Controls.Frameless;

/// <summary>
/// 窗口过程那一层。对应 Chromium <c>ui/views/win/hwnd_message_handler.cc</c> 的 <c>HWNDMessageHandler</c>：
/// 它挂 <c>HwndSource</c> 钩子、分发消息，并在合适的时机调 <c>UpdateDwmFrame()</c>。
///
/// <para>框架模式：本工程自绘标题栏，所以是 <see cref="FrameMode.CustomDrawn"/> —— 和 Chromium 一样，
/// 这个标志用来决定 <c>WM_NCUAHDRAWCAPTION / WM_NCUAHDRAWFRAME</c> 要不要吃掉
/// （源码里就这两条：<c>SetMsgHandled(delegate_->GetFrameMode() == FrameMode::CUSTOM_DRAWN)</c>）。</para>
/// </summary>
internal sealed class HwndMessageHandler
{
    /// <summary>Chromium 的 <c>FrameMode</c>（<c>ui/views/widget/widget.h</c>）。</summary>
    public enum FrameMode
    {
        SystemDrawn,
        CustomDrawn,
    }

    private readonly IFramelessWindowHost _delegate;
    private readonly FrameView _frameView;

    private HwndSource? _source;

    /// <summary>必须缓存同一个委托实例：RemoveHook 按引用匹配。</summary>
    private HwndSourceHook? _hook;

    private IntPtr _hwnd;

    /// <summary>对应 Chromium 的 <c>last_dwm_frame_insets_</c>：避免重复的跨进程 DWM 调用。</summary>
    private NativeMethods.MARGINS? _lastDwmFrameInsets;

    private CaptionButton _pressedButton = CaptionButton.None;

    public HwndMessageHandler(IFramelessWindowHost messageDelegate)
    {
        _delegate = messageDelegate;
        _frameView = new FrameView(messageDelegate);
    }

    /// <summary>Electron 的窗口自绘标题栏，对应 <c>FrameMode::CUSTOM_DRAWN</c>。</summary>
    public FrameMode CurrentFrameMode => FrameMode.CustomDrawn;

    public FrameView FrameView => _frameView;

    public IntPtr Hwnd => _hwnd;

    #region 挂载 / 卸载

    public void Attach(IntPtr hwnd, HwndSource source)
    {
        _hwnd = hwnd;
        _source = source;

        HwndSourceHook hook = FilterMessage;
        _hook = hook;
        source.AddHook(hook);

        UpdateDwmFrame();

        // ★「启动不闪原生标题栏」的那一步。
        //
        // 窗口是带 WS_CAPTION 创建的，系统在 CreateWindowEx 里那次 WM_NCCALCSIZE
        // （早于我们挂钩子）就把原生标题栏算进非客户区并缓存了下来。之后只挂钩子、
        // 只扩 DWM frame 都不会让它重算，系统一直用缓存那份 —— 第一次上屏画出来的
        // 就是完整的原生标题栏（图标 + 标题 + — □ ✕）。
        //
        // SWP_FRAMECHANGED 的文档原文正是干这个的：
        //   "Sends a WM_NCCALCSIZE message to the window, even if the window's size is not being changed."
        // 主动向系统要一次重算，而且必须在窗口第一次上屏之前 —— SourceInitialized
        // 就是那个时刻（WPF 这时还没调 ShowWindow）。
        ForceFrameRecompute();

        // 隐身（启动不闪）**不在这里做**：那是可选项，由 FramelessHelper 按
        // CloakUntilFirstFrame 决定要不要挂完立刻 SetCloaked(true)、ContentRendered 时解除。
        // 本方法只管"窗口过程该做的事"。
        // （实测过：cloak 必须赶在窗口第一次上屏之前设 —— SourceInitialized 正是那个时刻，
        //   此刻 IsWindowVisible 还是 False，所以连"空白一帧"都不会露。）

        Relayout();
    }

    /// <summary>要一次非客户区重算，让 <see cref="OnNcCalcSize"/> 在窗口首次上屏之前先跑一遍。</summary>
    private void ForceFrameRecompute()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_FRAMECHANGED
            | NativeMethods.SWP_NOMOVE
            | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOACTIVATE);
    }

    public void Detach()
    {
        if (_hook is not null)
        {
            _source?.RemoveHook(_hook);
            _hook = null;
        }

        _source = null;
        _hwnd = IntPtr.Zero;
    }

    /// <summary>
    /// 同步窗口状态 + 按当前光标刷新悬停。窗口尺寸/状态/DPI 变化后都要来一次。
    ///
    /// 按钮的**摆位与尺寸不在这里了** —— 那是样式的事（旧实现的 LayoutCaptionButtons /
    /// UpdateButtons / SetButtonSize / ApplyVisualState 整块删掉）；本方法也**不往任何元素上写高度**。
    /// </summary>
    public void Relayout()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        SyncWindowState();

        // 状态变化后按钮可能换了可见性（最大化↔还原由样式触发器管），
        // 悬停也按当前真实光标重判一次，免得光标没动却留下过期的 hover。
        _delegate.SetHoveredCaptionButton(CurrentHoveredButton(_hwnd));
    }

    private void SyncWindowState()
    {
        _frameView.DpiScale = _delegate.DpiScale;
        _frameView.CanResize = _delegate.CanResize;
        _frameView.CaptionHeight = _delegate.CaptionHeight;
        // 是否最大化 / 全屏**不缓存**：由 frame view 直接问操作系统
        // （对应 Electron 的 frame()->IsMaximized()），这样任何时候读都是对的。
    }

    #endregion

    #region UpdateDwmFrame（Chromium 同名方法，位于 hwnd_message_handler.cc）

    /// <summary>
    /// 对应 <c>HWNDMessageHandler::UpdateDwmFrame()</c>：
    /// <code>gfx::Insets insets;
    /// if (delegate_->GetDwmFrameInsetsInPixels(&amp;insets)) {
    ///   if (last_dwm_frame_insets_ == insets) return;      // "Avoid redundant cross-process DWM calls"
    ///   last_dwm_frame_insets_ = insets;
    ///   MARGINS margins = {insets.left(), insets.right(), insets.top(), insets.bottom()};
    ///   DwmExtendFrameIntoClientArea(hwnd(), &amp;margins);
    /// }</code>
    ///
    /// Electron 的 <c>GetDwmFrameInsetsInPixels</c> 给的就是全 0（<c>*insets = gfx::Insets();</c>，
    /// 注释 "Don't extend the glass in at all if it won't be visible."）。
    ///
    /// <b>全 0 不等于不调用</b>：这一步把窗口交给 DWM 的 frame 通道。
    /// 少了它，系统不把标题栏那块当非客户区，<c>WM_NCHITTEST</c> 一次都不会来 ——
    /// 自绘按钮既没有悬停高亮，也换不来 Snap Layouts 浮窗。
    /// </summary>
    private void UpdateDwmFrame()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.MARGINS insets = _delegate.GetDwmFrameInsetsInPixels();

        if (_lastDwmFrameInsets is { } last
            && last.LeftWidth == insets.LeftWidth
            && last.RightWidth == insets.RightWidth
            && last.TopHeight == insets.TopHeight
            && last.BottomHeight == insets.BottomHeight)
        {
            return;
        }

        _lastDwmFrameInsets = insets;

        // 返回值不看：这一步是「能不能收到 WM_NCHITTEST」的开关，失败就没必要往下猜了。
        // 排查时把 0 之外的 HRESULT 打出来即可 —— 库内不留诊断字段。
        NativeMethods.DwmExtendFrameIntoClientArea(_hwnd, ref insets);
    }

    #endregion

    #region 隐身（DWM cloak，对应 Electron 的 show:false）

    /// <summary>
    /// 隐身 / 解除隐身（DWM cloak）。
    ///
    /// 只能由窗口自己的进程设：跨进程调 <c>DwmSetWindowAttribute(DWMWA_CLOAK)</c> 会返回
    /// <c>E_ACCESSDENIED (0x80070005)</c> —— 文档里这条属性的语义就是「被属主应用 cloak」
    /// （<c>DWM_CLOAKED_APP</c>）。
    /// </summary>
    public void SetCloaked(bool cloaked)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        int value = cloaked ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_CLOAK, ref value, sizeof(int));
    }

    #endregion

    #region 消息分发

    private IntPtr FilterMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_NCCALCSIZE:
                return OnNcCalcSize(hwnd, wParam, lParam, ref handled);

            case NativeMethods.WM_NCHITTEST:
                return OnNcHitTest(hwnd, lParam, ref handled);

            case NativeMethods.WM_NCLBUTTONDOWN:
                // 记下按下的是哪颗按钮，并把这组消息吃掉：动作由我们自己执行。
                // 依据：参考实现里按钮是 views::Button，点击回调直接调
                // NativeWindow::Minimize/Maximize/Restore/Close —— 不是让系统去做。
                // DefWindowProc 也不会替我们做：那块在系统看来是客户区。
                _pressedButton = CurrentHoveredButton(hwnd);
                _delegate.SetPressedCaptionButton(_pressedButton);
                handled = _pressedButton != CaptionButton.None;
                break;

            case NativeMethods.WM_NCLBUTTONUP:
                // 抬起时指针仍停在同一颗按钮上才执行 —— 这就是"按下后拖走可取消"的手感
                if (_pressedButton != CaptionButton.None)
                {
                    if (CurrentHoveredButton(hwnd) == _pressedButton)
                    {
                        _delegate.PerformCaptionButtonAction(_pressedButton);
                    }

                    _pressedButton = CaptionButton.None;
                    _delegate.SetPressedCaptionButton(CaptionButton.None);
                    handled = true;
                }

                UpdateHoverFromCursor(hwnd);
                break;

            case NativeMethods.WM_NCMOUSELEAVE:
                _pressedButton = CaptionButton.None;
                _delegate.SetPressedCaptionButton(CaptionButton.None);
                UpdateHoverFromCursor(hwnd);
                break;

            case NativeMethods.WM_NCRBUTTONUP:
                // 标题栏右键 → 自己弹系统菜单。
                //
                // 实测（2026-10-10）：系统确实把 WM_NCRBUTTONUP(HTCAPTION) 送到了这里（消息轨迹里有），
                // 我们也没设 handled、让它继续往下走，可菜单就是不弹 —— 而同一个菜单 Alt+Space 能弹、
                // SC_MINIMIZE/MAXIMIZE/RESTORE 也都能执行。既然这一段没人做，就自己做。
                if ((long)wParam == NativeMethods.HTCAPTION && ShowSystemMenu(hwnd, lParam))
                {
                    // 防重：万一哪天真有别人也弹，别冒出两个菜单
                    handled = true;
                }

                break;

            case NativeMethods.WM_NCUAHDRAWCAPTION:
            case NativeMethods.WM_NCUAHDRAWFRAME:
                // "系统偷偷画原生标题栏"的两条私人消息。自绘标题栏时必须吃掉，
                // 否则激活/尺寸变化时会闪一下原生标题栏。
                if (CurrentFrameMode == FrameMode.CustomDrawn)
                {
                    handled = true;
                    return IntPtr.Zero;
                }

                break;

            case NativeMethods.WM_GETMINMAXINFO:
                OnGetMinMaxInfo(hwnd, lParam);
                break;

            case NativeMethods.WM_SIZE:
                // 尺寸/最大化状态变了就重摆按钮（Chromium 的 handler 也处理 WM_SIZE）。
                // 光靠 WPF 的 StateChanged 不够稳：最大化时它不一定先于这里到。
                // ★ 不设 handled，WPF 自己要这条消息做布局。
                Relayout();
                break;

            case NativeMethods.WM_WINDOWPOSCHANGED:
                // Chromium 也是在 WM_WINDOWPOSCHANGED 里 UpdateDwmFrame。
                // ★ 绝不设 handled：WPF 自己要这条消息做布局。
                UpdateDwmFrame();
                Relayout();
                break;

            case NativeMethods.WM_DPICHANGED:
            case NativeMethods.WM_SETTINGCHANGE:
            case NativeMethods.WM_THEMECHANGED:
                // 度量可能变了，重算。同样不设 handled —— WM_DPICHANGED 更要留给 WPF，
                // 它要按 lParam 里的建议矩形 SetWindowPos（吃掉它窗口会卡在旧尺寸）。
                Relayout();
                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 对应 <c>HWNDMessageHandler::OnNCCalcSize</c>：用 frame view 给的 insets 改客户区，返回 0
    /// （不是 WVR_REDRAW —— 返回它会整窗重绘，拖边框缩放时肉眼可见闪烁）。
    /// </summary>
    private IntPtr OnNcCalcSize(IntPtr hwnd, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (wParam == IntPtr.Zero)
        {
            // wParam = FALSE：只是查询客户区，不改动
            handled = true;
            return IntPtr.Zero;
        }

        SyncWindowState();

        Thickness insets = _frameView.GetClientAreaInsetsPx(hwnd);
        NativeMethods.NCCALCSIZE_PARAMS p = Marshal.PtrToStructure<NativeMethods.NCCALCSIZE_PARAMS>(lParam);

        p.Rgrc0.Left += (int)insets.Left;
        p.Rgrc0.Top += (int)insets.Top;
        p.Rgrc0.Right -= (int)insets.Right;
        p.Rgrc0.Bottom -= (int)insets.Bottom;

        Marshal.StructureToPtr(p, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>对应 <c>HWNDMessageHandler::OnNCHitTest</c>。</summary>
    private IntPtr OnNcHitTest(IntPtr hwnd, IntPtr lParam, ref bool handled)
    {
        // lParam 的低/高 16 位是屏幕物理像素，且是有符号的 —— 多显示器摆在主屏左/上时坐标是负的
        long packed = (long)lParam;
        var screenPointPx = new Point(
            unchecked((short)( packed & 0xFFFF )),
            unchecked((short)( ( packed >> 16 ) & 0xFFFF )));

        if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT windowRect))
        {
            return IntPtr.Zero;
        }

        SyncWindowState();

        double scale = _delegate.DpiScale;
        var windowPointDip = new Point(
            ( screenPointPx.X - windowRect.Left ) / scale,
            ( screenPointPx.Y - windowRect.Top ) / scale);

        var windowSizeDip = new Size(
            ( windowRect.Right - windowRect.Left ) / scale,
            ( windowRect.Bottom - windowRect.Top ) / scale);

        int code = _frameView.NonClientHitTest(hwnd, screenPointPx, windowPointDip, windowSizeDip);

        // 悬停外观：命中的是哪个按钮。写成宿主上的两个只读附加属性、由样式触发器呈现 ——
        // 返回 HT* 之后那块是非客户区，WPF 输入栈不参与，Button.IsMouseOver 恒为 false。
        CaptionButton hovered = code switch
        {
            NativeMethods.HTMINBUTTON => CaptionButton.Minimize,
            // 最大化/还原是同一个按钮元素（样式按 WindowState 换字形与命令），所以按当前状态报身份：
            // 已最大化报 Restore —— 动作按身份派发，报 Maximize 的话"点还原"会变成空操作。
            NativeMethods.HTMAXBUTTON => FrameView.IsMaximized(hwnd) ? CaptionButton.Restore : CaptionButton.Maximize,
            NativeMethods.HTCLOSE => CaptionButton.Close,
            _ => CaptionButton.None,
        };

        _delegate.SetHoveredCaptionButton(hovered);

        if (code == NativeMethods.HTNotHandled)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(code);
    }

    /// <summary>
    /// 在右键点弹出系统菜单，选中的命令回送给窗口。
    ///
    /// 用 <c>TPM_RETURNCMD</c> 拿返回值再 <c>PostMessage(WM_SYSCOMMAND)</c>：这样不用自己处理
    /// <c>WM_COMMAND</c>，命令走的也是和 <c>DefWindowProc</c> 同一条下发路径 ——
    /// 所以「还原 / 移动 / 大小 / 最小化 / 最大化 / 关闭」这些都不用手写。
    /// </summary>
    private static bool ShowSystemMenu(IntPtr hwnd, IntPtr lParam)
    {
        IntPtr menu = NativeMethods.GetSystemMenu(hwnd, revert: false);
        if (menu == IntPtr.Zero)
        {
            return false;
        }

        // 右键点的屏幕坐标就在 lParam 里：低 16 位 x、高 16 位 y，且都是有符号的
        long packed = (long)lParam;
        int x = unchecked((short)(packed & 0xFFFF));
        int y = unchecked((short)((packed >> 16) & 0xFFFF));

        int command = NativeMethods.TrackPopupMenuEx(
            menu,
            NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
            x,
            y,
            hwnd,
            IntPtr.Zero);

        if (command != 0)
        {
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_SYSCOMMAND, new IntPtr(command), IntPtr.Zero);
        }

        return true;
    }

    /// <summary>
    /// 鼠标离开非客户区。这里**不能**盲目清掉悬停：我们给标题栏返回的是非客户区 HT 码，
    /// 而系统认为那块是客户区，于是刚设上的悬停会被一条 WM_NCMOUSELEAVE 立刻清掉。
    /// 所以按当前真实光标位置重新判一次。
    /// </summary>
    private void UpdateHoverFromCursor(IntPtr hwnd)
    {
        _delegate.SetHoveredCaptionButton(CurrentHoveredButton(hwnd));
    }

    /// <summary>
    /// 光标当前在哪颗按钮上。判定交给宿主（按模板里三个 Button 的**实测尺寸**算），
    /// 这里只负责「光标在不在窗口上」这一道 —— 不在就明说 None。
    /// </summary>
    private CaptionButton CurrentHoveredButton(IntPtr hwnd)
    {
        if (_hwnd == IntPtr.Zero
            || !NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT windowRect)
            || !NativeMethods.GetCursorPos(out NativeMethods.POINT cursor))
        {
            return CaptionButton.None;
        }

        bool inside = cursor.X >= windowRect.Left && cursor.X <= windowRect.Right
                      && cursor.Y >= windowRect.Top && cursor.Y <= windowRect.Bottom;

        return inside
            ? _delegate.HitTestCaptionButtonAt(new Point(cursor.X, cursor.Y))
            : CaptionButton.None;
    }

    /// <summary>
    /// 对应 <c>HWNDMessageHandler::OnGetMinMaxInfo</c>：
    /// "Add the native frame border size to the minimum and maximum size if the view reports its size
    /// as the client size." 窗口有 WS_THICKFRAME，系统给的 track size 是「窗口矩形」尺寸，
    /// 而 WPF 的 MinWidth/MinHeight 是客户区尺寸 —— 不加回去，MinWidth=520 实际会少掉两边边框。
    /// </summary>
    private void OnGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        int frame = FrameView.GetFrameThicknessFromWindowPx(hwnd);

        info.PtMinTrackSize.X += frame * 2;
        info.PtMinTrackSize.Y += frame * 2;

        Marshal.StructureToPtr(info, lParam, false);
        // 不设 handled：其余字段（ptMaxSize / ptMaxPosition 等）交给系统
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public NativeMethods.POINT PtReserved;
        public NativeMethods.POINT PtMaxSize;
        public NativeMethods.POINT PtMaxPosition;
        public NativeMethods.POINT PtMinTrackSize;
        public NativeMethods.POINT PtMaxTrackSize;
    }

    #endregion
}

