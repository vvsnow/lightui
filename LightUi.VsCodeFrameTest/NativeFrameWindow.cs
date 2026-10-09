using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

using LightUi.VsCodeFrameTest.Native;

namespace LightUi.VsCodeFrameTest;

/// <summary>
/// 无边框窗口，**不使用 WPF 的 WindowChrome**：非客户区行为全部按 Electron / Chromium
/// （VS Code 就是这套）的做法自己处理。
///
/// <list type="bullet">
/// <item><see cref="Native.HwndMessageHandler"/> —— 对应 <c>ui/views/win/hwnd_message_handler.cc</c></item>
/// <item><see cref="Native.WinFrameView"/> —— 对应 <c>shell/browser/ui/views/win_frame_view.cc</c></item>
/// <item><see cref="Native.WinCaptionButtonContainer"/> / <see cref="Native.WinCaptionButton"/> ——
/// 对应 Electron 的同名文件</item>
/// </list>
///
/// 这个类本身只做两件事：把窗口该有的信息喂给 <see cref="IHwndMessageHandlerDelegate"/>，
/// 以及把过程判出来的悬停状态暴露给 XAML。
/// </summary>
public class NativeFrameWindow : Window, IHwndMessageHandlerDelegate
{
    private const int GWL_STYLE = -16;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    #region 标题栏高度（自定义）

    /// <summary>
    /// 自定义标题栏高度（DIP）。0 = 用算出来的默认高度。
    ///
    /// 对应 Electron 的 <c>titleBarOverlay.height</c> → <c>window()->titlebar_overlay_height()</c>，
    /// 最终进 <c>WinFrameView::TitlebarHeight(int custom_height)</c>。
    ///
    /// ★ 注意参考实现的那个判断：<c>if (custom_height &gt; TitlebarMaximizedVisualHeight())</c>
    /// —— 也就是说**只有比 SM_CYCAPTION（本机 23 DIP）更高时这个值才生效**，
    /// 想调得比默认还矮是做不到的（这是 Electron 的行为，我这里照抄不擅自放宽）。
    /// </summary>
    public static readonly DependencyProperty TitlebarOverlayHeightProperty =
        DependencyProperty.Register(
            nameof(TitlebarOverlayHeight),
            typeof(double),
            typeof(NativeFrameWindow),
            new PropertyMetadata(0.0, OnTitlebarOverlayHeightChanged));

    public double TitlebarOverlayHeight
    {
        get => (double)GetValue(TitlebarOverlayHeightProperty);
        set => SetValue(TitlebarOverlayHeightProperty, value);
    }

    private static void OnTitlebarOverlayHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 改高度要重摆：标题栏行高和四个按钮的高度都来自 TitlebarHeight()
        (d as NativeFrameWindow)?._handler?.Relayout();
    }

    #endregion

    #region 悬停状态（只读 DP，给 XAML / 诊断面板读）

    private static readonly DependencyPropertyKey HoveredCaptionButtonKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HoveredCaptionButton),
            typeof(CaptionButton),
            typeof(NativeFrameWindow),
            new PropertyMetadata(CaptionButton.None));

    public static readonly DependencyProperty HoveredCaptionButtonProperty =
        HoveredCaptionButtonKey.DependencyProperty;

    /// <summary>鼠标当前停在哪个标题栏按钮上。返回 HT* 之后 WPF 收不到鼠标事件，只能这样告诉 XAML。</summary>
    public CaptionButton HoveredCaptionButton => (CaptionButton)GetValue(HoveredCaptionButtonProperty);

    #endregion

    #region 每实例状态

    private HwndMessageHandler? _handler;
    private IntPtr _hwnd;

    private Canvas? _captionHost;
    private FrameworkElement? _titleBar;

    // 诊断：命中测试走了没有、返回了什么
    private int _hitTestCount;
    private int _lastHitCode = int.MinValue;
    private Point _lastHitPoint;

    #endregion

    public NativeFrameWindow()
    {
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        SizeChanged += (_, _) => _handler?.Relayout();
        StateChanged += (_, _) => _handler?.Relayout();
    }

    #region 挂载 / 卸载

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        HwndSource? source = HwndSource.FromHwnd(_hwnd);
        if (source is null)
        {
            return;
        }

        _captionHost = FindName("PART_CaptionButtonHost") as Canvas;
        _titleBar = FindName("PART_TitleBar") as FrameworkElement;

        _handler = new HwndMessageHandler(this);
        _handler.Attach(_hwnd, source);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _handler?.Detach();
        _handler = null;
        _hwnd = IntPtr.Zero;
    }

    #endregion

    #region IHwndMessageHandlerDelegate

    Canvas IHwndMessageHandlerDelegate.CaptionButtonHost =>
        _captionHost ?? throw new InvalidOperationException(
            "找不到 PART_CaptionButtonHost：标题栏里必须有一个铺满整条标题栏的 Canvas 当按钮宿主。");

    double IHwndMessageHandlerDelegate.DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    double IHwndMessageHandlerDelegate.ClientWidthDip => ClientSizeDip().Width;

    bool IHwndMessageHandlerDelegate.IsMinimizable => ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip or ResizeMode.CanMinimize;

    bool IHwndMessageHandlerDelegate.IsMaximizable => ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

    bool IHwndMessageHandlerDelegate.IsClosable => true;

    bool IHwndMessageHandlerDelegate.CanResize => ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

    int IHwndMessageHandlerDelegate.TitlebarOverlayHeight => (int)TitlebarOverlayHeight;

    /// <summary>
    /// 对应 Electron 的 <c>GetDwmFrameInsetsInPixels</c>：全 0 —— 不扩展玻璃，但这条调用本身
    /// 让窗口进入 DWM frame 通道（见 <see cref="HwndMessageHandler"/> 里 UpdateDwmFrame 的注释）。
    /// </summary>
    NativeMethods.MARGINS IHwndMessageHandlerDelegate.GetDwmFrameInsetsInPixels() => default;

    void IHwndMessageHandlerDelegate.SetHoveredCaptionButton(CaptionButton button)
        => SetValue(HoveredCaptionButtonKey, button);

    /// <summary>
    /// 对应 Electron 按钮回调里的 <c>NativeWindow::Minimize/Maximize/Restore/Close</c>。
    /// 走 SystemCommands：它内部是 <c>PostMessage(WM_SYSCOMMAND)</c>，异步执行，
    /// 不会在钩子里重入消息循环。
    /// </summary>
    void IHwndMessageHandlerDelegate.PerformCaptionButtonAction(CaptionButton button)
    {
        switch (button)
        {
            case CaptionButton.Minimize:
                SystemCommands.MinimizeWindow(this);
                break;

            case CaptionButton.Maximize:
                SystemCommands.MaximizeWindow(this);
                break;

            case CaptionButton.Restore:
                SystemCommands.RestoreWindow(this);
                break;

            case CaptionButton.Close:
                SystemCommands.CloseWindow(this);
                break;
        }
    }

    void IHwndMessageHandlerDelegate.RecordHitTest(Point screenPointPx, int hitCode)
    {
        _hitTestCount++;
        _lastHitPoint = screenPointPx;
        _lastHitCode = hitCode;
    }

    void IHwndMessageHandlerDelegate.SetTitlebarHeight(double heightDip)
    {
        if (_titleBar is not null && Math.Abs(_titleBar.Height - heightDip) > 0.5 && heightDip > 0)
        {
            _titleBar.Height = heightDip;
        }
    }

    #endregion

    #region 诊断

    /// <summary>窗口当前的原生几何信息，单位都是物理像素。</summary>
    public readonly record struct NativeGeometry(
        bool Available,
        RectWindow WindowRect,
        RectWindow VisibleFrameRect,
        int ClientWidth,
        int ClientHeight,
        int FrameThickness,
        long StyleBits,
        bool IsZoomed);

    public readonly record struct RectWindow(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    public NativeGeometry ReadGeometry()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return new NativeGeometry(false, default, default, 0, 0, 0, 0, false);
        }

        GetWindowRect(_hwnd, out RECT windowRect);
        GetClientRect(_hwnd, out RECT clientRect);

        int hr = DwmGetWindowAttribute(_hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT frame, Marshal.SizeOf<RECT>());
        bool hasFrame = hr == 0 && frame.Right > frame.Left;

        return new NativeGeometry(
            true,
            new RectWindow(windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom),
            hasFrame
                ? new RectWindow(frame.Left, frame.Top, frame.Right, frame.Bottom)
                : new RectWindow(windowRect.Left, windowRect.Top, windowRect.Right, windowRect.Bottom),
            clientRect.Right - clientRect.Left,
            clientRect.Bottom - clientRect.Top,
            WinFrameView.GetFrameThicknessFromWindowPx(_hwnd),
            GetWindowLong(_hwnd, GWL_STYLE) & 0xFFFFFFFFL,
            NativeMethods.IsZoomed(_hwnd));
    }

    /// <summary>诊断：UpdateDwmFrame 调没调、DWM 返回什么（HRESULT 0 = S_OK）。</summary>
    public string DwmFrameReport => _handler is null
        ? "窗口过程还没挂上"
        : $"UpdateDwmFrame 调用 {_handler.DwmFrameCallCount} 次，DwmExtendFrameIntoClientArea HRESULT=0x{_handler.DwmFrameResult:X8}";

    public string HitTestTrace =>
        _hitTestCount == 0
            ? "WM_NCHITTEST 一次都没收到（说明没走 DWM frame 通道）"
            : $"共 {_hitTestCount} 次；最近一次 屏幕点=({(int)_lastHitPoint.X},{(int)_lastHitPoint.Y}) 返回={DescribeHitCode(_lastHitCode)}";

    public string CaptionButtonReport()
    {
        if (_handler is null)
        {
            return "窗口过程还没挂上";
        }

        WinCaptionButtonContainer container = _handler.FrameView.CaptionButtons;
        var parts = new List<string>();

        foreach (WinCaptionButton button in container.Buttons)
        {
            Rect b = button.Bounds;
            parts.Add($"{(button.IsShown ? "" : "(隐藏)")}{button.Kind}={b.Left:F0},{b.Top:F0} {b.Width:F0}x{b.Height:F0}");
        }

        return string.Join("  ", parts);
    }

    private static string DescribeHitCode(int code) => code switch
    {
        NativeMethods.HTCLIENT => "HTCLIENT(1)",
        NativeMethods.HTCAPTION => "HTCAPTION(2)",
        NativeMethods.HTMINBUTTON => "HTMINBUTTON(8)",
        NativeMethods.HTMAXBUTTON => "HTMAXBUTTON(9)",
        NativeMethods.HTCLOSE => "HTCLOSE(20)",
        NativeMethods.HTTOP => "HTTOP(12)",
        NativeMethods.HTTOPLEFT => "HTTOPLEFT(13)",
        NativeMethods.HTTOPRIGHT => "HTTOPRIGHT(14)",
        NativeMethods.HTNotHandled => "不接管(0) → 交给系统/WPF",
        int.MinValue => "（还没进过钩子）",
        _ => $"其他({code})",
    };

    private Size ClientSizeDip()
    {
        if (_hwnd == IntPtr.Zero || !GetClientRect(_hwnd, out RECT rect))
        {
            return new Size(ActualWidth, ActualHeight);
        }

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return new Size((rect.Right - rect.Left) / scale, (rect.Bottom - rect.Top) / scale);
    }

    /// <summary>
    /// 把当前几何写成一份机器可读的文本，供验收脚本读取。
    ///
    /// 为什么需要：按 Electron 的做法，标题栏按钮是自绘的 Canvas + Path，不是 WPF Button，
    /// UIAutomation 看不见它们 —— 脚本没法像普通控件那样查到按钮矩形。
    /// 所以由窗口自己把「四个按钮在屏幕上的实际矩形」报出来，脚本读这个文件。
    /// </summary>
    public void WriteDiagnosticsDump(string path)
    {
        if (_hwnd == IntPtr.Zero || _handler is null)
        {
            return;
        }

        NativeGeometry g = ReadGeometry();
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;

        Thickness insets = _handler.FrameView.GetClientAreaInsetsPx(_hwnd);

        // 客户区原点（屏幕物理像素）= 窗口矩形左上角 + 客户区内缩
        int clientOriginX = g.WindowRect.Left + (int)insets.Left;
        int clientOriginY = g.WindowRect.Top + (int)insets.Top;

        var lines = new List<string>
        {
            $"window={g.WindowRect.Left},{g.WindowRect.Top},{g.WindowRect.Right},{g.WindowRect.Bottom}",
            $"invisibleBorder={g.VisibleFrameRect.Left - g.WindowRect.Left},{g.VisibleFrameRect.Top - g.WindowRect.Top},{g.WindowRect.Right - g.VisibleFrameRect.Right},{g.WindowRect.Bottom - g.VisibleFrameRect.Bottom}",
            $"client={g.ClientWidth},{g.ClientHeight}",
            $"frameThickness={g.FrameThickness}",
            $"dpiScale={scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"hwnd={_hwnd}",
            $"windowState={WindowState}",
            $"isZoomed={NativeMethods.IsZoomed(_hwnd)}",
            $"hitTestCount={_hitTestCount}",
            $"lastHitCode={_lastHitCode}",
        };

        foreach (WinCaptionButton button in _handler.FrameView.CaptionButtons.Buttons)
        {
            if (!button.IsShown)
            {
                continue;
            }

            // 按钮矩形（客户区 DIP）→ 屏幕物理像素
            int left = clientOriginX + (int)Math.Round(button.Bounds.Left * scale);
            int top = clientOriginY + (int)Math.Round(button.Bounds.Top * scale);
            int right = clientOriginX + (int)Math.Round(button.Bounds.Right * scale);
            int bottom = clientOriginY + (int)Math.Round(button.Bounds.Bottom * scale);

            lines.Add($"button={button.Kind},{left},{top},{right},{bottom}");
        }

        try
        {
            System.IO.File.WriteAllLines(path, lines);
            DiagnosticsDumpStatus = $"已写入 {path}";
        }
        catch (Exception ex)
        {
            // 写不了不影响窗口，但要把原因显示出来，别让人对着一个空文件猜
            DiagnosticsDumpStatus = $"写失败 {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>诊断旁路的写入结果（给面板显示）。</summary>
    public string DiagnosticsDumpStatus { get; private set; } = "（还没写）";

    #endregion
}
