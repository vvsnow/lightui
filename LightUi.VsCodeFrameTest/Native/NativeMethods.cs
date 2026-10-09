using System.Runtime.InteropServices;

namespace LightUi.VsCodeFrameTest.Native;

/// <summary>
/// Win32 的常量、结构体和 P/Invoke，集中一处。
///
/// 对应 Chromium 里散落的 <c>ui/views/win/*</c> + <c>ui/base/win/hwnd_metrics.cc</c> 用到的那些定义；
/// 这里只是把用得到的部分收拢起来，值都用 <c>Winuser.h</c> / <c>Dwmapi.h</c> 的原始编号。
/// </summary>
internal static class NativeMethods
{
    #region 消息

    public const int WM_NCCALCSIZE = 0x0083;
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_NCACTIVATE = 0x0086;
    public const int WM_NCPAINT = 0x0085;
    public const int WM_NCMOUSEMOVE = 0x00A0;
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int WM_NCLBUTTONUP = 0x00A2;
    public const int WM_NCLBUTTONDBLCLK = 0x00A3;
    public const int WM_NCMOUSELEAVE = 0x02A2;
    public const int WM_SIZE = 0x0005;
    public const int WM_GETMINMAXINFO = 0x0024;
    public const int WM_WINDOWPOSCHANGED = 0x0047;
    public const int WM_DPICHANGED = 0x02E0;
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_THEMECHANGED = 0x031A;
    public const int WM_DWMCOMPOSITIONCHANGED = 0x031E;

    // 下面两条是「系统偷偷画原生标题栏」的私人消息（没进公开头文件）。
    // Chromium 专门吃掉它们，否则窗口在激活/尺寸变化时会闪一下原生标题栏。
    public const int WM_NCUAHDRAWCAPTION = 0x00AE;
    public const int WM_NCUAHDRAWFRAME = 0x00AF;

    #endregion

    #region 命中测试返回值（MS 文档已核对）

    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTMINBUTTON = 8;
    public const int HTMAXBUTTON = 9;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;
    public const int HTCLOSE = 20;

    /// <summary>本工程用 0 表示「不接管这条 WM_NCHITTEST，交回 WPF」。</summary>
    public const int HTNotHandled = 0;

    #endregion

    #region 系统度量

    public const int SM_CXSIZEFRAME = 32;
    public const int SM_CYSIZEFRAME = 33;
    public const int SM_CXPADDEDBORDER = 92;
    public const int SM_CYCAPTION = 4;

    public const uint MONITOR_DEFAULTTONEAREST = 2;

    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CAPTION_BUTTON_BOUNDS = 5;

    #endregion

    #region 结构体

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>WM_NCCALCSIZE 且 wParam=TRUE 时 lParam 指向的结构。只关心 rgrc[0]（提议的客户区）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NCCALCSIZE_PARAMS
    {
        public RECT Rgrc0;
        public RECT Rgrc1;
        public RECT Rgrc2;
        public IntPtr WindowPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int CbSize;
        public RECT RcMonitor;
        public RECT RcWork;
        public uint DwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int LeftWidth;
        public int RightWidth;
        public int TopHeight;
        public int BottomHeight;
    }

    #endregion

    #region P/Invoke

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetricsForDpi(int index, uint dpi);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    #endregion
}
